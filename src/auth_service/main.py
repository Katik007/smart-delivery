from fastapi import FastAPI, HTTPException, status, Depends
from pydantic import BaseModel, EmailStr
from typing import Dict, Optional, List
from datetime import datetime, timedelta
import uuid

from src.auth_service.models import User, RefreshTokenRecord, UserAuditLog
from src.common.domain import UserRole
from src.common.security import (
    hash_password, verify_password,
    create_access_token, create_refresh_token,
    decode_token, get_current_user
)

app = FastAPI(title="SmartDelivery AuthService API", version="1.0.0")

# In-memory storage (DDD Repository Simulation)
users_db: Dict[str, User] = {}
refresh_tokens_db: Dict[str, RefreshTokenRecord] = {}
audit_logs_db: List[UserAuditLog] = []


class RegisterRequest(BaseModel):
    username: str
    email: str
    password: str
    role: UserRole = UserRole.CUSTOMER


class LoginRequest(BaseModel):
    username: str
    password: str


class RefreshRequest(BaseModel):
    refresh_token: str


class AuthResponse(BaseModel):
    access_token: str
    refresh_token: str
    token_type: str = "bearer"
    user_id: str
    username: str
    role: str


@app.get("/health")
def health_check():
    return {"status": "Healthy", "service": "AuthService", "timestamp": datetime.utcnow().isoformat()}


@app.post("/api/auth/register", response_model=AuthResponse, status_code=status.HTTP_201_CREATED)
def register(req: RegisterRequest):
    # Check if username or email already exists
    for u in users_db.values():
        if u.username == req.username:
            raise HTTPException(status_code=400, detail="Username already registered")
        if u.email == req.email:
            raise HTTPException(status_code=400, detail="Email already registered")

    user = User(
        username=req.username,
        email=req.email,
        password_hash=hash_password(req.password),
        role=req.role
    )
    users_db[user.id] = user

    # Create audit log
    audit_logs_db.append(UserAuditLog(
        user_id=user.id,
        action="REGISTER",
        ip_address="127.0.0.1"
    ))

    # Issue tokens
    access_token = create_access_token(user.id, user.username, user.role.value)
    refresh_token = create_refresh_token(user.id)

    refresh_tokens_db[refresh_token] = RefreshTokenRecord(
        token=refresh_token,
        user_id=user.id,
        expires_at=datetime.utcnow() + timedelta(days=7)
    )

    return AuthResponse(
        access_token=access_token,
        refresh_token=refresh_token,
        user_id=user.id,
        username=user.username,
        role=user.role.value
    )


@app.post("/api/auth/login", response_model=AuthResponse)
def login(req: LoginRequest):
    user = None
    for u in users_db.values():
        if u.username == req.username:
            user = u
            break

    if not user or not verify_password(req.password, user.password_hash):
        raise HTTPException(status_code=401, detail="Invalid username or password")

    access_token = create_access_token(user.id, user.username, user.role.value)
    refresh_token = create_refresh_token(user.id)

    refresh_tokens_db[refresh_token] = RefreshTokenRecord(
        token=refresh_token,
        user_id=user.id,
        expires_at=datetime.utcnow() + timedelta(days=7)
    )

    audit_logs_db.append(UserAuditLog(
        user_id=user.id,
        action="LOGIN_SUCCESS",
        ip_address="127.0.0.1"
    ))

    return AuthResponse(
        access_token=access_token,
        refresh_token=refresh_token,
        user_id=user.id,
        username=user.username,
        role=user.role.value
    )


@app.post("/api/auth/refresh")
def refresh_token(req: RefreshRequest):
    record = refresh_tokens_db.get(req.refresh_token)
    if not record or record.is_revoked or record.expires_at < datetime.utcnow():
        raise HTTPException(status_code=401, detail="Invalid or expired refresh token")

    user = users_db.get(record.user_id)
    if not user:
        raise HTTPException(status_code=404, detail="User not found")

    new_access_token = create_access_token(user.id, user.username, user.role.value)
    return {"access_token": new_access_token, "token_type": "bearer"}


@app.get("/api/auth/me")
def get_me(user: dict = Depends(get_current_user)):
    user_record = users_db.get(user["sub"])
    if not user_record:
        raise HTTPException(status_code=404, detail="User not found")
    return {
        "id": user_record.id,
        "username": user_record.username,
        "email": user_record.email,
        "role": user_record.role.value,
        "created_at": user_record.created_at.isoformat()
    }
