using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace SmartDelivery.Shared.Messaging;

public interface IRabbitMqPublisher
{
    Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default);
}

public class RabbitMqPublisher : IRabbitMqPublisher
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new();

    public RabbitMqPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private void EnsureChannel()
    {
        if (_channel != null && _channel.IsOpen) return;

        lock (_lock)
        {
            if (_channel != null && _channel.IsOpen) return;

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

                // Declare main exchange
                _channel.ExchangeDeclare(
                    exchange: _options.ExchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    arguments: null);

                _logger.LogInformation("Connected to RabbitMQ on {Host}:{Port}, exchange '{Exchange}' ready", _options.Host, _options.Port, _options.ExchangeName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to RabbitMQ broker at {Host}:{Port}", _options.Host, _options.Port);
                _channel = null;
                _connection = null;
            }
        }
    }

    public Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default)
    {
        EnsureChannel();

        if (_channel == null || !_channel.IsOpen)
        {
            _logger.LogWarning("Cannot publish event with key {RoutingKey}: RabbitMQ channel is unavailable", routingKey);
            return Task.CompletedTask;
        }

        try
        {
            var json = JsonSerializer.Serialize(message);
            var body = System.Text.Encoding.UTF8.GetBytes(json);

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Type = typeof(T).Name;

            lock (_lock)
            {
                _channel.BasicPublish(
                    exchange: _options.ExchangeName,
                    routingKey: routingKey,
                    basicProperties: properties,
                    body: body);
            }

            _logger.LogInformation("Published event {Type} with routingKey '{RoutingKey}' to exchange '{Exchange}'", typeof(T).Name, routingKey, _options.ExchangeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing event {Type} to RabbitMQ", typeof(T).Name);
        }

        return Task.CompletedTask;
    }
}
