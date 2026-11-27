from fastapi import FastAPI, HTTPException, status, Depends
from pydantic import BaseModel, Field
from typing import List, Optional
from datetime import datetime
import os

from src.catalog_service.models import Product, Category, InventoryItem, Warehouse
from src.catalog_service.cache import CatalogCacheService
from src.catalog_service.grpc_server import start_grpc_server
from src.common.security import get_current_user, require_roles
from src.common.storage import Storage

app = FastAPI(title="SmartDelivery CatalogService API", version="1.0.0")

storage = Storage("catalog")
categories_db = storage.collection("categories", Category)
products_db = storage.collection("products", Product)
warehouses_db = storage.collection("warehouses", Warehouse)
inventory_db = storage.collection("inventory", InventoryItem)

cache_service = CatalogCacheService()
grpc_server_instance = None


# Seed initial data immediately
def seed_data():
    if not warehouses_db:
        w = Warehouse(code="WH-MAIN", name="Центральный склад", address="г. Москва, ул. Складская, 10")
        warehouses_db[w.id] = w

        c = Category(name="Электроника", description="Гаджеты и аксессуары")
        categories_db[c.id] = c

        p1 = Product(id="prod-101", name="Беспроводные наушники Pro", description="ANC, Bluetooth 5.3", price=4990.0, category_id=c.id)
        p2 = Product(id="prod-102", name="Внешний аккумулятор 20000mAh", description="Быстрая зарядка 65W", price=2990.0, category_id=c.id)
        products_db[p1.id] = p1
        products_db[p2.id] = p2

        inventory_db[p1.id] = InventoryItem(product_id=p1.id, warehouse_id=w.id, available_quantity=25)
        inventory_db[p2.id] = InventoryItem(product_id=p2.id, warehouse_id=w.id, available_quantity=40)

if not storage.dsn:
    seed_data()


@app.on_event("startup")
def on_startup():
    global grpc_server_instance
    storage.initialize()
    with storage.transaction(lock=True):
        seed_data()
    grpc_port = int(os.getenv("GRPC_PORT", "50051"))
    grpc_server_instance = start_grpc_server(inventory_db, storage, port=grpc_port)


@app.on_event("shutdown")
def on_shutdown():
    global grpc_server_instance
    if grpc_server_instance:
        grpc_server_instance.stop(0)


@app.get("/health")
def health_check():
    return {
        "status": "Healthy",
        "service": "CatalogService",
        "redis_connected": cache_service.is_connected,
        "timestamp": datetime.utcnow().isoformat()
    }


@app.get("/api/catalog/categories", response_model=List[Category])
def get_categories():
    return list(categories_db.values())


@app.get("/api/catalog/products", response_model=List[Product])
def get_products():
    return list(products_db.values())


@app.get("/api/catalog/products/{product_id}", response_model=Product)
def get_product_by_id(product_id: str):
    # Pattern: Cache-Aside with Redis
    cache_key = f"product:{product_id}"
    cached = cache_service.get(cache_key)
    if cached:
        return Product(**cached)

    product = products_db.get(product_id)
    if not product:
        raise HTTPException(status_code=404, detail="Product not found")

    cache_service.set(cache_key, product.model_dump(), ttl_seconds=600)
    return product


class CreateProductRequest(BaseModel):
    name: str
    description: Optional[str] = None
    price: float = Field(gt=0, allow_inf_nan=False)
    category_id: str
    initial_quantity: int = Field(default=10, ge=0)


@app.post("/api/catalog/products", response_model=Product, status_code=status.HTTP_201_CREATED)
def create_product(req: CreateProductRequest, user: dict = Depends(require_roles(["Manager", "Admin"]))):
    product = Product(
        name=req.name,
        description=req.description,
        price=req.price,
        category_id=req.category_id
    )
    with storage.transaction(lock=True):
        if req.category_id not in categories_db:
            raise HTTPException(status_code=404, detail="Category not found")
        warehouse = next(iter(warehouses_db.values()))
        products_db[product.id] = product
        inventory_db[product.id] = InventoryItem(
            product_id=product.id,
            warehouse_id=warehouse.id,
            available_quantity=req.initial_quantity
        )

    # Invalidate cache
    cache_service.invalidate(f"product:{product.id}")
    return product


@app.get("/api/catalog/inventory/{product_id}")
def get_inventory(product_id: str):
    item = inventory_db.get(product_id)
    if not item:
        raise HTTPException(status_code=404, detail="Inventory record not found")
    return {
        "product_id": item.product_id,
        "available_quantity": item.available_quantity,
        "reserved_quantity": item.reserved_quantity
    }
