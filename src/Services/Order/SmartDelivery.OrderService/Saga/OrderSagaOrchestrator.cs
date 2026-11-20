using System.Text.Json;
using SmartDelivery.OrderService.Domain;
using SmartDelivery.OrderService.Infrastructure.Clients;
using SmartDelivery.OrderService.Infrastructure.Data;
using SmartDelivery.Shared.Domain;
using SmartDelivery.Shared.Events;

namespace SmartDelivery.OrderService.Saga;

public interface IOrderSagaOrchestrator
{
    Task<Result<Order>> ExecuteOrderCheckoutSagaAsync(Order order, string? cardToken, CancellationToken ct = default);
}

public class OrderSagaOrchestrator : IOrderSagaOrchestrator
{
    private readonly OrderDbContext _dbContext;
    private readonly IStockServiceClient _stockClient;
    private readonly ILogger<OrderSagaOrchestrator> _logger;

    public OrderSagaOrchestrator(
        OrderDbContext dbContext,
        IStockServiceClient stockClient,
        ILogger<OrderSagaOrchestrator> logger)
    {
        _dbContext = dbContext;
        _stockClient = stockClient;
        _logger = logger;
    }

    public async Task<Result<Order>> ExecuteOrderCheckoutSagaAsync(Order order, string? cardToken, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting Order Checkout Saga for OrderId={OrderId}, CustomerId={CustomerId}", order.Id, order.CustomerId);

        var sagaState = new OrderSagaState(order.Id);
        _dbContext.SagaStates.Add(sagaState);

        order.MarkPendingPayment();

        var stockItems = order.Items.Select(i => new StockReservationItem(i.ProductId, i.Quantity)).ToList();

        // STEP 1: Check and Reserve Stock via CatalogService
        sagaState.TransitionTo(SagaStatus.Started, "ReservingStock");
        var stockResult = await _stockClient.ReserveStockAsync(order.Id, stockItems, ct);

        if (!stockResult.Success)
        {
            var error = string.IsNullOrEmpty(stockResult.Message) ? "Out of stock" : stockResult.Message;
            _logger.LogWarning("Saga failed at StockReservation for OrderId={OrderId}. Reason: {Reason}", order.Id, error);

            sagaState.TransitionTo(SagaStatus.Failed, "StockReservationFailed", error);
            order.Cancel($"Stock unavailable: {error}");

            await _dbContext.SaveChangesAsync(ct);
            return Result<Order>.Failure($"Stock unavailable: {error}");
        }

        sagaState.TransitionTo(SagaStatus.StockReserved, "StockReservedSuccessfully");

        // STEP 2: Process Payment
        sagaState.TransitionTo(SagaStatus.StockReserved, "ProcessingPayment");
        var payment = new Payment(Guid.NewGuid(), order.Id, order.TotalAmount);
        _dbContext.Payments.Add(payment);

        var isPaymentSuccessful = SimulatePayment(cardToken, order.TotalAmount);

        if (!isPaymentSuccessful)
        {
            _logger.LogWarning("Payment failed for OrderId={OrderId}. Initiating compensation...", order.Id);

            payment.MarkFailed("Card declined or simulated payment error.");
            sagaState.TransitionTo(SagaStatus.Compensating, "ReleasingStockCompensation", "Payment declined");

            // COMPENSATION: Release reserved stock
            var releaseSuccess = await _stockClient.ReleaseStockAsync(order.Id, stockItems, ct);
            if (releaseSuccess)
            {
                sagaState.TransitionTo(SagaStatus.Compensated, "StockReleasedSuccessfully");
            }
            else
            {
                sagaState.TransitionTo(SagaStatus.Failed, "CompensationFailed", "Failed to release stock");
            }

            order.Cancel("Payment failed: card was declined.");
            await _dbContext.SaveChangesAsync(ct);

            return Result<Order>.Failure("Payment failed. Reserved stock has been released.");
        }

        // STEP 3: Payment succeeded -> Confirm Order
        payment.MarkSucceeded($"TXN-{Guid.NewGuid():N}");
        order.Confirm();
        sagaState.TransitionTo(SagaStatus.Completed, "OrderCompletedSuccessfully");

        // STEP 4: Transactional Outbox Pattern: Save event atomically with Order
        var orderPaidEvent = new OrderPaidIntegrationEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount,
            DeliveryAddress = order.DeliveryAddress,
            Items = order.Items.Select(i => new OrderItemDto(i.ProductId, i.Title, i.UnitPrice, i.Quantity)).ToList()
        };

        var outboxMessage = new OutboxMessage(
            Guid.NewGuid(),
            nameof(OrderPaidIntegrationEvent),
            JsonSerializer.Serialize(orderPaidEvent)
        );

        _dbContext.OutboxMessages.Add(outboxMessage);

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Order Checkout Saga completed successfully for OrderId={OrderId}. Outbox message queued.", order.Id);

        return Result<Order>.Success(order);
    }

    private static bool SimulatePayment(string? cardToken, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(cardToken))
            return true; // Default simulated success

        if (cardToken.Equals("FAIL", StringComparison.OrdinalIgnoreCase) ||
            cardToken.EndsWith("0000") ||
            cardToken.Contains("declined", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
