import os
import json
import threading
import pika
from typing import Dict
from src.delivery_service.models import DeliveryTask, Courier, InboxMessage
from src.common.domain import DeliveryStatus, CourierStatus


class OrderPaidConsumerWorker:
    def __init__(self, tasks_db: Dict[str, DeliveryTask], couriers_db: Dict[str, Courier], inbox_db: Dict[str, InboxMessage], rabbitmq_url: str = None):
        self.tasks_db = tasks_db
        self.couriers_db = couriers_db
        self.inbox_db = inbox_db
        self.rabbitmq_url = rabbitmq_url or os.getenv("RABBITMQ_URL", "amqp://guest:guest@localhost:5672/")
        self._running = False
        self._thread = None

    def start(self):
        self._running = True
        self._thread = threading.Thread(target=self._run_loop, daemon=True)
        self._thread.start()

    def stop(self):
        self._running = False

    def process_order_paid_event(self, event_data: dict) -> bool:
        """Transaction Inbox implementation: deduplicates events and processes new ones idempotently."""
        event_id = event_data.get("event_id")
        if not event_id:
            return False

        # Check if already processed (Idempotency guarantee)
        if event_id in self.inbox_db:
            return False  # Duplicate ignored

        # Record into Transaction Inbox
        self.inbox_db[event_id] = InboxMessage(
            event_id=event_id,
            event_type="OrderPaid"
        )

        # Create delivery task
        order_id = event_data.get("order_id")
        address = event_data.get("delivery_address", "Адрес не указан")
        task = DeliveryTask(
            order_id=order_id,
            delivery_address=address,
            status=DeliveryStatus.PENDING
        )

        # Try to find available courier
        available_courier = next((c for c in self.couriers_db.values() if c.status == CourierStatus.AVAILABLE), None)
        if available_courier:
            task.assign_to_courier(available_courier)

        self.tasks_db[task.id] = task
        return True

    def _run_loop(self):
        while self._running:
            try:
                params = pika.URLParameters(self.rabbitmq_url)
                connection = pika.BlockingConnection(params)
                channel = connection.channel()

                channel.exchange_declare(exchange="smartdelivery.exchange", exchange_type="topic", durable=True)
                channel.queue_declare(queue="smartdelivery.delivery.orderpaid", durable=True)
                channel.queue_bind(
                    exchange="smartdelivery.exchange",
                    queue="smartdelivery.delivery.orderpaid",
                    routing_key="order.paid"
                )

                def callback(ch, method, properties, body):
                    try:
                        data = json.loads(body.decode("utf-8"))
                        self.process_order_paid_event(data)
                        ch.basic_ack(delivery_tag=method.delivery_tag)
                    except Exception:
                        ch.basic_nack(delivery_tag=method.delivery_tag, requeue=True)

                channel.basic_consume(queue="smartdelivery.delivery.orderpaid", on_message_callback=callback)
                channel.start_consuming()
            except Exception:
                pass
