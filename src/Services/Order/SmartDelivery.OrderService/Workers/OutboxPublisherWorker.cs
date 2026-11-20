using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDelivery.OrderService.Infrastructure.Data;
using SmartDelivery.Shared.Events;
using SmartDelivery.Shared.Messaging;

namespace SmartDelivery.OrderService.Workers;

public class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IRabbitMqPublisher _publisher;
    private readonly ILogger<OutboxPublisherWorker> _logger;

    public OutboxPublisherWorker(
        IServiceProvider serviceProvider,
        IRabbitMqPublisher publisher,
        ILogger<OutboxPublisherWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisherWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred processing outbox messages");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        var pendingMessages = await db.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(20)
            .ToListAsync(ct);

        if (pendingMessages.Count == 0) return;

        _logger.LogInformation("Found {Count} pending outbox messages to publish", pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            try
            {
                if (message.Type == nameof(OrderPaidIntegrationEvent))
                {
                    var eventPayload = JsonSerializer.Deserialize<OrderPaidIntegrationEvent>(message.Content);
                    if (eventPayload != null)
                    {
                        await _publisher.PublishAsync("order.paid", eventPayload, ct);
                    }
                }
                else
                {
                    _logger.LogWarning("Unknown outbox event type: {Type}", message.Type);
                }

                message.MarkProcessed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish outbox message {Id}", message.Id);
                message.MarkFailed(ex.Message);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
