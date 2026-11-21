import time
import os
import threading
import json
import pika


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
            self.publish_pending_messages()
            time.sleep(5)

    def publish_pending_messages(self):
        pending = [m for m in self.outbox_db.values() if m.status == "PENDING"]
        if not pending:
            return

        try:
            params = pika.URLParameters(self.rabbitmq_url)
            connection = pika.BlockingConnection(params)
            channel = connection.channel()
            channel.exchange_declare(exchange="smartdelivery.exchange", exchange_type="topic", durable=True)

            for msg in pending:
                channel.basic_publish(
                    exchange="smartdelivery.exchange",
                    routing_key="order.paid",
                    body=msg.payload,
                    properties=pika.BasicProperties(delivery_mode=2)
                )
                msg.status = "PUBLISHED"

            connection.close()
        except Exception:
            # If RabbitMQ is temporarily unreachable, messages remain PENDING (guaranteed delivery)
            pass
