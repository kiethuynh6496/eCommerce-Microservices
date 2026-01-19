namespace Ecommerce.Contracts.Configuration;

/// <summary>
/// RabbitMQ connection configuration
/// </summary>
public class RabbitMQConfiguration
{
    public const string SectionName = "RabbitMQ";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Build connection string for RabbitMQ
    /// Format: amqp://username:password@host:port/virtualhost
    /// </summary>
    public string GetConnectionString()
    {
        return $"amqp://{Username}:{Password}@{Host}:{Port}{VirtualHost}";
    }

    /// <summary>
    /// Validate configuration
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Host)
            && Port > 0
            && !string.IsNullOrWhiteSpace(Username);
    }
}