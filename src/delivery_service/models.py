from datetime import datetime
from typing import List, Optional
from pydantic import BaseModel, Field
import uuid
from src.common.domain import DeliveryStatus, CourierStatus


class Courier(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    name: str
    phone: str
    vehicle_type: str = "Scooter"  # Foot, Bicycle, Scooter, Car
    status: CourierStatus = CourierStatus.AVAILABLE
    current_zone: str = "Central"


class RouteStop(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    address: str
    is_completed: bool = False


class DeliveryRoute(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    courier_id: str
    date: str = Field(default_factory=lambda: datetime.utcnow().strftime("%Y-%m-%d"))
    stops: List[RouteStop] = []


class DeliveryTask(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    order_id: str
    customer_id: Optional[str] = None
    courier_id: Optional[str] = None
    delivery_address: str
    status: DeliveryStatus = DeliveryStatus.PENDING
    assigned_at: Optional[datetime] = None
    completed_at: Optional[datetime] = None
    created_at: datetime = Field(default_factory=datetime.utcnow)

    def assign_to_courier(self, courier: Courier):
        self.courier_id = courier.id
        self.status = DeliveryStatus.ASSIGNED
        self.assigned_at = datetime.utcnow()
        courier.status = CourierStatus.BUSY


class InboxMessage(BaseModel):
    event_id: str
    event_type: str
    received_at: datetime = Field(default_factory=datetime.utcnow)
    is_processed: bool = True
