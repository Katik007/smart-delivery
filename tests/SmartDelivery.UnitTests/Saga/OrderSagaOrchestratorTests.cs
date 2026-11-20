using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartDelivery.OrderService.Domain;
using SmartDelivery.OrderService.Infrastructure.Clients;
using SmartDelivery.OrderService.Infrastructure.Data;
using SmartDelivery.OrderService.Saga;
using Xunit;

namespace SmartDelivery.UnitTests.Saga;

public class OrderSagaOrchestratorTests
{
    private readonly DbContextOptions<OrderDbContext> _dbOptions;
    private readonly Mock<IStockServiceClient> _mockStockClient;

    public OrderSagaOrchestratorTests()
    {
        _dbOptions = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(databaseName: $"OrderDb_{Guid.NewGuid()}")
            .Options;

        _mockStockClient = new Mock<IStockServiceClient>();
    }

    [Fact]
    public async Task CheckoutSaga_WhenStockAvailableAndPaymentSucceeds_ConfirmsOrderAndCreatesOutbox()
    {
        // Arrange
        using var db = new OrderDbContext(_dbOptions);
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var orderItem = new OrderItem(Guid.NewGuid(), orderId, productId, "Test Product", 100m, 2);
        var order = new Order(orderId, customerId, "Test Address 123", new[] { orderItem });
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        _mockStockClient.Setup(c => c.ReserveStockAsync(orderId, It.IsAny<IEnumerable<StockReservationItem>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReservationResult(true, "Reserved", Array.Empty<string>()));

        var orchestrator = new OrderSagaOrchestrator(db, _mockStockClient.Object, NullLogger<OrderSagaOrchestrator>.Instance);

        // Act
        var result = await orchestrator.ExecuteOrderCheckoutSagaAsync(order, cardToken: "4111-SUCCESS-CARD");

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Confirmed);

        var outboxMessages = await db.OutboxMessages.ToListAsync();
        outboxMessages.Should().HaveCount(1);
        outboxMessages[0].Type.Should().Be("OrderPaidIntegrationEvent");

        var sagaState = await db.SagaStates.FirstOrDefaultAsync(s => s.OrderId == orderId);
        sagaState.Should().NotBeNull();
        sagaState!.Status.Should().Be(SagaStatus.Completed);
    }

    [Fact]
    public async Task CheckoutSaga_WhenStockUnavailable_CancelsOrderWithoutCharging()
    {
        // Arrange
        using var db = new OrderDbContext(_dbOptions);
        var orderId = Guid.NewGuid();
        var orderItem = new OrderItem(Guid.NewGuid(), orderId, Guid.NewGuid(), "Out of Stock Product", 50m, 1);
        var order = new Order(orderId, Guid.NewGuid(), "Sample Street", new[] { orderItem });
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        _mockStockClient.Setup(c => c.ReserveStockAsync(orderId, It.IsAny<IEnumerable<StockReservationItem>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReservationResult(false, "Out of stock in warehouse", new[] { orderItem.ProductId.ToString() }));

        var orchestrator = new OrderSagaOrchestrator(db, _mockStockClient.Object, NullLogger<OrderSagaOrchestrator>.Instance);

        // Act
        var result = await orchestrator.ExecuteOrderCheckoutSagaAsync(order, cardToken: "4111-SUCCESS-CARD");

        // Assert
        result.IsFailure.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Contain("Stock unavailable");

        var payments = await db.Payments.ToListAsync();
        payments.Should().BeEmpty(); // Payment was never initiated

        var sagaState = await db.SagaStates.FirstOrDefaultAsync(s => s.OrderId == orderId);
        sagaState.Should().NotBeNull();
        sagaState!.Status.Should().Be(SagaStatus.Failed);
    }

    [Fact]
    public async Task CheckoutSaga_WhenPaymentFails_TriggersCompensationAndReleasesStock()
    {
        // Arrange
        using var db = new OrderDbContext(_dbOptions);
        var orderId = Guid.NewGuid();
        var orderItem = new OrderItem(Guid.NewGuid(), orderId, Guid.NewGuid(), "Expensive Product", 500m, 1);
        var order = new Order(orderId, Guid.NewGuid(), "Sample Street 42", new[] { orderItem });
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        _mockStockClient.Setup(c => c.ReserveStockAsync(orderId, It.IsAny<IEnumerable<StockReservationItem>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockReservationResult(true, "Reserved", Array.Empty<string>()));

        _mockStockClient.Setup(c => c.ReleaseStockAsync(orderId, It.IsAny<IEnumerable<StockReservationItem>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var orchestrator = new OrderSagaOrchestrator(db, _mockStockClient.Object, NullLogger<OrderSagaOrchestrator>.Instance);

        // Act - cardToken with "declined" simulates card failure
        var result = await orchestrator.ExecuteOrderCheckoutSagaAsync(order, cardToken: "card_declined_insufficient_funds");

        // Assert
        result.IsFailure.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);

        // Verify compensation call
        _mockStockClient.Verify(c => c.ReleaseStockAsync(orderId, It.IsAny<IEnumerable<StockReservationItem>>(), It.IsAny<CancellationToken>()), Times.Once);

        var sagaState = await db.SagaStates.FirstOrDefaultAsync(s => s.OrderId == orderId);
        sagaState.Should().NotBeNull();
        sagaState!.Status.Should().Be(SagaStatus.Compensated);
    }
}
