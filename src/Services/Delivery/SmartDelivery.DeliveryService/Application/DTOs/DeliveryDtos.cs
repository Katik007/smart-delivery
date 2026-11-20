using SmartDelivery.DeliveryService.Domain;

namespace SmartDelivery.DeliveryService.Application.DTOs;

public record DeliveryTaskDto(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    string DeliveryAddress,
    decimal TotalAmount,
    string Status,
    Guid? CourierId,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? AssignedAtUtc,
    DateTime? DeliveredAtUtc
);

public record CourierDto(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string Vehicle,
    string Status,
    string CurrentZone,
    DateTime CreatedAtUtc
);

public record CreateCourierRequest(
    string FullName,
    string PhoneNumber,
    VehicleType Vehicle,
    string CurrentZone
);

public record UpdateTaskStatusRequest(
    DeliveryTaskStatus Status,
    string? FailureReason
);

public record AssignCourierRequest(
    Guid CourierId
);
