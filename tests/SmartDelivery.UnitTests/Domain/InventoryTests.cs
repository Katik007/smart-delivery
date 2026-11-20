using FluentAssertions;
using SmartDelivery.CatalogService.Domain;
using Xunit;

namespace SmartDelivery.UnitTests.Domain;

public class InventoryTests
{
    [Fact]
    public void TryReserve_WhenStockIsAvailable_DecreasesAvailableAndIncreasesReserved()
    {
        // Arrange
        var inventory = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "WH-A1", 100);

        // Act
        var result = inventory.TryReserve(30);

        // Assert
        result.Should().BeTrue();
        inventory.AvailableQuantity.Should().Be(70);
        inventory.ReservedQuantity.Should().Be(30);
        inventory.TotalStock.Should().Be(100);
    }

    [Fact]
    public void TryReserve_WhenRequestedMoreThanAvailable_ReturnsFalseAndLeavesStockIntact()
    {
        // Arrange
        var inventory = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "WH-A1", 20);

        // Act
        var result = inventory.TryReserve(25);

        // Assert
        result.Should().BeFalse();
        inventory.AvailableQuantity.Should().Be(20);
        inventory.ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public void Release_RestoresAvailableStockAndReducesReserved()
    {
        // Arrange
        var inventory = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "WH-A1", 50);
        inventory.TryReserve(20);

        // Act
        inventory.Release(15);

        // Assert
        inventory.ReservedQuantity.Should().Be(5);
        inventory.AvailableQuantity.Should().Be(45);
    }

    [Fact]
    public void Restock_IncreasesAvailableStock()
    {
        // Arrange
        var inventory = new InventoryItem(Guid.NewGuid(), Guid.NewGuid(), "WH-A1", 10);

        // Act
        inventory.Restock(40);

        // Assert
        inventory.AvailableQuantity.Should().Be(50);
    }
}
