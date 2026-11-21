from typing import Optional
from pydantic import BaseModel, Field
import uuid


class Category(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    name: str
    description: Optional[str] = None


class Product(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    name: str
    description: Optional[str] = None
    price: float
    category_id: str
    is_active: bool = True


class Warehouse(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    code: str
    name: str
    address: str


class InventoryItem(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid.uuid4()))
    product_id: str
    warehouse_id: str
    available_quantity: int
    reserved_quantity: int = 0

    # DDD Domain Invariants
    def try_reserve(self, quantity: int) -> bool:
        """Domain Invariant: Cannot reserve more than available quantity."""
        if quantity <= 0:
            return False
        if self.available_quantity < quantity:
            return False
        self.available_quantity -= quantity
        self.reserved_quantity += quantity
        return True

    def release(self, quantity: int) -> None:
        """Domain Invariant: Release reserved quantity back to available stock."""
        if quantity <= 0:
            return
        to_release = min(self.reserved_quantity, quantity)
        self.reserved_quantity -= to_release
        self.available_quantity += to_release
