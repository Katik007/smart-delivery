import grpc
from concurrent import futures
import threading
from src.common.grpc_stubs import stock_pb2, stock_pb2_grpc


class StockGrpcServiceImpl(stock_pb2_grpc.StockGrpcServiceServicer):
    def __init__(self, inventory_db):
        self.inventory_db = inventory_db

    def CheckAndReserveStock(self, request, context):
        product_id = request.product_id
        quantity = request.quantity

        item = self.inventory_db.get(product_id)
        if not item:
            return stock_pb2.StockReserveReply(
                success=False,
                message=f"Product {product_id} not found in inventory"
            )

        success = item.try_reserve(quantity)
        if success:
            return stock_pb2.StockReserveReply(
                success=True,
                message="Stock reserved successfully"
            )
        else:
            return stock_pb2.StockReserveReply(
                success=False,
                message=f"Insufficient stock for {product_id}. Available: {item.available_quantity}"
            )

    def ReleaseStock(self, request, context):
        product_id = request.product_id
        quantity = request.quantity

        item = self.inventory_db.get(product_id)
        if item:
            item.release(quantity)
        return stock_pb2.StockReleaseReply(success=True)


def start_grpc_server(inventory_db, port: int = 50051):
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=5))
    stock_pb2_grpc.add_StockGrpcServiceServicer_to_server(
        StockGrpcServiceImpl(inventory_db), server
    )
    server.add_insecure_port(f"[::]:{port}")
    server.start()
    return server
