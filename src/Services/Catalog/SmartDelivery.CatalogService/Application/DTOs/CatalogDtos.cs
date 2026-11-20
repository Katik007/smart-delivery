using SmartDelivery.CatalogService.Domain;
using SmartDelivery.Shared.Domain;

namespace SmartDelivery.CatalogService.Application.DTOs;

public record CreateProductRequest(
    string Name,
    string Description,
    decimal Price,
    Guid CategoryId,
    int InitialStock,
    string? WarehouseLocation
);

public record UpdateProductRequest(
    string Name,
    string Description,
    decimal Price,
    ProductStatus Status
);

public record RestockRequest(int Quantity);

public record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    Guid CategoryId,
    Guid SellerId,
    string Status,
    int AvailableStock,
    int ReservedStock,
    DateTime CreatedAtUtc
);

public record CreateCategoryRequest(string Name, string Description, Guid? ParentCategoryId);

public record CategoryDto(Guid Id, string Name, string Description, Guid? ParentCategoryId);

public interface ICatalogService
{
    Task<Result<ProductDto>> CreateProductAsync(CreateProductRequest request, Guid sellerId, CancellationToken ct = default);
    Task<Result<ProductDto>> UpdateProductAsync(Guid id, UpdateProductRequest request, Guid sellerId, CancellationToken ct = default);
    Task<Result<ProductDto>> GetProductByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ProductDto>>> GetProductsAsync(Guid? categoryId = null, CancellationToken ct = default);
    Task<Result<bool>> RestockProductAsync(Guid id, int quantity, CancellationToken ct = default);
    Task<Result<CategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<CategoryDto>>> GetCategoriesAsync(CancellationToken ct = default);
}
