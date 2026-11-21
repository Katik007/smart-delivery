import pytest
from src.delivery_service.consumer import OrderPaidConsumerWorker
from src.delivery_service.models import Courier
from src.common.domain import CourierStatus


def test_transaction_inbox_deduplication():
    tasks_db = {}
    couriers_db = {
        "c1": Courier(id="c1", name="Иван", phone="123", status=CourierStatus.AVAILABLE)
    }
    inbox_db = {}

    worker = OrderPaidConsumerWorker(tasks_db, couriers_db, inbox_db)

    event_payload = {
        "event_id": "evt-unique-999",
        "order_id": "ord-777",
        "delivery_address": "г. Москва, ул. Арбат, 15",
        "total_amount": 2500.0
    }

    # First receive -> Should process and create task
    processed_1 = worker.process_order_paid_event(event_payload)
    assert processed_1 is True
    assert len(tasks_db) == 1
    assert "evt-unique-999" in inbox_db

    # Duplicate receive with SAME event_id -> Should be ignored!
    processed_2 = worker.process_order_paid_event(event_payload)
    assert processed_2 is False
    assert len(tasks_db) == 1  # Still only 1 task created, no double delivery!
