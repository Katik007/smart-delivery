import grpc
from concurrent import futures
from src.common.grpc_stubs import stock_pb2, stock_pb2_grpc


class StockGrpcServiceImpl(stock_pb2_grpc.StockGrpcServiceServicer):
    def __init__(self, inventory_db, storage):
        self.inventory_db = inventory_db
        self.storage = storage

    def CheckAndReserveStock(self, request, context):
        product_id = request.product_id
        quantity = request.quantity

        with self.storage.transaction(lock=True):
            item = self.inventory_db.get(product_id)
            if item is None:
                return stock_pb2.StockReserveReply(
                    success=False,
                    message=f"Product {product_id} not found in inventory"
                )
            if item.try_reserve(quantity):
                self.inventory_db[product_id] = item
                return stock_pb2.StockReserveReply(success=True, message="Stock reserved successfully")
            return stock_pb2.StockReserveReply(
                success=False,
                message=f"Insufficient stock for {product_id}. Available: {item.available_quantity}"
            )

    def ReleaseStock(self, request, context):
        product_id = request.product_id
        quantity = request.quantity

        with self.storage.transaction(lock=True):
            item = self.inventory_db.get(product_id)
            if item:
                item.release(quantity)
                self.inventory_db[product_id] = item
        return stock_pb2.StockReleaseReply(success=True)


def start_grpc_server(inventory_db, storage, port: int = 50051):
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=5))
    stock_pb2_grpc.add_StockGrpcServiceServicer_to_server(
        StockGrpcServiceImpl(inventory_db, storage), server
    )
    server.add_insecure_port(f"[::]:{port}")
    server.start()
    return server
