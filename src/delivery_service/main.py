from fastapi import FastAPI, HTTPException, status, Depends
from pydantic import BaseModel
from typing import Dict, List, Optional
from datetime import datetime
import os

from src.delivery_service.models import DeliveryTask, Courier, DeliveryRoute, InboxMessage
from src.delivery_service.consumer import OrderPaidConsumerWorker
from src.common.domain import DeliveryStatus, CourierStatus
from src.common.security import get_current_user, require_roles

app = FastAPI(title="SmartDelivery DeliveryService API", version="1.0.0")

# In-memory stores
tasks_db: Dict[str, DeliveryTask] = {}
couriers_db: Dict[str, Courier] = {}
routes_db: Dict[str, DeliveryRoute] = {}
inbox_db: Dict[str, InboxMessage] = {}

# Seed default couriers immediately
c1 = Courier(name="Иван Кузнецов", phone="+79991112233", vehicle_type="Scooter", status=CourierStatus.AVAILABLE)
c2 = Courier(name="Алексей Смирнов", phone="+79994445566", vehicle_type="Bicycle", status=CourierStatus.AVAILABLE)
couriers_db[c1.id] = c1
couriers_db[c2.id] = c2

consumer_worker = OrderPaidConsumerWorker(tasks_db, couriers_db, inbox_db)


@app.on_event("startup")
def on_startup():
    consumer_worker.start()


@app.on_event("shutdown")
def on_shutdown():
    consumer_worker.stop()


@app.get("/health")
def health_check():
    return {
        "status": "Healthy",
        "service": "DeliveryService",
        "total_tasks": len(tasks_db),
        "inbox_processed_events": len(inbox_db),
        "timestamp": datetime.utcnow().isoformat()
    }


@app.get("/api/delivery/tasks", response_model=List[DeliveryTask])
def get_delivery_tasks(user: dict = Depends(get_current_user)):
    return list(tasks_db.values())


@app.get("/api/delivery/couriers", response_model=List[Courier])
def get_couriers(user: dict = Depends(get_current_user)):
    return list(couriers_db.values())


class CreateCourierRequest(BaseModel):
    name: str
    phone: str
    vehicle_type: str = "Scooter"
    current_zone: str = "Central"


@app.post("/api/delivery/couriers", response_model=Courier, status_code=status.HTTP_201_CREATED)
def create_courier(req: CreateCourierRequest, user: dict = Depends(require_roles(["Manager", "Admin"]))):
    courier = Courier(
        name=req.name,
        phone=req.phone,
        vehicle_type=req.vehicle_type,
        current_zone=req.current_zone
    )
    couriers_db[courier.id] = courier
    return courier


class UpdateTaskStatusRequest(BaseModel):
    status: DeliveryStatus


@app.put("/api/delivery/tasks/{task_id}/status", response_model=DeliveryTask)
def update_task_status(task_id: str, req: UpdateTaskStatusRequest, user: dict = Depends(require_roles(["Courier", "Manager", "Admin"]))):
    task = tasks_db.get(task_id)
    if not task:
        raise HTTPException(status_code=404, detail="Delivery task not found")

    task.status = req.status
    if req.status == DeliveryStatus.DELIVERED:
        task.completed_at = datetime.utcnow()
        if task.courier_id and task.courier_id in couriers_db:
            couriers_db[task.courier_id].status = CourierStatus.AVAILABLE

    return task
