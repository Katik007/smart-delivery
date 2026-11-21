from datetime import datetime
from enum import Enum
from typing import List, Optional, Any
from pydantic import BaseModel, Field
import uuid


class UserRole(str, Enum):
    CUSTOMER = "Customer"
    COURIER = "Courier"
    MANAGER = "Manager"
    ADMIN = "Admin"


class OrderStatus(str, Enum):
    CREATED = "Created"
    STOCK_RESERVED = "StockReserved"
    PAID = "Paid"
    CANCELLED = "Cancelled"
    DELIVERED = "Delivered"


class SagaStepStatus(str, Enum):
    STARTED = "Started"
    STOCK_RESERVED = "StockReserved"
    PAID = "Paid"
    COMPENSATED = "Compensated"
    FAILED = "Failed"


class DeliveryStatus(str, Enum):
    PENDING = "Pending"
    ASSIGNED = "Assigned"
    IN_TRANSIT = "InTransit"
    DELIVERED = "Delivered"
    FAILED = "Failed"


class CourierStatus(str, Enum):
    AVAILABLE = "Available"
    BUSY = "Busy"
    OFFLINE = "Offline"


# Integration Events (for RabbitMQ)
class OrderPaidIntegrationEvent(BaseModel):
    event_id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    order_id: str
    customer_id: str
    delivery_address: str
    total_amount: float
    items: List[dict]
    occurred_at: str = Field(default_factory=lambda: datetime.utcnow().isoformat())
