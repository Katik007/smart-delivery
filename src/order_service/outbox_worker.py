import time
import os
import threading
import logging
import pika
from src.common.messaging import EXCHANGE, ORDER_PAID_KEY, declare_order_paid_queue


class OutboxPublisherWorker:
    def __init__(self, outbox_db, rabbitmq_url: str = None):
        self.outbox_db = outbox_db
        self.rabbitmq_url = rabbitmq_url or os.getenv("RABBITMQ_URL", "amqp://guest:guest@localhost:5672/")
        self._running = False
        self._thread = None

    def start(self):
        self._running = True
        self._thread = threading.Thread(target=self._run_loop, daemon=True)
        self._thread.start()

    def stop(self):
        self._running = False

    def _run_loop(self):
        while self._running:
            try:
                self.publish_pending_messages()
            except Exception:
                logging.getLogger(__name__).exception("Outbox publication failed; will retry")
            time.sleep(5)

    def publish_pending_messages(self):
        pending = [m for m in self.outbox_db.values() if m.status == "PENDING"]
        if not pending:
            return

        params = pika.URLParameters(self.rabbitmq_url)
        params.blocked_connection_timeout = 5
        with pika.BlockingConnection(params) as connection:
            channel = connection.channel()
            declare_order_paid_queue(channel)
            channel.confirm_delivery()

            for msg in pending:
                channel.basic_publish(
                    exchange=EXCHANGE,
                    routing_key=ORDER_PAID_KEY,
                    body=msg.payload,
                    properties=pika.BasicProperties(delivery_mode=2, message_id=msg.id),
                    mandatory=True
                )
                msg.status = "PUBLISHED"

                self.outbox_db[msg.id] = msg
