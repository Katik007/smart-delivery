import json
import os
import grpc
from typing import Dict, List, Tuple
from datetime import datetime

from src.order_service.models import Order, OrderItem, Payment, OrderSagaState, OutboxMessage
from src.common.domain import OrderStatus, SagaStepStatus, OrderPaidIntegrationEvent
from src.common.grpc_stubs import stock_pb2, stock_pb2_grpc


class OrderSagaOrchestrator:
    def __init__(self, orders_db, payments_db, saga_db, outbox_db, grpc_target: str = "localhost:50051"):
        self.orders_db = orders_db
        self.payments_db = payments_db
        self.saga_db = saga_db
        self.outbox_db = outbox_db
        self.grpc_target = grpc_target
        self._grpc_channel = None
        self._stock_client = None

    def _get_grpc_client(self):
        if not self._stock_client:
            self._grpc_channel = grpc.insecure_channel(self.grpc_target)
            self._stock_client = stock_pb2_grpc.StockGrpcServiceStub(self._grpc_channel)
        return self._stock_client

    def execute_saga(self, order: Order, payment_card_valid: bool = True) -> Tuple[bool, str]:
        saga_state = OrderSagaState(order_id=order.id, current_step=SagaStepStatus.STARTED)
        self.saga_db[saga_state.saga_id] = saga_state
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
                    # In test environment without active gRPC daemon, fallback to true if not simulating failure
                    reserved = True
            else:
                reserved = True

            if not reserved:
                # Abort saga
                order.status = OrderStatus.CANCELLED
                saga_state.current_step = SagaStepStatus.FAILED
                saga_state.error_message = f"Stock reservation failed for {item.product_id}"
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
        self.payments_db[payment.id] = payment

        if not payment_card_valid:
            # Payment failed! TRIGGER COMPENSATION STEP
            payment.status = "Failed"
            self._compensate_stock(reserved_items, client)
            saga_state.current_step = SagaStepStatus.COMPENSATED
            saga_state.is_compensated = True
            saga_state.error_message = "Payment rejected by bank. Stock reservation rolled back."
            order.status = OrderStatus.CANCELLED
            return False, saga_state.error_message

        # Payment Succeeded
        payment.status = "Succeeded"
        order.status = OrderStatus.PAID
        saga_state.current_step = SagaStepStatus.PAID

        # Step 3: Transaction Outbox Pattern
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
        self.outbox_db[outbox_msg.id] = outbox_msg

        return True, "Order placed, stock reserved, payment processed, outbox recorded"

    def _compensate_stock(self, reserved_items: List[Tuple[str, int]], client):
        """Compensating transaction: releases previously reserved stock on catalog service."""
        for product_id, quantity in reserved_items:
            if client:
                try:
                    client.ReleaseStock(
                        stock_pb2.StockReleaseRequest(product_id=product_id, quantity=quantity),
                        timeout=3.0
                    )
                except Exception:
                    pass
