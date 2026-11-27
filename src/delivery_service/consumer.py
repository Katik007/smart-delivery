import os
import json
import logging
import threading
from contextlib import nullcontext
import pika
from src.delivery_service.models import DeliveryTask, InboxMessage
from src.common.domain import DeliveryStatus, CourierStatus
from src.common.messaging import ORDER_PAID_QUEUE, declare_order_paid_queue


class OrderPaidConsumerWorker:
    def __init__(self, tasks_db, couriers_db, inbox_db, rabbitmq_url=None, *, storage=None):
        self.tasks_db = tasks_db
        self.couriers_db = couriers_db
        self.inbox_db = inbox_db
        self.rabbitmq_url = rabbitmq_url or os.getenv("RABBITMQ_URL", "amqp://guest:guest@localhost:5672/")
        self.storage = storage
        self._stop = threading.Event()
        self._thread = None

    def start(self):
        self._stop.clear()
        self._thread = threading.Thread(target=self._run_loop, daemon=True)
        self._thread.start()

    def stop(self):
        self._stop.set()

    def process_order_paid_event(self, event_data: dict) -> bool:
        """Transaction Inbox implementation: deduplicates events and processes new ones idempotently."""
        event_id = event_data.get("event_id")
        if not event_id:
            return False

        with self.storage.transaction(lock=True) if self.storage else nullcontext():
            if event_id in self.inbox_db:
                return False
            task = DeliveryTask(
                order_id=event_data.get("order_id"),
                customer_id=event_data.get("customer_id"),
                delivery_address=event_data.get("delivery_address", "Адрес не указан"),
                status=DeliveryStatus.PENDING
            )
            courier = next((c for c in self.couriers_db.values() if c.status == CourierStatus.AVAILABLE), None)
            if courier:
                task.assign_to_courier(courier)
                self.couriers_db[courier.id] = courier
            self.tasks_db[task.id] = task
            self.inbox_db[event_id] = InboxMessage(event_id=event_id, event_type="OrderPaid")
            return True

    def _run_loop(self):
        while not self._stop.is_set():
            try:
                params = pika.URLParameters(self.rabbitmq_url)
                with pika.BlockingConnection(params) as connection:
                    channel = connection.channel()
                    declare_order_paid_queue(channel)
                    channel.basic_qos(prefetch_count=1)
                    for method, _, body in channel.consume(ORDER_PAID_QUEUE, inactivity_timeout=1):
                        if self._stop.is_set():
                            break
                        if method is None:
                            continue
                        try:
                            data = json.loads(body)
                            if not isinstance(data, dict) or not data.get("event_id"):
                                raise ValueError("Missing event ID")
                            self.process_order_paid_event(data)
                        except ValueError:
                            logging.getLogger(__name__).warning("Invalid OrderPaid event; discarded")
                            channel.basic_nack(delivery_tag=method.delivery_tag, requeue=False)
                        else:
                            channel.basic_ack(delivery_tag=method.delivery_tag)
            except Exception:
                logging.getLogger(__name__).exception("Delivery consumer disconnected; will retry")
                self._stop.wait(3)
