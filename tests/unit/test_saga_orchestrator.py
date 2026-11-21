import pytest
from src.order_service.models import Order, OrderItem
from src.order_service.saga import OrderSagaOrchestrator
from src.common.domain import OrderStatus, SagaStepStatus


def test_saga_successful_execution():
    orders_db = {}
    payments_db = {}
    saga_db = {}
    outbox_db = {}

    orchestrator = OrderSagaOrchestrator(orders_db, payments_db, saga_db, outbox_db)

    order = Order(customer_id="cust-1", delivery_address="Test Address", total_amount=1500.0)
    order.items.append(OrderItem(order_id=order.id, product_id="prod-101", product_name="Test Product", quantity=2, unit_price=750.0))
    orders_db[order.id] = order

    success, msg = orchestrator.execute_saga(order, payment_card_valid=True)

    assert success is True
    assert order.status == OrderStatus.PAID
    assert len(payments_db) == 1
    assert list(payments_db.values())[0].status == "Succeeded"
    assert len(outbox_db) == 1
    assert list(outbox_db.values())[0].event_type == "OrderPaid"


def test_saga_compensation_on_payment_failure():
    orders_db = {}
    payments_db = {}
    saga_db = {}
    outbox_db = {}

    orchestrator = OrderSagaOrchestrator(orders_db, payments_db, saga_db, outbox_db)

    order = Order(customer_id="cust-2", delivery_address="Test Address", total_amount=3000.0)
    order.items.append(OrderItem(order_id=order.id, product_id="prod-102", product_name="Test Product 2", quantity=1, unit_price=3000.0))
    orders_db[order.id] = order

    # Simulate payment rejection
    success, msg = orchestrator.execute_saga(order, payment_card_valid=False)

    assert success is False
    assert order.status == OrderStatus.CANCELLED
    assert len(payments_db) == 1
    assert list(payments_db.values())[0].status == "Failed"
    # Verify saga state is COMPENSATED
    saga = list(saga_db.values())[0]
    assert saga.current_step == SagaStepStatus.COMPENSATED
    assert saga.is_compensated is True
    # Outbox should NOT have an event since payment failed
    assert len(outbox_db) == 0
