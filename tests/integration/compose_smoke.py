"""Exercise the running Docker Compose stack and its durable data."""

import subprocess
import time
import uuid

import httpx


def compose(*args):
    return subprocess.run(["docker", "compose", *args], check=True, capture_output=True, text=True).stdout.strip()


def get(client, path):
    return client.get(path).raise_for_status().json()


def main():
    with httpx.Client(base_url="http://127.0.0.1:8080", timeout=15) as client:
        assert "SmartDelivery Microservices" in client.get("/").raise_for_status().text
        assert get(client, "/api/catalog/health")["redis_connected"]

        product = get(client, "/api/catalog/products/prod-101")
        assert compose("exec", "-T", "redis", "redis-cli", "EXISTS", f"product:{product['id']}") == "1"

        username = f"smoke_{uuid.uuid4().hex}"
        registration = {"username": username, "email": f"{username}@example.com", "password": uuid.uuid4().hex}
        assert client.post("/api/auth/register", json={**registration, "role": "Admin"}).status_code == 403
        auth = client.post("/api/auth/register", json=registration).raise_for_status().json()
        assert client.get("/api/auth/me", headers={"Authorization": f"Bearer {auth['refresh_token']}"}).status_code == 401
        client.headers["Authorization"] = f"Bearer {auth['access_token']}"
        assert get(client, "/api/auth/me")["username"] == username

        inventory_path = f"/api/catalog/inventory/{product['id']}"
        before = get(client, inventory_path)["available_quantity"]
        order = {
            "delivery_address": "Учебный адрес",
            "items": [{"product_id": product["id"], "product_name": product["name"],
                       "quantity": 1, "unit_price": product["price"]}],
        }
        declined = client.post("/api/orders", json={**order, "payment_card": "0000"})
        assert declined.status_code == 400 and "rolled back" in declined.json()["detail"]
        assert get(client, inventory_path)["available_quantity"] == before

        paid = client.post("/api/orders", json=order).raise_for_status().json()
        order_id = paid["order_id"]
        assert paid["status"] == "Paid"
        assert get(client, inventory_path)["available_quantity"] == before - 1
        assert get(client, f"/api/orders/saga/{order_id}")["current_step"] == "Paid"

        deadline = time.monotonic() + 35
        while time.monotonic() < deadline:
            if any(task["order_id"] == order_id for task in get(client, "/api/delivery/tasks")):
                break
            time.sleep(1)
        else:
            raise AssertionError("The OrderPaid event did not create a delivery task")

        compose("restart", "auth-service", "catalog-service", "order-service", "delivery-service")
        compose("up", "--detach", "--wait", "--wait-timeout", "90")
        assert get(client, "/api/auth/me")["username"] == username
        assert get(client, inventory_path)["available_quantity"] == before - 1
        assert get(client, f"/api/orders/{order_id}")["status"] == "Paid"
        assert sum(task["order_id"] == order_id for task in get(client, "/api/delivery/tasks")) == 1
        print("JWT, Redis, PostgreSQL persistence, gRPC compensation and RabbitMQ delivery verified.")


if __name__ == "__main__":
    main()
