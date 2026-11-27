from fastapi import FastAPI, HTTPException, status, Depends
from pydantic import BaseModel, Field
from typing import List
from datetime import datetime
import os

from src.order_service.models import Order, OrderItem, Payment, OrderSagaState, OutboxMessage
from src.order_service.saga import OrderSagaOrchestrator
from src.order_service.outbox_worker import OutboxPublisherWorker
from src.common.domain import OrderStatus
from src.common.security import get_current_user
from src.common.storage import Storage

app = FastAPI(title="SmartDelivery OrderService API", version="1.0.0")

storage = Storage("orders")
orders_db = storage.collection("orders", Order)
payments_db = storage.collection("payments", Payment)
saga_db = storage.collection("saga_states", OrderSagaState)
outbox_db = storage.collection("outbox_messages", OutboxMessage)

grpc_host = os.getenv("CATALOG_GRPC_HOST", "localhost:50051")
saga_orchestrator = OrderSagaOrchestrator(
    orders_db, payments_db, saga_db, outbox_db, grpc_target=grpc_host, allow_offline=not storage.dsn
)
outbox_worker = OutboxPublisherWorker(outbox_db)


@app.on_event("startup")
def on_startup():
    storage.initialize()
    outbox_worker.start()


@app.on_event("shutdown")
def on_shutdown():
    outbox_worker.stop()


@app.get("/health")
def health_check():
    return {
        "status": "Healthy",
        "service": "OrderService",
        "pending_outbox_messages": len([m for m in outbox_db.values() if m.status == "PENDING"]),
        "timestamp": datetime.utcnow().isoformat()
    }


class OrderItemInput(BaseModel):
    product_id: str
    product_name: str
    quantity: int = Field(gt=0, le=2147483647)
    unit_price: float = Field(gt=0, allow_inf_nan=False)


class CreateOrderRequest(BaseModel):
    delivery_address: str
    items: List[OrderItemInput] = Field(min_length=1)
    payment_card: str = "4111222233334444"  # Use '0000' to simulate payment failure & compensation


@app.post("/api/orders", status_code=status.HTTP_201_CREATED)
def create_order(req: CreateOrderRequest, user: dict = Depends(get_current_user)):
    total = sum(i.quantity * i.unit_price for i in req.items)
    order = Order(
        customer_id=user["sub"],
        delivery_address=req.delivery_address,
        total_amount=total,
        status=OrderStatus.CREATED
    )

    for item in req.items:
        order.items.append(OrderItem(
            order_id=order.id,
            product_id=item.product_id,
            product_name=item.product_name,
            quantity=item.quantity,
            unit_price=item.unit_price
        ))

    card_valid = (req.payment_card != "0000")
    with storage.transaction():
        success, message = saga_orchestrator.execute_saga(order, payment_card_valid=card_valid)

    if not success:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Order Saga failed: {message}"
        )

    return {
        "order_id": order.id,
        "status": order.status.value,
        "total_amount": order.total_amount,
        "message": message
    }


@app.get("/api/orders", response_model=List[Order])
def list_orders(user: dict = Depends(get_current_user)):
    return [o for o in orders_db.values() if o.customer_id == user["sub"] or user.get("role") in ["Manager", "Admin"]]


@app.get("/api/orders/{order_id}", response_model=Order)
def get_order(order_id: str, user: dict = Depends(get_current_user)):
    order = orders_db.get(order_id)
    if not order or (order.customer_id != user["sub"] and user.get("role") not in ("Manager", "Admin")):
        raise HTTPException(status_code=404, detail="Order not found")
    return order


@app.get("/api/orders/saga/{order_id}")
def get_saga_status(order_id: str, user: dict = Depends(get_current_user)):
    get_order(order_id, user)
    saga = next((s for s in saga_db.values() if s.order_id == order_id), None)
    if not saga:
        raise HTTPException(status_code=404, detail="Saga state not found")
    return saga
