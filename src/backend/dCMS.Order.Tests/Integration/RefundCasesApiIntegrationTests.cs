using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace dCMS.Order.Tests.Integration;

/// <summary>
/// DAI-655 — integration tests for refund cases endpoints (Auth disabled in tests).
///
/// Endpoints:
///   GET   /api/refund-cases
///   GET   /api/orders/{orderId}/refund-case
///   PUT   /api/orders/{orderId}/refund-case
///   PATCH /api/refund-cases/{orderId} (legacy; same body as PUT)
///
/// Refund case definition: Order.Status in cancelled family (Cancelled, AdminCancelled, UserCancelled),
/// PaymentIntentId IS NOT NULL, and latest *qualifying* PaymentTransaction (by CreatedAt among
/// succeeded, refunded, initiated, completed) exists for the order.
/// </summary>
[CollectionDefinition("RefundCasesApi", DisableParallelization = true)]
public sealed class RefundCasesApiCollection : ICollectionFixture<RefundCasesApiFixture>
{
}

[Collection("RefundCasesApi")]
public sealed class RefundCasesApiIntegrationTests(RefundCasesApiFixture fx)
{
    [Fact]
    public async Task ListRefundCases_requires_tenant_store_headers()
    {
        var client = fx.Factory.CreateClient();
        var res = await client.GetAsync("/api/refund-cases");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ListRefundCases_returns_only_cancelled_orders_with_payments()
    {
        // Cancelled + has payment → included
        var include = Guid.NewGuid();
        await fx.SeedOrderAsync(include, status: "Cancelled", paymentIntentId: "pi_inc");
        await fx.SeedPaymentTxnAsync(include, status: "succeeded", amount: 99.99m, currency: "USD", method: "card");

        // Cancelled but no payment → excluded by query (PaymentIntentId IS NULL)
        var noPayment = Guid.NewGuid();
        await fx.SeedOrderAsync(noPayment, status: "Cancelled", paymentIntentId: null);

        // Active order → excluded
        var active = Guid.NewGuid();
        await fx.SeedOrderAsync(active, status: "Confirmed", paymentIntentId: "pi_active");
        await fx.SeedPaymentTxnAsync(active, status: "succeeded", amount: 50m, currency: "USD", method: "card");

        var client = fx.Factory.CreateClient();
        fx.AddHeaders(client);
        var res = await client.GetAsync("/api/refund-cases?limit=20");
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"Status={(int)res.StatusCode} Body={body}");

        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        var items = data.GetProperty("items").EnumerateArray().ToList();

        Assert.Contains(items, e => e.GetProperty("orderId").GetString() == include.ToString());
        Assert.DoesNotContain(items, e => e.GetProperty("orderId").GetString() == noPayment.ToString());
        Assert.DoesNotContain(items, e => e.GetProperty("orderId").GetString() == active.ToString());
    }

    [Fact]
    public async Task ListRefundCases_excludes_cancelled_order_when_latest_payment_is_failed()
    {
        var oid = Guid.NewGuid();
        await fx.SeedOrderAsync(oid, status: "Cancelled", paymentIntentId: "pi_fail");
        await fx.SeedPaymentTxnAsync(oid, status: "failed");

        var client = fx.Factory.CreateClient();
        fx.AddHeaders(client);
        var res = await client.GetAsync("/api/refund-cases?limit=20");
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var ids = doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("orderId").GetString()).ToList();
        Assert.DoesNotContain(oid.ToString(), ids);
    }

    [Fact]
    public async Task ListRefundCases_uses_latest_qualifying_payment_when_newer_txn_is_failed()
    {
        var oid = Guid.NewGuid();
        var t = DateTimeOffset.UtcNow;
        await fx.SeedOrderAsync(oid, status: "Cancelled", paymentIntentId: "pi_qual");
        // Older qualifying row vs newer failed row — list must use latest *qualifying* (succeeded), not the failed row's amount.
        await fx.SeedPaymentTxnAsync(oid, status: "succeeded", amount: 10m, createdAt: t.AddHours(-2));
        await fx.SeedPaymentTxnAsync(oid, status: "failed", amount: 99m, createdAt: t);

        var client = fx.Factory.CreateClient();
        fx.AddHeaders(client);
        var res = await client.GetAsync("/api/refund-cases?limit=20");
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var matches = doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Where(e => e.GetProperty("orderId").GetString() == oid.ToString()).ToList();
        Assert.Single(matches);
        Assert.Equal(10m, matches[0].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task ListRefundCases_isolates_by_tenant_store()
    {
        var mine = Guid.NewGuid();
        await fx.SeedOrderAsync(mine, status: "Cancelled", paymentIntentId: "pi_mine");
        await fx.SeedPaymentTxnAsync(mine, status: "succeeded");

        var foreign = Guid.NewGuid();
        await fx.SeedOrderAsync(foreign, status: "Cancelled", paymentIntentId: "pi_other",
            tenantId: "other-tenant", storeId: "other-store");
        await fx.SeedPaymentTxnAsync(foreign, status: "succeeded",
            tenantId: Guid.Parse("00000000-0000-0000-0000-000000000099"),
            storeId: Guid.Parse("00000000-0000-0000-0000-000000000098"));

        var client = fx.Factory.CreateClient();
        fx.AddHeaders(client);
        var res = await client.GetAsync("/api/refund-cases?limit=50");
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().Select(e => e.GetProperty("orderId").GetString()).ToList();

        Assert.Contains(mine.ToString(), items);
        Assert.DoesNotContain(foreign.ToString(), items);
    }

    [Fact]
    public async Task ListRefundCases_pagination_returns_nextCursor_and_advances()
    {
        // Seed 3 cancelled orders with payments at distinct timestamps (DESC order by CreatedAt).
        var t = DateTimeOffset.UtcNow;
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        for (int i = 0; i < ids.Length; i++)
        {
            await fx.SeedOrderAsync(ids[i], status: "Cancelled", paymentIntentId: $"pi_p_{i}",
                createdAt: t.AddSeconds(-i));
            await fx.SeedPaymentTxnAsync(ids[i], status: "succeeded");
        }

        var client = fx.Factory.CreateClient();
        fx.AddHeaders(client);

        var res1 = await client.GetAsync("/api/refund-cases?limit=2");
        res1.EnsureSuccessStatusCode();
        using var doc1 = JsonDocument.Parse(await res1.Content.ReadAsStringAsync());
        var data1 = doc1.RootElement.GetProperty("data");
        Assert.Equal(2, data1.GetProperty("items").GetArrayLength());

        var nextCursor = data1.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(nextCursor));

        var res2 = await client.GetAsync($"/api/refund-cases?limit=2&cursor={Uri.EscapeDataString(nextCursor!)}");
        res2.EnsureSuccessStatusCode();
        using var doc2 = JsonDocument.Parse(await res2.Content.ReadAsStringAsync());
        var page2 = doc2.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().Select(e => e.GetProperty("orderId").GetString()).ToList();

        var page1 = data1.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("orderId").GetString()).ToList();
        Assert.False(page2.Any(x => page1.Contains(x)), "page2 must not overlap page1");
    }

    [Fact]
    public async Task UpdateRefundCase_updates_status_and_remark()
    {
        var oid = Guid.NewGuid();
        await fx.SeedOrderAsync(oid, status: "Cancelled", paymentIntentId: "pi_upd");
        await fx.SeedPaymentTxnAsync(oid, status: "succeeded");

        var client = fx.Factory.CreateClient();
        fx.AddHeaders(client);

        var res = await client.PutAsJsonAsync($"/api/orders/{oid:D}/refund-case",
            new { status = "Processing", remark = "contacted gateway" });
        res.EnsureSuccessStatusCode();

        var getRes = await client.GetAsync($"/api/orders/{oid:D}/refund-case");
        getRes.EnsureSuccessStatusCode();
        using (var doc = JsonDocument.Parse(await getRes.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Processing",
                doc.RootElement.GetProperty("data").GetProperty("status").GetString());
        }

        // verify columns persisted
        await using var conn = new NpgsqlConnection(fx.OrderConnectionString);
        var row = await conn.QuerySingleAsync<(string? RefundStatus, string RefundRemark)>(
            """SELECT "RefundStatus", "RefundRemark" FROM "Orders" WHERE "Id" = @Id""",
            new { Id = oid });
        Assert.Equal("Processing", row.RefundStatus);
        Assert.Equal("contacted gateway", row.RefundRemark);
    }

    [Fact]
    public async Task ListRefundCases_returns_empty_for_unused_tenant_store()
    {
        var client = fx.Factory.CreateClient();
        client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        client.DefaultRequestHeaders.Remove("X-Store-Id");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "t-no-rows");
        client.DefaultRequestHeaders.Add("X-Store-Id", "s-no-rows");

        var res = await client.GetAsync("/api/refund-cases?limit=20");
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"Status={(int)res.StatusCode} Body={body}");
        using var doc = JsonDocument.Parse(body);
        var items = doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.Empty(items);
    }

    [Fact]
    public async Task UpdateRefundCase_rejects_invalid_status()
    {
        var oid = Guid.NewGuid();
        await fx.SeedOrderAsync(oid, status: "Cancelled", paymentIntentId: "pi_badstat");
        await fx.SeedPaymentTxnAsync(oid, status: "succeeded");

        var client = fx.Factory.CreateClient();
        fx.AddHeaders(client);

        var res = await client.PutAsJsonAsync($"/api/orders/{oid:D}/refund-case",
            new { status = "NotAStatus", remark = "" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task UpdateRefundCase_rejects_invalid_orderId()
    {
        var client = fx.Factory.CreateClient();
        fx.AddHeaders(client);

        var res = await client.PutAsJsonAsync("/api/orders/not-a-uuid/refund-case",
            new { status = "Processing", remark = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task GetRefundCaseForOrder_returns_detail()
    {
        var oid = Guid.NewGuid();
        await fx.SeedOrderAsync(oid, status: "Cancelled", paymentIntentId: "pi_get");
        await fx.SeedPaymentTxnAsync(oid, status: "succeeded");

        var client = fx.Factory.CreateClient();
        fx.AddHeaders(client);
        var res = await client.GetAsync($"/api/orders/{oid:D}/refund-case");
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(oid.ToString(), doc.RootElement.GetProperty("data").GetProperty("orderId").GetString());
    }

    [Fact]
    public async Task UpdateRefundCase_no_op_for_non_cancelled_order_returns_no_match()
    {
        var oid = Guid.NewGuid();
        await fx.SeedOrderAsync(oid, status: "Confirmed", paymentIntentId: "pi_x");

        var client = fx.Factory.CreateClient();
        fx.AddHeaders(client);

        var res = await client.PutAsJsonAsync($"/api/orders/{oid:D}/refund-case",
            new { status = "Processing", remark = "should not apply" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);

        await using var conn = new NpgsqlConnection(fx.OrderConnectionString);
        var status = await conn.QuerySingleOrDefaultAsync<string?>(
            """SELECT "RefundStatus" FROM "Orders" WHERE "Id" = @Id""",
            new { Id = oid });
        Assert.Null(status);
    }
}

public sealed class RefundCasesApiFixture : IAsyncLifetime
{
    public const string TenantId = "t-refund";
    public const string StoreId = "s-refund";

    // Payment service stores TenantId/StoreId as Guid; map our string ids to fixed Guids for tests.
    public static readonly Guid TenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid StoreGuid = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid CustomerGuid = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private readonly PostgreSqlContainer _orderPg = new PostgreSqlBuilder()
        .WithDatabase("dcms_order")
        .WithUsername("dcms")
        .WithPassword("Your_password123")
        .Build();

    private readonly PostgreSqlContainer _paymentPg = new PostgreSqlBuilder()
        .WithDatabase("dcms_payment")
        .WithUsername("dcms")
        .WithPassword("Your_password123")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder().Build();

    private WebApplicationFactory<Program>? _factory;
    public WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException("Fixture not initialized.");

    public string OrderConnectionString => _orderPg.GetConnectionString();
    public string PaymentConnectionString => _paymentPg.GetConnectionString();
    public string RedisConnectionString => _redis.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_orderPg.StartAsync(), _paymentPg.StartAsync(), _redis.StartAsync());

        // Minimal Orders schema (matches migration 001 + 012).
        await using (var conn = new NpgsqlConnection(OrderConnectionString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS "Orders" (
                    "Id"               UUID         NOT NULL PRIMARY KEY DEFAULT gen_random_uuid(),
                    "TenantId"         VARCHAR(64)  NOT NULL,
                    "StoreId"          VARCHAR(64)  NOT NULL,
                    "CustomerId"       VARCHAR(64)  NOT NULL,
                    "Status"           VARCHAR(32)  NOT NULL,
                    "Currency"         VARCHAR(8)   NOT NULL DEFAULT 'USD',
                    "SubTotal"         NUMERIC(18,4) NOT NULL DEFAULT 0,
                    "TaxTotal"         NUMERIC(18,4) NOT NULL DEFAULT 0,
                    "Total"            NUMERIC(18,4) NOT NULL DEFAULT 0,
                    "PaymentIntentId"  VARCHAR(128) NULL,
                    "IdempotencyKey"   VARCHAR(128) NOT NULL,
                    "CreatedAt"        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    "UpdatedAt"        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    "RefundStatus"     VARCHAR(32)  NULL,
                    "RefundRemark"     VARCHAR(512) NOT NULL DEFAULT '',
                    "RefundedAt"       TIMESTAMPTZ  NULL
                );
                """);
        }

        // Minimal PaymentTransactions schema (matches Payment service migration).
        await using (var conn = new NpgsqlConnection(PaymentConnectionString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS "PaymentTransactions" (
                    "Id"              UUID         NOT NULL PRIMARY KEY,
                    "OrderId"         UUID         NOT NULL,
                    "TenantId"        UUID         NOT NULL,
                    "StoreId"         UUID         NOT NULL,
                    "CustomerId"      VARCHAR(64)  NOT NULL,
                    "PaymentMethod"   VARCHAR(32)  NOT NULL,
                    "PaymentIntentId" VARCHAR(128) NOT NULL,
                    "Amount"          NUMERIC(18,4) NOT NULL,
                    "Currency"        VARCHAR(8)   NOT NULL,
                    "Status"          VARCHAR(32)  NOT NULL,
                    "Provider"        VARCHAR(32)  NOT NULL,
                    "CreatedAt"       TIMESTAMPTZ  NOT NULL DEFAULT NOW()
                );
                CREATE INDEX IF NOT EXISTS "IX_PaymentTransactions_OrderId" ON "PaymentTransactions" ("OrderId");
                """);
        }

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("Auth:Enabled", "false");
                b.ConfigureAppConfiguration((_, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Order"] = OrderConnectionString,
                        ["ConnectionStrings:Payment"] = PaymentConnectionString,
                        ["ConnectionStrings:Redis"] = RedisConnectionString,
                        ["Payment:BaseUrl"] = "http://payment.test/",
                        ["Inventory:BaseUrl"] = "http://inventory.test/",
                        ["RabbitMq:Host"] = "localhost",
                    });
                });
            });
    }

    public async Task DisposeAsync()
    {
        try { _factory?.Dispose(); } catch { /* ignore */ }
        try { await _redis.DisposeAsync(); } catch { /* ignore */ }
        try { await _paymentPg.DisposeAsync(); } catch { /* ignore */ }
        try { await _orderPg.DisposeAsync(); } catch { /* ignore */ }
    }

    public void AddHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        client.DefaultRequestHeaders.Remove("X-Store-Id");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TenantId);
        client.DefaultRequestHeaders.Add("X-Store-Id", StoreId);
    }

    public async Task SeedOrderAsync(
        Guid orderId,
        string status,
        string? paymentIntentId,
        string? tenantId = null,
        string? storeId = null,
        decimal total = 0m,
        string currency = "USD",
        DateTimeOffset? createdAt = null)
    {
        await using var conn = new NpgsqlConnection(OrderConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync("""
            INSERT INTO "Orders"
                ("Id","TenantId","StoreId","CustomerId","Status","Currency","Total","PaymentIntentId","IdempotencyKey","CreatedAt","UpdatedAt")
            VALUES
                (@Id,@TenantId,@StoreId,@CustomerId,@Status,@Currency,@Total,@PaymentIntentId,@Idem,@Now,@Now)
            ON CONFLICT ("Id") DO NOTHING
            """, new
        {
            Id = orderId,
            TenantId = tenantId ?? TenantId,
            StoreId = storeId ?? StoreId,
            CustomerId = "cust-1",
            Status = status,
            Currency = currency,
            Total = total,
            PaymentIntentId = paymentIntentId,
            Idem = $"idem-{orderId:N}",
            Now = createdAt ?? DateTimeOffset.UtcNow
        });
    }

    public async Task SeedPaymentTxnAsync(
        Guid orderId,
        string status,
        decimal amount = 0m,
        string currency = "USD",
        string method = "card",
        Guid? tenantId = null,
        Guid? storeId = null,
        DateTimeOffset? createdAt = null)
    {
        await using var conn = new NpgsqlConnection(PaymentConnectionString);
        await conn.OpenAsync();
        var at = createdAt ?? DateTimeOffset.UtcNow;
        await conn.ExecuteAsync("""
            INSERT INTO "PaymentTransactions"
                ("Id","OrderId","TenantId","StoreId","CustomerId","PaymentMethod","PaymentIntentId","Amount","Currency","Status","Provider","CreatedAt")
            VALUES
                (@Id,@OrderId,@TenantId,@StoreId,@CustomerId,@Method,@Pi,@Amount,@Currency,@Status,@Provider,@CreatedAt)
            """, new
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            TenantId = tenantId ?? TenantGuid,
            StoreId = storeId ?? StoreGuid,
            CustomerId = "cust-1",
            Method = method,
            Pi = $"pi_{orderId:N}",
            Amount = amount,
            Currency = currency,
            Status = status,
            Provider = "stub",
            CreatedAt = at.UtcDateTime
        });
    }
}
