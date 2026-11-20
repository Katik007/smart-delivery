using SmartDelivery.Shared.Domain;

namespace SmartDelivery.OrderService.Infrastructure.Clients;

public record StockReservationItem(Guid ProductId, int Quantity);

public record StockReservationResult(bool Success, string Message, IReadOnlyList<string> UnavailableProductIds);

public interface IStockServiceClient
{
    Task<StockReservationResult> ReserveStockAsync(Guid orderId, IEnumerable<StockReservationItem> items, CancellationToken ct = default);
    Task<bool> ReleaseStockAsync(Guid orderId, IEnumerable<StockReservationItem> items, CancellationToken ct = default);
}
