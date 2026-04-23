using System.Net;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace dCMS.Order.Tests.Integration;

/// <summary>DAI-632 — Failed Orders REST endpoints (Auth disabled in tests).</summary>
[CollectionDefinition("OrderFailedApi", DisableParallelization = true)]
public sealed class OrderFailedApiCollection : ICollectionFixture<OrderFailedApiFixture>
{
}

[Collection("OrderFailedApi")]
public sealed class OrderFailedApiIntegrationTests(OrderFailedApiFixture fx)
{
    [Fact]
    public async Task ListFailed_requires_tenant_store_headers()
    {
        var client = fx.Factory.CreateClient();
        var res = await client.GetAsync("/api/orders/failed");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ListFailed_returns_items_and_nextCursor()
    {
        var o1 = Guid.NewGuid();
        var o2 = Guid.NewGuid();
        await fx.SeedFailureAsync(o1, failedAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        await fx.SeedFailureAsync(o2, failedAt: DateTimeOffset.UtcNow.AddMinutes(-2));

        var client = fx.Factory.CreateClient();
        fx.AddHeaders(client);

        var res = await client.GetAsync("/api/orders/failed?limit=1");
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("data").GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.True(doc.RootElement.GetProperty("meta").GetProperty("nextCursor").GetString()?.Length > 0);
    }

    [Fact]
    public async Task Retry_increments_retryCount_and_idempotency_skips_second_call()
    {
        var oid = Guid.NewGuid();
        await fx.SeedFailureAsync(oid, failedAt: DateTimeOffset.UtcNow);

        var client = fx.Factory.CreateClient();
        fx.AddHeaders(client);

        var idem = "k1";
        var body = JsonSerializer.Serialize(new { kind = "PaymentFailed", idempotencyKey = idem });
        var res1 = await client.PostAsync($"/api/orders/failed/{oid:D}/retry",
            new StringContent(body, Encoding.UTF8, "application/json"));
        res1.EnsureSuccessStatusCode();

        // Ensure idempotency marker exists in Redis
        await using (var mux = await ConnectionMultiplexer.ConnectAsync(fx.RedisConnectionString))
        {
            var key = $"dcms:order-failure:retry:{OrderFailedApiFixture.TenantId}:{OrderFailedApiFixture.StoreId}:{oid:D}:{idem}";
            var v = await mux.GetDatabase().StringGetAsync(key);
            Assert.True(v.HasValue);
        }

        var res2 = await client.PostAsync($"/api/orders/failed/{oid:D}/retry",
            new StringContent(body, Encoding.UTF8, "application/json"));
        res2.EnsureSuccessStatusCode();

        // verify retry count == 1
        await using var conn = new NpgsqlConnection(fx.PostgresConnectionString);
        var rc = await conn.ExecuteScalarAsync<int>(
            "SELECT \"RetryCount\" FROM \"OrderFailures\" WHERE \"OrderId\"=@Id",
            new { Id = oid });
        Assert.Equal(1, rc);
    }

    [Fact]
    public async Task Resolve_marks_row_resolved_and_hides_from_list()
    {
        var oid = Guid.NewGuid();
        await fx.SeedFailureAsync(oid, failedAt: DateTimeOffset.UtcNow);

        var client = fx.Factory.CreateClient();
        fx.AddHeaders(client);

        var res = await client.PostAsync($"/api/orders/failed/{oid:D}/resolve",
            new StringContent("{\"note\":\"ok\"}", Encoding.UTF8, "application/json"));
        res.EnsureSuccessStatusCode();

        var list = await client.GetAsync("/api/orders/failed?limit=50");
        list.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("data").GetProperty("items");
        Assert.DoesNotContain(items.EnumerateArray(), x => x.GetProperty("orderId").GetString() == oid.ToString("D"));
    }
}

public sealed class OrderFailedApiFixture : IAsyncLifetime
{
    public const string TenantId = "t-test";
    public const string StoreId = "s-test";

    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithDatabase("dcms_order")
        .WithUsername("dcms")
        .WithPassword("Your_password123")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder().Build();

    private WebApplicationFactory<Program>? _factory;
    public WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException("Fixture not initialized.");

    public string PostgresConnectionString => _pg.GetConnectionString();
    public string RedisConnectionString => _redis.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        await _redis.StartAsync();

        // run order migrations by invoking the upgrader via hosted service is complex; we just execute the needed SQL here.
        await using var conn = new NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();
        // minimal table for OrderFailures (matches migration 010)
        await conn.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS "OrderFailures" (
                "OrderId" UUID PRIMARY KEY,
                "TenantId" VARCHAR(64) NOT NULL,
                "StoreId" VARCHAR(64) NOT NULL,
                "FailureStatus" VARCHAR(32) NOT NULL,
                "FailureReason" TEXT NOT NULL,
                "FailureErrorCode" VARCHAR(64) NULL,
                "SourceEventId" VARCHAR(128) NULL,
                "FailedAt" TIMESTAMPTZ NOT NULL,
                "RetryCount" INT NOT NULL DEFAULT 0,
                "LastRetryAt" TIMESTAMPTZ NULL,
                "ResolvedAt" TIMESTAMPTZ NULL,
                "ResolvedBy" VARCHAR(128) NULL,
                "LogJson" JSONB NOT NULL DEFAULT '[]'::jsonb
            );
            CREATE INDEX IF NOT EXISTS "IX_OrderFailures_Tenant_Store_Status_FailedAt"
                ON "OrderFailures" ("TenantId", "StoreId", "FailureStatus", "FailedAt" DESC);
            """);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("Auth:Enabled", "false");
                b.ConfigureAppConfiguration((_, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Order"] = PostgresConnectionString,
                        ["ConnectionStrings:Redis"] = RedisConnectionString,
                        ["Payment:BaseUrl"] = "http://payment.test/",
                        ["Inventory:BaseUrl"] = "http://inventory.test/",
                        ["RabbitMq:Host"] = "localhost",
                    });
                });
                b.ConfigureServices(services =>
                {
                    // remove hosted migrations/transport for test speed if present
                    // (not strictly necessary for these endpoints)
                });
            });

        // ensure redis is reachable
        await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);
    }

    public async Task DisposeAsync()
    {
        try { _factory?.Dispose(); } catch { /* ignore */ }
        try { await _redis.DisposeAsync(); } catch { /* ignore */ }
        try { await _pg.DisposeAsync(); } catch { /* ignore */ }
    }

    public void AddHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        client.DefaultRequestHeaders.Remove("X-Store-Id");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TenantId);
        client.DefaultRequestHeaders.Add("X-Store-Id", StoreId);
    }

    public async Task SeedFailureAsync(Guid orderId, DateTimeOffset failedAt)
    {
        await using var conn = new NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync("""
            INSERT INTO "OrderFailures"
            ("OrderId","TenantId","StoreId","FailureStatus","FailureReason","FailureErrorCode","SourceEventId","FailedAt","RetryCount","LastRetryAt","ResolvedAt","ResolvedBy","LogJson")
            VALUES
            (@OrderId,@TenantId,@StoreId,@Status,@Reason,NULL,NULL,@FailedAt,0,NULL,NULL,NULL,'[]'::jsonb)
            ON CONFLICT ("OrderId") DO UPDATE SET
                "FailureStatus"=EXCLUDED."FailureStatus",
                "FailureReason"=EXCLUDED."FailureReason",
                "FailedAt"=EXCLUDED."FailedAt",
                "ResolvedAt"=NULL,
                "ResolvedBy"=NULL;
            """, new
        {
            OrderId = orderId,
            TenantId,
            StoreId,
            Status = "PaymentFailed",
            Reason = "test",
            FailedAt = failedAt
        });
    }
}

