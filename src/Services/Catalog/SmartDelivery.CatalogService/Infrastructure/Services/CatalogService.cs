using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using SmartDelivery.CatalogService.Application.DTOs;
using SmartDelivery.CatalogService.Domain;
using SmartDelivery.CatalogService.Infrastructure.Data;
using SmartDelivery.Shared.Domain;

namespace SmartDelivery.CatalogService.Infrastructure.Services;

public class CatalogService : ICatalogService
{
    private readonly CatalogDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private const string AllProductsCacheKey = "catalog_products_all";

    public CatalogService(CatalogDbContext dbContext, IDistributedCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<Result<ProductDto>> CreateProductAsync(CreateProductRequest request, Guid sellerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<ProductDto>.Failure("Product name is required.");
        if (request.Price <= 0)
            return Result<ProductDto>.Failure("Price must be greater than zero.");

        var product = new Product(
            Guid.NewGuid(),
            request.Name,
            request.Description,
            request.Price,
            request.CategoryId,
            sellerId
        );

        var inventory = new InventoryItem(
            Guid.NewGuid(),
            product.Id,
            request.WarehouseLocation ?? "Default-WH-A",
            request.InitialStock
        );

        product.AttachInventory(inventory);

        _dbContext.Products.Add(product);
        _dbContext.InventoryItems.Add(inventory);
        await _dbContext.SaveChangesAsync(ct);

        await InvalidateCacheAsync(product.Id, ct);

        return Result<ProductDto>.Success(MapToDto(product, inventory));
    }

    public async Task<Result<ProductDto>> UpdateProductAsync(Guid id, UpdateProductRequest request, Guid sellerId, CancellationToken ct = default)
    {
        var product = await _dbContext.Products
            .Include(p => p.Inventory)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (product == null)
            return Result<ProductDto>.Failure("Product not found.");

        if (product.SellerId != sellerId)
            return Result<ProductDto>.Failure("Forbidden: You do not own this product.");

        product.UpdateDetails(request.Name, request.Description, request.Price);
        product.SetStatus(request.Status);

        await _dbContext.SaveChangesAsync(ct);
        await InvalidateCacheAsync(product.Id, ct);

        return Result<ProductDto>.Success(MapToDto(product, product.Inventory));
    }

    public async Task<Result<ProductDto>> GetProductByIdAsync(Guid id, CancellationToken ct = default)
    {
        var cacheKey = $"product_{id}";
        try
        {
            var cached = await _cache.GetStringAsync(cacheKey, ct);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedDto = JsonSerializer.Deserialize<ProductDto>(cached);
                if (cachedDto != null) return Result<ProductDto>.Success(cachedDto);
            }
        }
        catch
        {
            // Redis fallback
        }

        var product = await _dbContext.Products
            .Include(p => p.Inventory)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (product == null)
            return Result<ProductDto>.Failure("Product not found.");

        var dto = MapToDto(product, product.Inventory);

        try
        {
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(dto), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            }, ct);
        }
        catch
        {
            // Cache failure ignored
        }

        return Result<ProductDto>.Success(dto);
    }

    public async Task<Result<IReadOnlyList<ProductDto>>> GetProductsAsync(Guid? categoryId = null, CancellationToken ct = default)
    {
        if (categoryId == null)
        {
            try
            {
                var cached = await _cache.GetStringAsync(AllProductsCacheKey, ct);
                if (!string.IsNullOrEmpty(cached))
                {
                    var cachedList = JsonSerializer.Deserialize<List<ProductDto>>(cached);
                    if (cachedList != null) return Result<IReadOnlyList<ProductDto>>.Success(cachedList);
                }
            }
            catch
            {
                // Fallback
            }
        }

        var query = _dbContext.Products
            .Include(p => p.Inventory)
            .AsNoTracking();

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        var products = await query.ToListAsync(ct);
        var dtos = products.Select(p => MapToDto(p, p.Inventory)).ToList();

        if (categoryId == null)
        {
            try
            {
                await _cache.SetStringAsync(AllProductsCacheKey, JsonSerializer.Serialize(dtos), new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                }, ct);
            }
            catch
            {
                // Ignore
            }
        }

        return Result<IReadOnlyList<ProductDto>>.Success(dtos);
    }

    public async Task<Result<bool>> RestockProductAsync(Guid id, int quantity, CancellationToken ct = default)
    {
        var inventory = await _dbContext.InventoryItems.FirstOrDefaultAsync(i => i.ProductId == id, ct);
        if (inventory == null)
            return Result<bool>.Failure("Inventory item not found.");

        inventory.Restock(quantity);
        await _dbContext.SaveChangesAsync(ct);
        await InvalidateCacheAsync(id, ct);

        return Result<bool>.Success(true);
    }

    public async Task<Result<CategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<CategoryDto>.Failure("Category name is required.");

        var category = new Category(Guid.NewGuid(), request.Name, request.Description, request.ParentCategoryId);
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(ct);

        return Result<CategoryDto>.Success(new CategoryDto(category.Id, category.Name, category.Description, category.ParentCategoryId));
    }

    public async Task<Result<IReadOnlyList<CategoryDto>>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var categories = await _dbContext.Categories.AsNoTracking().ToListAsync(ct);
        var dtos = categories.Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.ParentCategoryId)).ToList();
        return Result<IReadOnlyList<CategoryDto>>.Success(dtos);
    }

    private async Task InvalidateCacheAsync(Guid productId, CancellationToken ct)
    {
        try
        {
            await _cache.RemoveAsync($"product_{productId}", ct);
            await _cache.RemoveAsync(AllProductsCacheKey, ct);
        }
        catch
        {
            // Redis failure fallback
        }
    }

    private static ProductDto MapToDto(Product p, InventoryItem? inv) =>
        new(
            p.Id,
            p.Name,
            p.Description,
            p.Price,
            p.CategoryId,
            p.SellerId,
            p.Status.ToString(),
            inv?.AvailableQuantity ?? 0,
            inv?.ReservedQuantity ?? 0,
            p.CreatedAtUtc
        );
}
