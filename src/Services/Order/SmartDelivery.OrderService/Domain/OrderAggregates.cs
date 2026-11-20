using SmartDelivery.Shared.Domain;

namespace SmartDelivery.OrderService.Domain;

public enum OrderStatus
{
    Created = 1,
    PendingPayment = 2,
    Confirmed = 3,
    DeliveryAssigned = 4,
    InTransit = 5,
    Delivered = 6,
    Cancelled = 7
}

public class OrderItem : Entity<Guid>
{
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal TotalPrice => UnitPrice * Quantity;

    private OrderItem() { }

    public OrderItem(Guid id, Guid orderId, Guid productId, string title, decimal unitPrice, int quantity)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        OrderId = orderId;
        ProductId = productId;
        Title = title.Trim();
        UnitPrice = unitPrice > 0 ? unitPrice : throw new ArgumentException("Price must be > 0", nameof(unitPrice));
        Quantity = quantity > 0 ? quantity : throw new ArgumentException("Quantity must be > 0", nameof(quantity));
    }
}

public class Order : AggregateRoot<Guid>
{
    private readonly List<OrderItem> _items = new();

    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Created;
    public string DeliveryAddress { get; private set; } = string.Empty;
    public decimal TotalAmount { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order() { }

    public Order(Guid id, Guid customerId, string deliveryAddress, IEnumerable<OrderItem> items)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        CustomerId = customerId;
        DeliveryAddress = deliveryAddress.Trim();
        CreatedAtUtc = DateTime.UtcNow;
        Status = OrderStatus.Created;

        foreach (var item in items)
        {
            _items.Add(item);
        }

        if (_items.Count == 0)
            throw new InvalidOperationException("Order must contain at least one item.");

        TotalAmount = _items.Sum(i => i.TotalPrice);
    }

    public void MarkPendingPayment()
    {
        if (Status != OrderStatus.Created)
            throw new InvalidOperationException($"Cannot move to PendingPayment from {Status}");
        Status = OrderStatus.PendingPayment;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Confirm()
    {
        if (Status != OrderStatus.PendingPayment && Status != OrderStatus.Created)
            throw new InvalidOperationException($"Cannot confirm order in status {Status}");
        Status = OrderStatus.Confirmed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Delivered)
            throw new InvalidOperationException("Cannot cancel a delivered order.");
        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateDeliveryStatus(OrderStatus newStatus)
    {
        Status = newStatus;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

public enum PaymentStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    Refunded = 4
}

public class Payment : AggregateRoot<Guid>
{
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;
    public string? TransactionReference { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    private Payment() { }

    public Payment(Guid id, Guid orderId, decimal amount)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        OrderId = orderId;
        Amount = amount > 0 ? amount : throw new ArgumentException("Amount must be > 0", nameof(amount));
        Status = PaymentStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkSucceeded(string transactionRef)
    {
        Status = PaymentStatus.Succeeded;
        TransactionReference = transactionRef;
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        Status = PaymentStatus.Failed;
        FailureReason = reason;
        ProcessedAtUtc = DateTime.UtcNow;
    }
}

public enum SagaStatus
{
    Started = 1,
    StockReserved = 2,
    PaymentProcessed = 3,
    Completed = 4,
    Compensating = 5,
    Compensated = 6,
    Failed = 7
}

public class OrderSagaState : AggregateRoot<Guid>
{
    public Guid OrderId { get; private set; }
    public SagaStatus Status { get; private set; } = SagaStatus.Started;
    public string CurrentStep { get; private set; } = "Start";
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private OrderSagaState() { }

    public OrderSagaState(Guid orderId)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        Status = SagaStatus.Started;
        CurrentStep = "Initialized";
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void TransitionTo(SagaStatus newStatus, string step, string? error = null)
    {
        Status = newStatus;
        CurrentStep = step;
        ErrorMessage = error;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

public class OutboxMessage : AggregateRoot<Guid>
{
    public DateTime OccurredOnUtc { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTime? ProcessedOnUtc { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(Guid id, string type, string content)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        OccurredOnUtc = DateTime.UtcNow;
        Type = type;
        Content = content;
        ProcessedOnUtc = null;
    }

    public void MarkProcessed()
    {
        ProcessedOnUtc = DateTime.UtcNow;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Error = error;
    }
}
