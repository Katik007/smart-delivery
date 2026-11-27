EXCHANGE = "smartdelivery.exchange"
ORDER_PAID_QUEUE = "smartdelivery.delivery.orderpaid"
ORDER_PAID_KEY = "order.paid"


def declare_order_paid_queue(channel):
    channel.exchange_declare(exchange=EXCHANGE, exchange_type="topic", durable=True)
    channel.queue_declare(queue=ORDER_PAID_QUEUE, durable=True)
    channel.queue_bind(exchange=EXCHANGE, queue=ORDER_PAID_QUEUE, routing_key=ORDER_PAID_KEY)
