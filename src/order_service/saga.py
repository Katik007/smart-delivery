import json
import logging
import grpc
from typing import Dict, List, Tuple

from src.order_service.models import Order, OrderItem, Payment, OrderSagaState, OutboxMessage
from src.order_service.repository import OrderRepository
from src.common.domain import OrderStatus, SagaStepStatus, OrderPaidIntegrationEvent
from src.common.grpc_stubs import stock_pb2, stock_pb2_grpc


class OrderSagaOrchestrator:
    def __init__(self, orders_db, payments_db, saga_db, outbox_db, grpc_target: str = "localhost:50051", *, allow_offline=True):
        self.orders_db = orders_db
        self.payments_db = payments_db
        self.saga_db = saga_db
        self.outbox_db = outbox_db
        self.order_repo = OrderRepository(orders_db, outbox_db)
        self.grpc_target = grpc_target
        self.allow_offline = allow_offline
        self._grpc_channel = None
        self._stock_client = None

    def _get_grpc_client(self):
        if not self._stock_client:
            self._grpc_channel = grpc.insecure_channel(self.grpc_target)
            self._stock_client = stock_pb2_grpc.StockGrpcServiceStub(self._grpc_channel)
        return self._stock_client

    def execute_saga(self, order: Order, payment_card_valid: bool = True) -> Tuple[bool, str]:
        saga_state = OrderSagaState(order_id=order.id, current_step=SagaStepStatus.STARTED)
        reserved_items: List[Tuple[str, int]] = []

        try:
            client = self._get_grpc_client()
        except Exception:
            client = None

        # Step 1: Check and reserve stock via gRPC
        for item in order.items:
            reserved = False
            if client:
                try:
                    response = client.CheckAndReserveStock(
                        stock_pb2.StockReserveRequest(product_id=item.product_id, quantity=item.quantity),
                        timeout=3.0
                    )
                    reserved = response.success
                except Exception:
                    reserved = self.allow_offline
            else:
                reserved = self.allow_offline

            if not reserved:
                self._compensate_stock(reserved_items, client)
                order.status = OrderStatus.CANCELLED
                saga_state.current_step = SagaStepStatus.FAILED
                saga_state.error_message = f"Stock reservation failed for {item.product_id}"
                self.saga_db[saga_state.saga_id] = saga_state
                self.order_repo.save_order_with_outbox(order)
                return False, saga_state.error_message

            reserved_items.append((item.product_id, item.quantity))

        saga_state.current_step = SagaStepStatus.STOCK_RESERVED
        order.status = OrderStatus.STOCK_RESERVED

        # Step 2: Payment step
        payment = Payment(
            order_id=order.id,
            amount=order.total_amount,
            status="Pending"
        )
        if not payment_card_valid:
            # Payment failed! TRIGGER COMPENSATION STEP
            payment.status = "Failed"
            rolled_back = self._compensate_stock(reserved_items, client)
            saga_state.current_step = SagaStepStatus.COMPENSATED if rolled_back else SagaStepStatus.FAILED
            saga_state.is_compensated = rolled_back
            saga_state.error_message = (
                "Payment rejected by bank. Stock reservation rolled back."
                if rolled_back else "Payment rejected by bank. Stock release could not be confirmed."
            )
            order.status = OrderStatus.CANCELLED
            self.payments_db[payment.id] = payment
            self.saga_db[saga_state.saga_id] = saga_state
            self.order_repo.save_order_with_outbox(order)
            return False, saga_state.error_message

        # Payment Succeeded
        payment.status = "Succeeded"
        order.status = OrderStatus.PAID
        saga_state.current_step = SagaStepStatus.PAID
        self.payments_db[payment.id] = payment
        self.saga_db[saga_state.saga_id] = saga_state

        # Step 3: Transaction Outbox Pattern (бизнес-логика + Outbox в единой транзакции)
        event = OrderPaidIntegrationEvent(
            order_id=order.id,
            customer_id=order.customer_id,
            delivery_address=order.delivery_address,
            total_amount=order.total_amount,
            items=[{"product_id": i.product_id, "quantity": i.quantity} for i in order.items]
        )
        outbox_msg = OutboxMessage(
            event_type="OrderPaid",
            payload=json.dumps(event.model_dump())
        )
        
        # Атомарное сохранение изменения заказа и записи в outbox через репозиторий
        self.order_repo.save_order_with_outbox(order, outbox_msg)

        return True, "Order placed, stock reserved, payment processed, outbox recorded"

    def _compensate_stock(self, reserved_items: List[Tuple[str, int]], client):
        """Compensating transaction: releases previously reserved stock on catalog service."""
        released = True
        for product_id, quantity in reserved_items:
            if client:
                try:
                    response = client.ReleaseStock(
                        stock_pb2.StockReleaseRequest(product_id=product_id, quantity=quantity),
                        timeout=3.0
                    )
                    released = released and response.success
                except grpc.RpcError:
                    logging.getLogger(__name__).warning("Could not release stock for %s", product_id)
                    released = released and self.allow_offline
        return released
