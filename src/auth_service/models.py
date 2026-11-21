from datetime import datetime
from typing import Optional
from pydantic import BaseModel, Field
import uuid
from src.common.domain import UserRole


class User(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    username: str
    email: str
    password_hash: str
    role: UserRole = UserRole.CUSTOMER
    created_at: datetime = Field(default_factory=datetime.utcnow)


class RefreshTokenRecord(BaseModel):
    token: str
    user_id: str
    expires_at: datetime
    is_revoked: bool = False


class UserAuditLog(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    user_id: str
    action: str
    ip_address: str
    created_at: datetime = Field(default_factory=datetime.utcnow)
