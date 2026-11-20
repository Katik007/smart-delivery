using SmartDelivery.Shared.Domain;

namespace SmartDelivery.DeliveryService.Domain;

public enum DeliveryTaskStatus
{
    Created = 1,
    Assigned = 2,
    InTransit = 3,
    Delivered = 4,
    Failed = 5
}

public class DeliveryTask : AggregateRoot<Guid>
{
    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string DeliveryAddress { get; private set; } = string.Empty;
    public decimal TotalAmount { get; private set; }
    public DeliveryTaskStatus Status { get; private set; } = DeliveryTaskStatus.Created;
    public Guid? CourierId { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? AssignedAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }

    private DeliveryTask() { }

    public DeliveryTask(Guid id, Guid orderId, Guid customerId, string deliveryAddress, decimal totalAmount)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        OrderId = orderId;
        CustomerId = customerId;
        DeliveryAddress = deliveryAddress.Trim();
        TotalAmount = totalAmount;
        Status = DeliveryTaskStatus.Created;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void AssignCourier(Guid courierId)
    {
        CourierId = courierId;
        Status = DeliveryTaskStatus.Assigned;
        AssignedAtUtc = DateTime.UtcNow;
    }

    public void StartTransit()
    {
        if (Status != DeliveryTaskStatus.Assigned)
            throw new InvalidOperationException($"Cannot start transit from status {Status}");
        Status = DeliveryTaskStatus.InTransit;
    }

    public void CompleteDelivery()
    {
        if (Status != DeliveryTaskStatus.InTransit && Status != DeliveryTaskStatus.Assigned)
            throw new InvalidOperationException($"Cannot complete delivery from status {Status}");
        Status = DeliveryTaskStatus.Delivered;
        DeliveredAtUtc = DateTime.UtcNow;
    }

    public void FailDelivery(string reason)
    {
        Status = DeliveryTaskStatus.Failed;
        FailureReason = reason;
    }
}

public enum CourierStatus
{
    Available = 1,
    Busy = 2,
    Offline = 3
}

public enum VehicleType
{
    Foot = 1,
    Bicycle = 2,
    Scooter = 3,
    Car = 4
}

public class Courier : AggregateRoot<Guid>
{
    public string FullName { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public VehicleType Vehicle { get; private set; } = VehicleType.Bicycle;
    public CourierStatus Status { get; private set; } = CourierStatus.Available;
    public string CurrentZone { get; private set; } = "Center";
    public DateTime CreatedAtUtc { get; private set; }

    private Courier() { }

    public Courier(Guid id, string fullName, string phoneNumber, VehicleType vehicle, string currentZone)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        FullName = fullName.Trim();
        PhoneNumber = phoneNumber.Trim();
        Vehicle = vehicle;
        CurrentZone = currentZone.Trim();
        Status = CourierStatus.Available;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void SetStatus(CourierStatus status) => Status = status;
    public void UpdateZone(string zone) => CurrentZone = zone.Trim();
}

public class RouteStop : Entity<Guid>
{
    public Guid RouteId { get; private set; }
    public Guid DeliveryTaskId { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public int SequenceOrder { get; private set; }
    public bool IsVisited { get; private set; }

    private RouteStop() { }

    public RouteStop(Guid id, Guid routeId, Guid deliveryTaskId, string address, int sequenceOrder)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        RouteId = routeId;
        DeliveryTaskId = deliveryTaskId;
        Address = address.Trim();
        SequenceOrder = sequenceOrder;
        IsVisited = false;
    }

    public void MarkVisited() => IsVisited = true;
}

public class DeliveryRoute : AggregateRoot<Guid>
{
    private readonly List<RouteStop> _stops = new();

    public Guid CourierId { get; private set; }
    public DateOnly Date { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<RouteStop> Stops => _stops.AsReadOnly();

    private DeliveryRoute() { }

    public DeliveryRoute(Guid id, Guid courierId, DateOnly date, IEnumerable<RouteStop> stops)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        CourierId = courierId;
        Date = date;
        CreatedAtUtc = DateTime.UtcNow;
        IsCompleted = false;

        foreach (var stop in stops)
        {
            _stops.Add(stop);
        }
    }

    public void MarkCompleted() => IsCompleted = true;
}

public class InboxMessage : AggregateRoot<Guid>
{
    public DateTime OccurredOnUtc { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; private set; }
    public string? Error { get; private set; }

    private InboxMessage() { }

    public InboxMessage(Guid eventId, string type, string payload)
    {
        Id = eventId;
        OccurredOnUtc = DateTime.UtcNow;
        Type = type;
        Payload = payload;
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string error) => Error = error;
}
