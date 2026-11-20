using SmartDelivery.Shared.Domain;

namespace SmartDelivery.CatalogService.Domain;

public enum ProductStatus
{
    Active = 1,
    OutOfStock = 2,
    Discontinued = 3
}

public class Product : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid SellerId { get; private set; }
    public ProductStatus Status { get; private set; } = ProductStatus.Active;
    public DateTime CreatedAtUtc { get; private set; }

    public InventoryItem? Inventory { get; private set; }

    private Product() { }

    public Product(Guid id, string name, string description, decimal price, Guid categoryId, Guid sellerId)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Name = name.Trim();
        Description = description.Trim();
        Price = price > 0 ? price : throw new ArgumentException("Price must be greater than zero.", nameof(price));
        CategoryId = categoryId;
        SellerId = sellerId;
        Status = ProductStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string description, decimal price)
    {
        Name = name.Trim();
        Description = description.Trim();
        Price = price > 0 ? price : throw new ArgumentException("Price must be greater than zero.", nameof(price));
    }

    public void SetStatus(ProductStatus status)
    {
        Status = status;
    }

    public void AttachInventory(InventoryItem inventory)
    {
        Inventory = inventory;
    }
}

public class Category : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid? ParentCategoryId { get; private set; }

    private Category() { }

    public Category(Guid id, string name, string description, Guid? parentCategoryId = null)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Name = name.Trim();
        Description = description.Trim();
        ParentCategoryId = parentCategoryId;
    }
}

public class InventoryItem : AggregateRoot<Guid>
{
    public Guid ProductId { get; private set; }
    public string WarehouseLocation { get; private set; } = string.Empty;
    public int AvailableQuantity { get; private set; }
    public int ReservedQuantity { get; private set; }

    public int TotalStock => AvailableQuantity + ReservedQuantity;

    private InventoryItem() { }

    public InventoryItem(Guid id, Guid productId, string warehouseLocation, int initialStock)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProductId = productId;
        WarehouseLocation = warehouseLocation;
        AvailableQuantity = Math.Max(0, initialStock);
        ReservedQuantity = 0;
    }

    public bool TryReserve(int quantity)
    {
        if (quantity <= 0 || AvailableQuantity < quantity) return false;

        AvailableQuantity -= quantity;
        ReservedQuantity += quantity;
        return true;
    }

    public void Release(int quantity)
    {
        var toRelease = Math.Min(quantity, ReservedQuantity);
        ReservedQuantity -= toRelease;
        AvailableQuantity += toRelease;
    }

    public void ConfirmDeduction(int quantity)
    {
        var toDeduct = Math.Min(quantity, ReservedQuantity);
        ReservedQuantity -= toDeduct;
    }

    public void Restock(int quantity)
    {
        if (quantity > 0)
        {
            AvailableQuantity += quantity;
        }
    }
}

public class Warehouse : AggregateRoot<Guid>
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private Warehouse() { }

    public Warehouse(Guid id, string code, string name, string address)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Address = address.Trim();
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
