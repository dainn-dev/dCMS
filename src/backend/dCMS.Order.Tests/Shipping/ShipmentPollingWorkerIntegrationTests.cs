using System.Net;
using System.Text.Json;
using Dapper;
using dCMS.Order.Infrastructure.Migrations;
using dCMS.Order.Infrastructure.Persistence;
using dCMS.Order.Infrastructure.Shipping;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Order.Tests.Shipping;

public sealed class ShipmentPollingWorkerIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private IHost? _carrierHost;
    private string? _carrierBaseUrl;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await _postgres.StartAsync();

        _carrierBaseUrl = await StartCarrierApiAsync();
    }

    public async Task DisposeAsync()
    {
        if (_carrierHost is not null)
            await _carrierHost.StopAsync();
        _carrierHost?.Dispose();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Polling_creates_ShipmentEvent_when_status_changed()
    {
        var cs = _postgres!.GetConnectionString();
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Order"] = cs,
            ["Shipment:Carriers:mock:TrackingBaseUrl"] = _carrierBaseUrl,
            ["Shipment:CarrierStatusMapping:mock:IN_TRANSIT"] = "in_transit",
            ["Shipment:Polling:BatchSize"] = "10",
            ["Shipment:Polling:StaleAfterMinutes"] = "60",
            ["Shipment:Polling:DefaultDelayMs"] = "0",
        }).Build();

        OrderDatabaseUpgrader.Run(cfg, NullLogger.Instance);

        var shipmentId = await SeedShipmentAsync(cs);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(cfg);
        services.AddLogging();
        services.AddSingleton<ShipmentPollingStore>();
        services.AddSingleton<ICarrierStatusMapper, ConfigCarrierStatusMapper>();
        services.AddHttpClient(nameof(HttpCarrierTrackingClient));
        services.AddSingleton<ICarrierTrackingClient, HttpCarrierTrackingClient>();
        services.AddSingleton<ShipmentPollingWorker>();

        await using var sp = services.BuildServiceProvider(true);
        var worker = sp.GetRequiredService<ShipmentPollingWorker>();

        await worker.PollOnceAsync(CancellationToken.None);

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        var count = await conn.ExecuteScalarAsync<int>(
            """SELECT COUNT(*)::int FROM "ShipmentEvents" WHERE "ShipmentId" = @Id""",
            new { Id = shipmentId });

        Assert.True(count >= 1);
    }

    private static async Task<Guid> SeedShipmentAsync(string cs)
    {
        var orderId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var stale = now.AddHours(-2);

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        await conn.ExecuteAsync(
            """
            INSERT INTO "Orders" ("Id","TenantId","StoreId","CustomerId","Status","Currency","SubTotal","TaxTotal","Total","IdempotencyKey","CreatedAt","UpdatedAt","ShippingAddress")
            VALUES (@Id,'t1','s1','c1','Shipped','USD',1,0,1,@Idem,@Now,@Now,'{}'::jsonb)
            """,
            new { Id = orderId, Idem = $"seed-{orderId:N}", Now = now.UtcDateTime });

        await conn.ExecuteAsync(
            """
            INSERT INTO "Shipments" ("Id","OrderId","Carrier","TrackingNumber","Status","CreatedAt","UpdatedAt")
            VALUES (@Id,@OrderId,'mock','TN-1','pending',@Now,@UpdatedAt)
            """,
            new { Id = shipmentId, OrderId = orderId, Now = now.UtcDateTime, UpdatedAt = stale.UtcDateTime });

        return shipmentId;
    }

    private async Task<string> StartCarrierApiAsync()
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(web =>
            {
                web.UseKestrel()
                    .UseUrls("http://127.0.0.1:0")
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/track/{trackingNumber}", async context =>
                            {
                                var trackingNumber = context.Request.RouteValues["trackingNumber"]?.ToString() ?? "";
                                var json = JsonSerializer.Serialize(new
                                {
                                    trackingNumber,
                                    status = "IN_TRANSIT",
                                    occurredAt = DateTimeOffset.UtcNow,
                                    location = "HCMC"
                                });
                                context.Response.StatusCode = (int)HttpStatusCode.OK;
                                context.Response.ContentType = "application/json";
                                await context.Response.WriteAsync(json);
                            });
                        });
                    });
            });

        _carrierHost = builder.Build();
        await _carrierHost.StartAsync();

        var addresses = _carrierHost.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
            .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!;
        var url = addresses.Addresses.First();

        return url.TrimEnd('/') + "/";
    }
}

