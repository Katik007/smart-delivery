from fastapi.testclient import TestClient
from src.catalog_service.main import app
from src.common.security import create_access_token

client = TestClient(app)


def test_catalog_endpoints():
    # 1. Health
    h_resp = client.get("/health")
    assert h_resp.status_code == 200
    assert h_resp.json()["status"] == "Healthy"

    # 2. Categories
    cat_resp = client.get("/api/catalog/categories")
    assert cat_resp.status_code == 200
    assert len(cat_resp.json()) >= 1

    # 3. Products
    prod_resp = client.get("/api/catalog/products")
    assert prod_resp.status_code == 200
    products = prod_resp.json()
    assert len(products) >= 2

    first_prod_id = products[0]["id"]

    # 4. Product by ID (tests Cache-Aside)
    p_resp = client.get(f"/api/catalog/products/{first_prod_id}")
    assert p_resp.status_code == 200
    assert p_resp.json()["id"] == first_prod_id

    # 5. Create product with Manager role
    mgr_token = create_access_token("mgr-1", "manager_user", "Manager")
    new_prod_resp = client.post(
        "/api/catalog/products",
        headers={"Authorization": f"Bearer {mgr_token}"},
        json={
            "name": "Тестовый товар",
            "description": "Описание",
            "price": 1200.0,
            "category_id": products[0]["category_id"],
            "initial_quantity": 15
        }
    )
    assert new_prod_resp.status_code == 201
    created_id = new_prod_resp.json()["id"]

    # Check inventory was initialized
    inv_resp = client.get(f"/api/catalog/inventory/{created_id}")
    assert inv_resp.status_code == 200
    assert inv_resp.json()["available_quantity"] == 15
