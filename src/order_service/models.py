from datetime import datetime
from typing import List, Optional
from pydantic import BaseModel, Field
import uuid
from src.common.domain import OrderStatus, SagaStepStatus


class OrderItem(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    order_id: str
    product_id: str
    product_name: str
    quantity: int
    unit_price: float


class Order(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    customer_id: str
    delivery_address: str
    total_amount: float
    status: OrderStatus = OrderStatus.CREATED
    created_at: datetime = Field(default_factory=datetime.utcnow)
    items: List[OrderItem] = []

    def can_cancel(self) -> bool:
        return self.status in [OrderStatus.CREATED, OrderStatus.STOCK_RESERVED]


class Payment(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    order_id: str
    amount: float
    status: str  # Pending, Succeeded, Failed
    payment_method: str = "CreditCard"
    created_at: datetime = Field(default_factory=datetime.utcnow)


class OrderSagaState(BaseModel):
    saga_id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    order_id: str
    current_step: SagaStepStatus = SagaStepStatus.STARTED
    is_compensated: bool = False
    error_message: Optional[str] = None
    updated_at: datetime = Field(default_factory=datetime.utcnow)


class OutboxMessage(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    event_type: str
    payload: str  # JSON payload
    status: str = "PENDING"  # PENDING, PUBLISHED, FAILED
    created_at: datetime = Field(default_factory=datetime.utcnow)
