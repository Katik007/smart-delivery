from fastapi import FastAPI, HTTPException, status, Depends
from pydantic import BaseModel
from typing import Dict, List, Optional
from datetime import datetime
import os

from src.catalog_service.models import Product, Category, InventoryItem, Warehouse
from src.catalog_service.cache import CatalogCacheService
from src.catalog_service.grpc_server import start_grpc_server
from src.common.security import get_current_user, require_roles

app = FastAPI(title="SmartDelivery CatalogService API", version="1.0.0")

# In-memory stores
categories_db: Dict[str, Category] = {}
products_db: Dict[str, Product] = {}
warehouses_db: Dict[str, Warehouse] = {}
inventory_db: Dict[str, InventoryItem] = {}  # key: product_id

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

seed_data()


@app.on_event("startup")
def on_startup():
    global grpc_server_instance
    # Start gRPC server in background
    grpc_port = int(os.getenv("GRPC_PORT", "50051"))
    try:
        grpc_server_instance = start_grpc_server(inventory_db, port=grpc_port)
    except Exception as e:
        print(f"Warning: could not bind gRPC port {grpc_port}: {e}")


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

    cache_service.set(cache_key, product.dict(), ttl_seconds=600)
    return product


class CreateProductRequest(BaseModel):
    name: str
    description: Optional[str] = None
    price: float
    category_id: str
    initial_quantity: int = 10


@app.post("/api/catalog/products", response_model=Product, status_code=status.HTTP_201_CREATED)
def create_product(req: CreateProductRequest, user: dict = Depends(require_roles(["Manager", "Admin"]))):
    product = Product(
        name=req.name,
        description=req.description,
        price=req.price,
        category_id=req.category_id
    )
    products_db[product.id] = product

    # Create inventory item
    warehouse = list(warehouses_db.values())[0] if warehouses_db else None
    wh_id = warehouse.id if warehouse else "WH-MAIN"
    inventory_db[product.id] = InventoryItem(
        product_id=product.id,
        warehouse_id=wh_id,
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
