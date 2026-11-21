from fastapi.testclient import TestClient
from src.auth_service.main import app, users_db

client = TestClient(app)


def test_auth_register_and_login_flow():
    # 1. Register
    reg_resp = client.post("/api/auth/register", json={
        "username": "ekaterina",
        "email": "ekaterina@test.ru",
        "password": "Password123!",
        "role": "Customer"
    })
    assert reg_resp.status_code == 201
    data = reg_resp.json()
    assert "access_token" in data
    assert data["username"] == "ekaterina"

    # 2. Login
    login_resp = client.post("/api/auth/login", json={
        "username": "ekaterina",
        "password": "Password123!"
    })
    assert login_resp.status_code == 200
    login_data = login_resp.json()
    token = login_data["access_token"]
    refresh = login_data["refresh_token"]

    # 3. Refresh token
    refresh_resp = client.post("/api/auth/refresh", json={"refresh_token": refresh})
    assert refresh_resp.status_code == 200
    assert "access_token" in refresh_resp.json()

    # 4. Protected endpoint /me
    me_resp = client.get("/api/auth/me", headers={"Authorization": f"Bearer {token}"})
    assert me_resp.status_code == 200
    assert me_resp.json()["username"] == "ekaterina"
