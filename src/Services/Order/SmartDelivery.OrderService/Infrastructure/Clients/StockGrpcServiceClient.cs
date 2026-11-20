using Grpc.Net.Client;
using SmartDelivery.Shared.Protos;

namespace SmartDelivery.OrderService.Infrastructure.Clients;

public class StockGrpcServiceClient : IStockServiceClient
{
    private readonly StockService.StockServiceClient _client;
    private readonly ILogger<StockGrpcServiceClient> _logger;

    public StockGrpcServiceClient(StockService.StockServiceClient client, ILogger<StockGrpcServiceClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<StockReservationResult> ReserveStockAsync(Guid orderId, IEnumerable<StockReservationItem> items, CancellationToken ct = default)
    {
        try
        {
            var request = new ReserveStockRequest
            {
                OrderId = orderId.ToString()
            };

            foreach (var item in items)
            {
                request.Items.Add(new StockItemRequest
                {
                    ProductId = item.ProductId.ToString(),
                    Quantity = item.Quantity
                });
            }

            var response = await _client.CheckAndReserveStockAsync(request, cancellationToken: ct);
            return new StockReservationResult(response.Success, response.Message, response.UnavailableProductIds.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call Catalog gRPC CheckAndReserveStock for OrderId={OrderId}", orderId);
            return new StockReservationResult(false, $"gRPC error: {ex.Message}", Array.Empty<string>());
        }
    }

    public async Task<bool> ReleaseStockAsync(Guid orderId, IEnumerable<StockReservationItem> items, CancellationToken ct = default)
    {
        try
        {
            var request = new ReleaseStockRequest
            {
                OrderId = orderId.ToString()
            };

            foreach (var item in items)
            {
                request.Items.Add(new StockItemRequest
                {
                    ProductId = item.ProductId.ToString(),
                    Quantity = item.Quantity
                });
            }

            var response = await _client.ReleaseStockAsync(request, cancellationToken: ct);
            return response.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call Catalog gRPC ReleaseStock for OrderId={OrderId}", orderId);
            return false;
        }
    }
}
