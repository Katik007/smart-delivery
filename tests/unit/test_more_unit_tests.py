import pytest
from src.catalog_service.cache import CatalogCacheService
from src.delivery_service.models import DeliveryTask
from src.common.domain import DeliveryStatus
from src.common.security import decode_token
from fastapi import HTTPException


def test_cache_local_fallback_invalidation():
    cache = CatalogCacheService(redis_url="redis://nonexistent:9999/0")
    cache.set("key1", {"name": "item1"}, ttl_seconds=60)
    assert cache.get("key1") == {"name": "item1"}

    cache.invalidate("key1")
    assert cache.get("key1") is None


def test_delivery_status_flow():
    task = DeliveryTask(order_id="o-10", delivery_address="Ленина, 5")
    assert task.status == DeliveryStatus.PENDING

    task.status = DeliveryStatus.IN_TRANSIT
    assert task.status == DeliveryStatus.IN_TRANSIT

    task.status = DeliveryStatus.DELIVERED
    assert task.status == DeliveryStatus.DELIVERED


def test_invalid_jwt_raises_http_401():
    with pytest.raises(HTTPException) as excinfo:
        decode_token("completely.invalid.token")
    assert excinfo.value.status_code == 401
