using System.Threading.RateLimiting;
using dCMS.Infrastructure.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace dCMS.Tests.Unit.Infrastructure;

public sealed class DcmsWebHostExtensionsTests
{
    [Fact]
    public async Task AddDcmsCors_registers_configured_allowed_origin()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "https://admin.example.test",
        }).Build();

        services.AddDcmsCors(configuration);
        using var provider = services.BuildServiceProvider();

        var policy = await provider
            .GetRequiredService<ICorsPolicyProvider>()
            .GetPolicyAsync(new DefaultHttpContext(), DcmsWebHostDefaults.CorsPolicyName);

        policy.Should().NotBeNull();
        policy!.Origins.Should().Contain("https://admin.example.test");
        policy.Origins.Should().NotContain("https://evil.example.test");
    }

    [Fact]
    public async Task UseDcmsCorrelationId_propagates_existing_header()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        app.UseDcmsCorrelationId();
        app.Run(_ => Task.CompletedTask);
        var pipeline = app.Build();

        var context = new DefaultHttpContext();
        context.Request.Headers[DcmsWebHostDefaults.CorrelationIdHeaderName] = "corr-123";

        await pipeline(context);
        await context.Response.StartAsync();

        context.TraceIdentifier.Should().Be("corr-123");
        context.Request.Headers[DcmsWebHostDefaults.CorrelationIdHeaderName].ToString().Should().Be("corr-123");
        context.Response.Headers[DcmsWebHostDefaults.CorrelationIdHeaderName].ToString().Should().Be("corr-123");
    }

    [Fact]
    public async Task AddDcmsTenantPlanRateLimiting_throttles_tenant_partition()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:PermitLimit"] = "1",
            ["RateLimiting:WindowSeconds"] = "60",
        }).Build();

        services.AddDcmsTenantPlanRateLimiting(configuration);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;
        var context = new DefaultHttpContext
        {
            RequestServices = provider,
        };
        context.Request.RouteValues["tenantId"] = "tenant-a";

        using var first = await options.GlobalLimiter!.AcquireAsync(context, permitCount: 1);
        using var second = await options.GlobalLimiter!.AcquireAsync(context, permitCount: 1);

        first.IsAcquired.Should().BeTrue();
        second.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public async Task UseDcmsRequestObservability_logs_correlation_tenant_and_store_labels()
    {
        var logProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection()
            .AddLogging(builder => builder.AddProvider(logProvider))
            .BuildServiceProvider();

        var app = new ApplicationBuilder(services);
        app.UseDcmsCorrelationId();
        app.UseDcmsRequestObservability("gateway");
        app.Run(context =>
        {
            context.Request.RouteValues["tenantId"] = "tenant-a";
            context.Request.RouteValues["storeId"] = "store-b";
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = "/api/v1/tenants/tenant-a/stores/store-b/products";
        context.Request.Headers[DcmsWebHostDefaults.CorrelationIdHeaderName] = "corr-observe";

        await app.Build()(context);

        var entry = logProvider.Entries.Should().ContainSingle(e => e.Category == "dCMS.Observability.Http").Subject;
        entry.Properties.Should().ContainKey("Service").WhoseValue.Should().Be("gateway");
        entry.Properties.Should().ContainKey("CorrelationId").WhoseValue.Should().Be("corr-observe");
        entry.Properties.Should().ContainKey("TenantId").WhoseValue.Should().Be("tenant-a");
        entry.Properties.Should().ContainKey("StoreId").WhoseValue.Should().Be("store-b");
        entry.Properties.Should().ContainKey("FailureReason").WhoseValue.Should().Be("none");
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<LogEntry> _entries = [];

        public IReadOnlyList<LogEntry> Entries => _entries;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(string category, List<LogEntry> entries) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state as IEnumerable<KeyValuePair<string, object?>>
                ?? Enumerable.Empty<KeyValuePair<string, object?>>();

            entries.Add(new LogEntry(
                category,
                properties
                    .Where(p => p.Key != "{OriginalFormat}")
                    .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal)));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed record LogEntry(string Category, IReadOnlyDictionary<string, object?> Properties);
}
