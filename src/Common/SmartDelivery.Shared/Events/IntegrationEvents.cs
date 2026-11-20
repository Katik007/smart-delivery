namespace SmartDelivery.Shared.Events;

public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}

public record OrderItemDto(Guid ProductId, string Title, decimal UnitPrice, int Quantity);

public record OrderPaidIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
    public string DeliveryAddress { get; init; } = string.Empty;
    public List<OrderItemDto> Items { get; init; } = new();
}

public record OrderCancelledIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public List<OrderItemDto> Items { get; init; } = new();
}

public record DeliveryStatusChangedIntegrationEvent : IntegrationEvent
{
    public Guid DeliveryTaskId { get; init; }
    public Guid OrderId { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid? CourierId { get; init; }
}
