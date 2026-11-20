using FluentAssertions;
using SmartDelivery.DeliveryService.Domain;
using Xunit;

namespace SmartDelivery.UnitTests.Domain;

public class DeliveryDomainTests
{
    [Fact]
    public void DeliveryTask_AssignCourierAndDeliver_UpdatesTimestampsAndStatus()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var task = new DeliveryTask(taskId, orderId, customerId, "Nevsky Prospekt 1", 1500m);
        var courierId = Guid.NewGuid();

        task.Status.Should().Be(DeliveryTaskStatus.Created);

        // Act 1: Assign courier
        task.AssignCourier(courierId);
        task.Status.Should().Be(DeliveryTaskStatus.Assigned);
        task.CourierId.Should().Be(courierId);
        task.AssignedAtUtc.Should().NotBeNull();

        // Act 2: Start transit
        task.StartTransit();
        task.Status.Should().Be(DeliveryTaskStatus.InTransit);

        // Act 3: Complete delivery
        task.CompleteDelivery();
        task.Status.Should().Be(DeliveryTaskStatus.Delivered);
        task.DeliveredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void InboxMessage_RecordsIdempotentKey()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var message = new InboxMessage(eventId, "OrderPaidIntegrationEvent", "{\"OrderId\":\"123\"}");

        // Assert
        message.Id.Should().Be(eventId);
        message.Type.Should().Be("OrderPaidIntegrationEvent");
        message.Payload.Should().Contain("123");
        message.Error.Should().BeNull();
    }
}
