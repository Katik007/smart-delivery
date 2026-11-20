using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using SmartDelivery.CatalogService.Infrastructure.Data;
using SmartDelivery.Shared.Protos;

namespace SmartDelivery.CatalogService.Grpc;

public class StockGrpcServiceImpl : StockService.StockServiceBase
{
    private readonly CatalogDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly ILogger<StockGrpcServiceImpl> _logger;

    public StockGrpcServiceImpl(CatalogDbContext dbContext, IDistributedCache cache, ILogger<StockGrpcServiceImpl> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public override async Task<ReserveStockResponse> CheckAndReserveStock(ReserveStockRequest request, ServerCallContext context)
    {
        _logger.LogInformation("gRPC CheckAndReserveStock called for OrderId={OrderId} with {Count} items", request.OrderId, request.Items.Count);

        var response = new ReserveStockResponse();
        var inventoriesToReserve = new List<(Domain.InventoryItem Inventory, int Quantity)>();

        foreach (var item in request.Items)
        {
            if (!Guid.TryParse(item.ProductId, out var productId))
            {
                response.Success = false;
                response.Message = $"Invalid ProductId format: {item.ProductId}";
                response.UnavailableProductIds.Add(item.ProductId);
                return response;
            }

            var inventory = await _dbContext.InventoryItems.FirstOrDefaultAsync(i => i.ProductId == productId, context.CancellationToken);
            if (inventory == null || inventory.AvailableQuantity < item.Quantity)
            {
                response.UnavailableProductIds.Add(item.ProductId);
            }
            else
            {
                inventoriesToReserve.Add((inventory, item.Quantity));
            }
        }

        if (response.UnavailableProductIds.Count > 0)
        {
            response.Success = false;
            response.Message = "Insufficient stock for some products.";
            _logger.LogWarning("Insufficient stock for OrderId={OrderId}. Unavailable items: {Unavailable}", request.OrderId, string.Join(",", response.UnavailableProductIds));
            return response;
        }

        // Reserve all
        foreach (var (inventory, quantity) in inventoriesToReserve)
        {
            inventory.TryReserve(quantity);
            try
            {
                await _cache.RemoveAsync($"product_{inventory.ProductId}");
            }
            catch
            {
                // ignore
            }
        }

        try
        {
            await _cache.RemoveAsync("catalog_products_all");
        }
        catch
        {
            // ignore
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken);

        response.Success = true;
        response.Message = "Stock successfully reserved.";
        _logger.LogInformation("Stock reserved successfully for OrderId={OrderId}", request.OrderId);
        return response;
    }

    public override async Task<ReleaseStockResponse> ReleaseStock(ReleaseStockRequest request, ServerCallContext context)
    {
        _logger.LogInformation("gRPC ReleaseStock called for OrderId={OrderId}", request.OrderId);

        foreach (var item in request.Items)
        {
            if (Guid.TryParse(item.ProductId, out var productId))
            {
                var inventory = await _dbContext.InventoryItems.FirstOrDefaultAsync(i => i.ProductId == productId, context.CancellationToken);
                if (inventory != null)
                {
                    inventory.Release(item.Quantity);
                    try
                    {
                        await _cache.RemoveAsync($"product_{inventory.ProductId}");
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
        }

        try
        {
            await _cache.RemoveAsync("catalog_products_all");
        }
        catch
        {
            // ignore
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken);

        return new ReleaseStockResponse
        {
            Success = true,
            Message = "Stock successfully released."
        };
    }
}
