using FluentAssertions;
using SmartDelivery.OrderService.Domain;
using Xunit;

namespace SmartDelivery.UnitTests.Domain;

public class OrderDomainTests
{
    [Fact]
    public void OrderCreation_CalculatesTotalAmountCorrectly()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var item1 = new OrderItem(Guid.NewGuid(), orderId, Guid.NewGuid(), "Item 1", 25.50m, 2); // 51.00
        var item2 = new OrderItem(Guid.NewGuid(), orderId, Guid.NewGuid(), "Item 2", 10.00m, 3); // 30.00

        // Act
        var order = new Order(orderId, Guid.NewGuid(), "Ulitsa Lenina 10", new[] { item1, item2 });

        // Assert
        order.TotalAmount.Should().Be(81.00m);
        order.Status.Should().Be(OrderStatus.Created);
        order.Items.Should().HaveCount(2);
    }

    [Fact]
    public void OrderCreation_WhenItemsEmpty_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var action = () => new Order(Guid.NewGuid(), Guid.NewGuid(), "Address", Array.Empty<OrderItem>());
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Order_MarkPendingPayment_TransitionsStatus()
    {
        // Arrange
        var item = new OrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 10m, 1);
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), "Address", new[] { item });

        // Act
        order.MarkPendingPayment();

        // Assert
        order.Status.Should().Be(OrderStatus.PendingPayment);
    }

    [Fact]
    public void Order_Confirm_TransitionsStatusToConfirmed()
    {
        // Arrange
        var item = new OrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 10m, 1);
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), "Address", new[] { item });
        order.MarkPendingPayment();

        // Act
        order.Confirm();

        // Assert
        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public void Order_Cancel_SetsCancellationReason()
    {
        // Arrange
        var item = new OrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item", 10m, 1);
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), "Address", new[] { item });

        // Act
        order.Cancel("Out of stock");

        // Assert
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Be("Out of stock");
    }
}
