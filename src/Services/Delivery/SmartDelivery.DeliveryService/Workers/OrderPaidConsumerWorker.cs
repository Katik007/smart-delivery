using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SmartDelivery.DeliveryService.Domain;
using SmartDelivery.DeliveryService.Infrastructure.Data;
using SmartDelivery.Shared.Events;
using SmartDelivery.Shared.Messaging;

namespace SmartDelivery.DeliveryService.Workers;

public class OrderPaidConsumerWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<OrderPaidConsumerWorker> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public OrderPaidConsumerWorker(
        IServiceProvider serviceProvider,
        IOptions<RabbitMqOptions> options,
        ILogger<OrderPaidConsumerWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderPaidConsumerWorker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_channel == null || !_channel.IsOpen)
                {
                    TryInitializeRabbitMq();
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("RabbitMQ consumer error or reconnecting: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private void TryInitializeRabbitMq()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
                DispatchConsumersAsync = true
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.ExchangeDeclare(
                exchange: _options.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            var queueName = "smartdelivery.delivery.orderpaid";
            _channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false);

            _channel.QueueBind(
                queue: queueName,
                exchange: _options.ExchangeName,
                routingKey: "order.paid");

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);

                try
                {
                    var orderPaidEvent = JsonSerializer.Deserialize<OrderPaidIntegrationEvent>(json);
                    if (orderPaidEvent != null)
                    {
                        await ProcessOrderPaidEventAsync(orderPaidEvent, json);
                    }

                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing OrderPaid message from queue");
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            _channel.BasicConsume(
                queue: queueName,
                autoAck: false,
                consumer: consumer);

            _logger.LogInformation("Successfully connected to RabbitMQ and listening on queue '{Queue}'", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Cannot connect to RabbitMQ broker yet ({Host}:{Port}): {Message}", _options.Host, _options.Port, ex.Message);
        }
    }

    private async Task ProcessOrderPaidEventAsync(OrderPaidIntegrationEvent evt, string payload)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();

        // STEP 1: Transactional Inbox pattern check for idempotency
        var alreadyProcessed = await db.InboxMessages.AnyAsync(m => m.Id == evt.EventId);
        if (alreadyProcessed)
        {
            _logger.LogInformation("Event {EventId} for OrderId={OrderId} already in Inbox. Skipping duplicate.", evt.EventId, evt.OrderId);
            return;
        }

        // STEP 2: Record in Inbox
        var inboxMessage = new InboxMessage(evt.EventId, nameof(OrderPaidIntegrationEvent), payload);
        db.InboxMessages.Add(inboxMessage);

        // STEP 3: Create DeliveryTask
        var task = new DeliveryTask(
            Guid.NewGuid(),
            evt.OrderId,
            evt.CustomerId,
            evt.DeliveryAddress,
            evt.TotalAmount
        );

        // Try auto-assigning an available courier
        var availableCourier = await db.Couriers.FirstOrDefaultAsync(c => c.Status == CourierStatus.Available);
        if (availableCourier != null)
        {
            task.AssignCourier(availableCourier.Id);
            availableCourier.SetStatus(CourierStatus.Busy);
            _logger.LogInformation("Assigned DeliveryTask {TaskId} to Courier {CourierName}", task.Id, availableCourier.FullName);
        }

        db.DeliveryTasks.Add(task);
        await db.SaveChangesAsync();

        _logger.LogInformation("Processed OrderPaidEvent for OrderId={OrderId}. DeliveryTask {TaskId} created in status {Status}.",
            evt.OrderId, task.Id, task.Status);
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
