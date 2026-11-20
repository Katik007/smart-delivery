using SmartDelivery.Shared.Domain;

namespace SmartDelivery.OrderService.Application.DTOs;

public record CreateOrderItemRequest(Guid ProductId, string Title, decimal UnitPrice, int Quantity);

public record CreateOrderRequest(
    string DeliveryAddress,
    List<CreateOrderItemRequest> Items,
    string? PaymentCardNumber
);

public record OrderItemDto(
    Guid Id,
    Guid ProductId,
    string Title,
    decimal UnitPrice,
    int Quantity,
    decimal TotalPrice
);

public record OrderDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    string DeliveryAddress,
    decimal TotalAmount,
    string? CancellationReason,
    DateTime CreatedAtUtc,
    List<OrderItemDto> Items
);

public record SagaStatusDto(
    Guid OrderId,
    string Status,
    string CurrentStep,
    string? ErrorMessage,
    DateTime UpdatedAtUtc
);

public interface IOrderService
{
    Task<Result<OrderDto>> CreateAndCheckoutOrderAsync(CreateOrderRequest request, Guid customerId, CancellationToken ct = default);
    Task<Result<OrderDto>> GetOrderByIdAsync(Guid orderId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<OrderDto>>> GetCustomerOrdersAsync(Guid customerId, CancellationToken ct = default);
    Task<Result<SagaStatusDto>> GetSagaStatusAsync(Guid orderId, CancellationToken ct = default);
}
