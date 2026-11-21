import pytest
from src.catalog_service.models import InventoryItem
from src.order_service.models import Order
from src.delivery_service.models import Courier, DeliveryTask
from src.common.domain import OrderStatus, DeliveryStatus, CourierStatus


def test_inventory_try_reserve_success():
    item = InventoryItem(product_id="p1", warehouse_id="w1", available_quantity=10, reserved_quantity=0)
    assert item.try_reserve(4) is True
    assert item.available_quantity == 6
    assert item.reserved_quantity == 4


def test_inventory_try_reserve_insufficient_stock():
    item = InventoryItem(product_id="p1", warehouse_id="w1", available_quantity=3, reserved_quantity=0)
    assert item.try_reserve(5) is False
    assert item.available_quantity == 3
    assert item.reserved_quantity == 0


def test_inventory_release_restores_stock():
    item = InventoryItem(product_id="p1", warehouse_id="w1", available_quantity=5, reserved_quantity=5)
    item.release(3)
    assert item.reserved_quantity == 2
    assert item.available_quantity == 8


def test_order_can_cancel_invariant():
    order = Order(customer_id="c1", delivery_address="Street 1", total_amount=100.0, status=OrderStatus.CREATED)
    assert order.can_cancel() is True

    order.status = OrderStatus.DELIVERED
    assert order.can_cancel() is False


def test_courier_assignment_changes_status():
    courier = Courier(name="Иван", phone="123", status=CourierStatus.AVAILABLE)
    task = DeliveryTask(order_id="o1", delivery_address="Address 1")

    task.assign_to_courier(courier)
    assert task.courier_id == courier.id
    assert task.status == DeliveryStatus.ASSIGNED
    assert courier.status == CourierStatus.BUSY
