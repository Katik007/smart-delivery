using Microsoft.EntityFrameworkCore;
using SmartDelivery.OrderService.Application.DTOs;
using SmartDelivery.OrderService.Domain;
using SmartDelivery.OrderService.Infrastructure.Data;
using SmartDelivery.OrderService.Saga;
using SmartDelivery.Shared.Domain;

namespace SmartDelivery.OrderService.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly OrderDbContext _dbContext;
    private readonly IOrderSagaOrchestrator _sagaOrchestrator;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        OrderDbContext dbContext,
        IOrderSagaOrchestrator sagaOrchestrator,
        ILogger<OrderService> logger)
    {
        _dbContext = dbContext;
        _sagaOrchestrator = sagaOrchestrator;
        _logger = logger;
    }

    public async Task<Result<OrderDto>> CreateAndCheckoutOrderAsync(CreateOrderRequest request, Guid customerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.DeliveryAddress))
            return Result<OrderDto>.Failure("Delivery address is required.");

        if (request.Items == null || request.Items.Count == 0)
            return Result<OrderDto>.Failure("Order must have at least one item.");

        var orderId = Guid.NewGuid();
        var items = new List<OrderItem>();

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0 || item.UnitPrice <= 0)
                return Result<OrderDto>.Failure("Item price and quantity must be greater than zero.");

            items.Add(new OrderItem(
                Guid.NewGuid(),
                orderId,
                item.ProductId,
                item.Title,
                item.UnitPrice,
                item.Quantity
            ));
        }

        var order = new Order(orderId, customerId, request.DeliveryAddress, items);

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Order {OrderId} created. Launching Saga Orchestration...", order.Id);

        // Execute Saga
        var sagaResult = await _sagaOrchestrator.ExecuteOrderCheckoutSagaAsync(order, request.PaymentCardNumber, ct);

        if (sagaResult.IsFailure)
        {
            return Result<OrderDto>.Failure($"Checkout failed: {sagaResult.Error}");
        }

        return Result<OrderDto>.Success(MapToDto(sagaResult.Value!));
    }

    public async Task<Result<OrderDto>> GetOrderByIdAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order == null)
            return Result<OrderDto>.Failure("Order not found.");

        return Result<OrderDto>.Success(MapToDto(order));
    }

    public async Task<Result<IReadOnlyList<OrderDto>>> GetCustomerOrdersAsync(Guid customerId, CancellationToken ct = default)
    {
        var orders = await _dbContext.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(ct);

        var dtos = orders.Select(MapToDto).ToList();
        return Result<IReadOnlyList<OrderDto>>.Success(dtos);
    }

    public async Task<Result<SagaStatusDto>> GetSagaStatusAsync(Guid orderId, CancellationToken ct = default)
    {
        var saga = await _dbContext.SagaStates
            .FirstOrDefaultAsync(s => s.OrderId == orderId, ct);

        if (saga == null)
            return Result<SagaStatusDto>.Failure("Saga state not found.");

        return Result<SagaStatusDto>.Success(new SagaStatusDto(
            saga.OrderId,
            saga.Status.ToString(),
            saga.CurrentStep,
            saga.ErrorMessage,
            saga.UpdatedAtUtc
        ));
    }

    private static OrderDto MapToDto(Order o) =>
        new(
            o.Id,
            o.CustomerId,
            o.Status.ToString(),
            o.DeliveryAddress,
            o.TotalAmount,
            o.CancellationReason,
            o.CreatedAtUtc,
            o.Items.Select(i => new OrderItemDto(
                i.Id,
                i.ProductId,
                i.Title,
                i.UnitPrice,
                i.Quantity,
                i.TotalPrice
            )).ToList()
        );
}
