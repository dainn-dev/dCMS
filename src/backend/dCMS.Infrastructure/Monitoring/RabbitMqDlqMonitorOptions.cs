namespace dCMS.Infrastructure.Monitoring;

public sealed class RabbitMqDlqMonitorOptions
{
    /// <summary>Base URL for management plugin, e.g. <c>http://localhost:15672</c>.</summary>
    public string ManagementBaseUrl { get; set; } = "";

    public string User { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    /// <summary>RabbitMQ vhost (default <c>/</c>).</summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>Value for Prometheus label <c>service</c> and Slack text.</summary>
    public string ServiceName { get; set; } = "unknown";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(60);

    public string? SlackWebhookUrl { get; set; }

    public TimeSpan SlackAlertCooldown { get; set; } = TimeSpan.FromMinutes(10);
}
