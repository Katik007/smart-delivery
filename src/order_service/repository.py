from contextlib import nullcontext
from typing import Optional
from src.order_service.models import Order, OutboxMessage


class OrderRepository:
    """Репозиторий заказов с поддержкой паттерна Transaction Outbox."""

    def __init__(self, orders_db, outbox_db):
        self.orders_db = orders_db
        self.outbox_db = outbox_db

    def save_order_with_outbox(self, order: Order, outbox_message: Optional[OutboxMessage] = None) -> None:
        """
        Паттерн Transaction Outbox:
        1. Изменяем состояние заказа в базе данных (бизнес-логика).
        2. В рамках этой же транзакции записываем событие в таблицу outbox.
        3. Фиксируем транзакцию (атомарно).
        """
        storage = getattr(self.orders_db, "storage", None)
        with storage.transaction() if storage else nullcontext():
            self.orders_db[order.id] = order
            if outbox_message:
                self.outbox_db[outbox_message.id] = outbox_message
