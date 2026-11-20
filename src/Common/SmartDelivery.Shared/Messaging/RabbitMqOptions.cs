namespace SmartDelivery.Shared.Messaging;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string ExchangeName { get; set; } = "smartdelivery.exchange";
    public string OrderPaidQueue { get; set; } = "smartdelivery.orders.paid";
    public string OrderCancelledQueue { get; set; } = "smartdelivery.orders.cancelled";
}
