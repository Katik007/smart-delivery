from fastapi.testclient import TestClient
from src.order_service.main import app as order_app
from src.delivery_service.main import app as delivery_app
from src.gateway.main import app as gateway_app
from src.common.security import create_access_token

order_client = TestClient(order_app)
delivery_client = TestClient(delivery_app)
gateway_client = TestClient(gateway_app)


def test_order_creation_and_saga_api():
    cust_token = create_access_token("cust-100", "test_customer", "Customer")

    # Place order
    resp = order_client.post(
        "/api/orders",
        headers={"Authorization": f"Bearer {cust_token}"},
        json={
            "delivery_address": "г. Москва, ул. Тверская, 1",
            "items": [
                {
                    "product_id": "prod-101",
                    "product_name": "Наушники Pro",
                    "quantity": 1,
                    "unit_price": 4990.0
                }
            ],
            "payment_card": "4111222233334444"
        }
    )
    assert resp.status_code == 201
    data = resp.json()
    assert data["status"] == "Paid"
    order_id = data["order_id"]

    # Check saga status endpoint
    saga_resp = order_client.get(f"/api/orders/saga/{order_id}")
    assert saga_resp.status_code == 200
    assert saga_resp.json()["current_step"] == "Paid"


def test_delivery_tasks_and_couriers_api():
    mgr_token = create_access_token("mgr-1", "manager_user", "Manager")

    # Get couriers
    c_resp = delivery_client.get("/api/delivery/couriers", headers={"Authorization": f"Bearer {mgr_token}"})
    assert c_resp.status_code == 200
    assert len(c_resp.json()) >= 2


def test_gateway_health_and_root():
    h_resp = gateway_client.get("/health")
    assert h_resp.status_code == 200
    assert h_resp.json()["status"] == "Healthy"

    root_resp = gateway_client.get("/")
    assert root_resp.status_code == 200
    assert "SmartDelivery Microservices" in root_resp.text
