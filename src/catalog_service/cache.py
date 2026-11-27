import json
import os
from typing import Optional, Any
import redis


class CatalogCacheService:
    def __init__(self, redis_url: Optional[str] = None):
        url = redis_url or os.getenv("REDIS_URL", "redis://localhost:6379/0")
        self.client = redis.from_url(url, decode_responses=True, socket_connect_timeout=1, socket_timeout=1)
        self.local_cache = {}

    @property
    def is_connected(self):
        try:
            return self.client.ping()
        except redis.RedisError:
            return False

    def get(self, key: str) -> Optional[dict]:
        try:
            data = self.client.get(key)
            return json.loads(data) if data else None
        except redis.RedisError:
            return self.local_cache.get(key)

    def set(self, key: str, value: Any, ttl_seconds: int = 600) -> None:
        val_str = json.dumps(value)
        try:
            self.client.setex(key, ttl_seconds, val_str)
        except redis.RedisError:
            self.local_cache[key] = value

    def invalidate(self, key: str) -> None:
        try:
            self.client.delete(key)
        except redis.RedisError:
            pass
        self.local_cache.pop(key, None)
