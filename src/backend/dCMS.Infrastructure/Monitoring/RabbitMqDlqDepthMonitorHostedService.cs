using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace dCMS.Infrastructure.Monitoring;

/// <summary>
/// US-F4 / DAI-361 — polls RabbitMQ management API for <c>dlq.*</c> queues, updates <c>dlq_depth{service,queue}</c>, optional Slack.
/// </summary>
public sealed class RabbitMqDlqDepthMonitorHostedService : BackgroundService
{
    private static readonly Gauge DlqDepth = Metrics.CreateGauge(
        "dlq_depth",
        "Approximate depth of RabbitMQ queues whose names start with dlq.",
        new GaugeConfiguration { LabelNames = ["service", "queue"] });

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RabbitMqDlqMonitorOptions _options;
    private readonly ILogger<RabbitMqDlqDepthMonitorHostedService> _logger;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _slackCooldownUntil = new(StringComparer.Ordinal);
    private HashSet<string> _previousDlqQueues = new(StringComparer.Ordinal);

    public RabbitMqDlqDepthMonitorHostedService(
        IHttpClientFactory httpClientFactory,
        RabbitMqDlqMonitorOptions options,
        ILogger<RabbitMqDlqDepthMonitorHostedService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ManagementBaseUrl))
        {
            _logger.LogInformation("RabbitMQ DLQ monitor disabled (RabbitMq:ManagementBaseUrl empty).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "RabbitMQ DLQ monitor poll failed.");
            }

            try
            {
                await Task.Delay(_options.PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public static bool IsDlqQueueName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.StartsWith("dlq.", StringComparison.OrdinalIgnoreCase);

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var vhost = string.IsNullOrWhiteSpace(_options.VirtualHost) ? "/" : _options.VirtualHost;
        var baseUrl = _options.ManagementBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/queues/{Uri.EscapeDataString(vhost)}";

        var client = _httpClientFactory.CreateClient(RabbitMqDlqMonitorExtensions.HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        var token = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.User}:{_options.Password}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);

        using var resp = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var queues = await JsonSerializer
            .DeserializeAsync<List<RabbitQueueInfoDto>>(stream, JsonOpts, cancellationToken)
            .ConfigureAwait(false) ?? [];

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var q in queues)
        {
            if (string.IsNullOrWhiteSpace(q.Name) || !IsDlqQueueName(q.Name))
                continue;

            var depth = q.Messages;
            DlqDepth.WithLabels(_options.ServiceName, q.Name).Set(depth);
            seen.Add(q.Name);

            if (depth > 0 && !string.IsNullOrWhiteSpace(_options.SlackWebhookUrl))
                await MaybePostSlackAsync(q.Name, depth, cancellationToken).ConfigureAwait(false);
        }

        foreach (var prev in _previousDlqQueues)
        {
            if (!seen.Contains(prev))
                DlqDepth.WithLabels(_options.ServiceName, prev).Set(0);
        }

        _previousDlqQueues = seen;
    }

    private async Task MaybePostSlackAsync(string queue, long depth, CancellationToken cancellationToken)
    {
        var cooldownKey = $"{_options.ServiceName}:{queue}";
        var now = DateTimeOffset.UtcNow;
        if (_slackCooldownUntil.TryGetValue(cooldownKey, out var until) && now < until)
            return;

        _slackCooldownUntil[cooldownKey] = now.Add(_options.SlackAlertCooldown);

        try
        {
            var client = _httpClientFactory.CreateClient();
            var body = new
            {
                text = $"DLQ alert: {queue} has {depth} message(s) [{_options.ServiceName}]",
            };
            using var resp = await client
                .PostAsJsonAsync(_options.SlackWebhookUrl, body, cancellationToken)
                .ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Slack DLQ webhook post failed for queue {Queue}.", queue);
        }
    }

    private sealed class RabbitQueueInfoDto
    {
        public string? Name { get; set; }
        public long Messages { get; set; }
    }
}
