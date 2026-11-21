import json
import os
from typing import Optional, Any
import redis


class CatalogCacheService:
    def __init__(self, redis_url: Optional[str] = None):
        url = redis_url or os.getenv("REDIS_URL", "redis://localhost:6379/0")
        try:
            self.client = redis.from_url(url, decode_responses=True)
            self.client.ping()
            self.is_connected = True
        except Exception:
            self.is_connected = False
            self.local_cache = {}

    def get(self, key: str) -> Optional[dict]:
        if self.is_connected:
            try:
                data = self.client.get(key)
                if data:
                    return json.loads(data)
            except Exception:
                pass
        return self.local_cache.get(key)

    def set(self, key: str, value: Any, ttl_seconds: int = 600) -> None:
        val_str = json.dumps(value)
        if self.is_connected:
            try:
                self.client.setex(key, ttl_seconds, val_str)
                return
            except Exception:
                pass
        self.local_cache[key] = value

    def invalidate(self, key: str) -> None:
        if self.is_connected:
            try:
                self.client.delete(key)
            except Exception:
                pass
        self.local_cache.pop(key, None)
