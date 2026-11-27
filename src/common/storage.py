"""Persist domain models in a PostgreSQL schema owned by each service."""

import os
from contextlib import contextmanager
from contextvars import ContextVar
from threading import RLock

import psycopg
from psycopg import sql
from psycopg.types.json import Jsonb


class Storage:
    def __init__(self, schema):
        self.schema = schema
        self.dsn = os.getenv("DATABASE_URL")
        self._connection = ContextVar(schema, default=None)
        self._lock = RLock()
        self._tables = []

    def collection(self, name, model):
        if not self.dsn:
            return {}
        table = sql.Identifier(self.schema, name)
        self._tables.append(table)
        return PostgresCollection(self, table, model)

    def initialize(self):
        if self.dsn:
            with self.transaction(lock=True) as conn:
                conn.execute(sql.SQL("CREATE SCHEMA IF NOT EXISTS {}").format(sql.Identifier(self.schema)))
                for table in self._tables:
                    conn.execute(sql.SQL(
                        "CREATE TABLE IF NOT EXISTS {} (id text PRIMARY KEY, data jsonb NOT NULL)"
                    ).format(table))

    @contextmanager
    def transaction(self, *, lock=False):
        if not self.dsn:
            with self._lock:
                yield None
            return
        current = self._connection.get()
        if current is not None:
            yield current
            return
        with psycopg.connect(self.dsn, connect_timeout=5) as conn:
            token = self._connection.set(conn)
            try:
                if lock:
                    conn.execute("SELECT pg_advisory_xact_lock(hashtext(%s))", (self.schema,))
                yield conn
            finally:
                self._connection.reset(token)


class PostgresCollection:
    def __init__(self, storage, table, model):
        self.storage = storage
        self.table = table
        self.model = model

    def get(self, key):
        with self.storage.transaction() as conn:
            row = conn.execute(sql.SQL("SELECT data FROM {} WHERE id = %s").format(self.table), (key,)).fetchone()
        return self.model.model_validate(row[0]) if row else None

    def __contains__(self, key):
        return self.get(key) is not None

    def __bool__(self):
        return len(self) > 0

    def __len__(self):
        with self.storage.transaction() as conn:
            return conn.execute(sql.SQL("SELECT count(*) FROM {}").format(self.table)).fetchone()[0]

    def __setitem__(self, key, model):
        with self.storage.transaction() as conn:
            conn.execute(sql.SQL(
                "INSERT INTO {} (id, data) VALUES (%s, %s) "
                "ON CONFLICT (id) DO UPDATE SET data = EXCLUDED.data"
            ).format(self.table), (key, Jsonb(model.model_dump(mode="json"))))

    def values(self):
        with self.storage.transaction() as conn:
            rows = conn.execute(sql.SQL("SELECT data FROM {} ORDER BY id").format(self.table)).fetchall()
        return [self.model.model_validate(row[0]) for row in rows]
