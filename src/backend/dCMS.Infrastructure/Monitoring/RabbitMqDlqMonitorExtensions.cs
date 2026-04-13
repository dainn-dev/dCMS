using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Prometheus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace dCMS.Infrastructure.Monitoring;

public static class RabbitMqDlqMonitorExtensions
{
    public const string HttpClientName = "rabbitmq-management";

    /// <summary>
    /// Registers RabbitMQ management DLQ polling (DAI-361). No-ops when <c>RabbitMq:ManagementBaseUrl</c> is empty.
    /// Call <see cref="MapDcmsPrometheusMetrics"/> on the app for <c>/metrics</c>.
    /// </summary>
    public static IServiceCollection AddRabbitMqDlqMonitoring(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        var section = configuration.GetSection("RabbitMq");
        var opts = new RabbitMqDlqMonitorOptions
        {
            ManagementBaseUrl = section["ManagementBaseUrl"]?.Trim() ?? "",
            User = section["User"] ?? "guest",
            Password = section["Pass"] ?? section["Password"] ?? "guest",
            VirtualHost = section["Vhost"]?.Trim() ?? "/",
            ServiceName = serviceName.Trim(),
            SlackWebhookUrl = configuration["Slack:DlqWebhookUrl"]?.Trim(),
        };
        if (int.TryParse(section["DlqPollSeconds"], out var sec) && sec > 0)
            opts.PollInterval = TimeSpan.FromSeconds(sec);

        services.TryAddSingleton(opts);
        services.AddHttpClient();
        services.AddHttpClient(HttpClientName, client =>
        {
            if (!string.IsNullOrWhiteSpace(opts.ManagementBaseUrl))
                client.BaseAddress = new Uri(opts.ManagementBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHostedService<RabbitMqDlqDepthMonitorHostedService>();
        return services;
    }

    public static WebApplication MapDcmsPrometheusMetrics(this WebApplication app)
    {
        app.MapMetrics().AllowAnonymous();
        return app;
    }
}
