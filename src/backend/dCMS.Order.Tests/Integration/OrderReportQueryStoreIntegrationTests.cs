using Dapper;
using dCMS.Order.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Order.Tests.Integration;

/// <summary>
/// PR1 — direct integration tests for <see cref="OrderReportQueryStore"/>:
///  • cross-DB split (Order DB + Payment DB) — summary/ecommerce no longer JOIN PaymentTransactions in-DB.
///  • brandCode filter actually filters via OrderItems.VariantSnapshot->>'brandCode'.
///  • overview metrics (totals, denominators, top day) on a fixture.
///  • detail row enrichment (Campaigns / ItemPromoCodes / PaymentType / BillingCountry).
/// </summary>
[CollectionDefinition("OrderReportQueryStore", DisableParallelization = true)]
public sealed class OrderReportQueryStoreCollection : ICollectionFixture<OrderReportQueryStoreFixture>
{
}

[Collection("OrderReportQueryStore")]
public sealed class OrderReportQueryStoreIntegrationTests(OrderReportQueryStoreFixture fx)
{
    private const string Tenant = OrderReportQueryStoreFixture.TenantId;
    private const string Store = OrderReportQueryStoreFixture.StoreId;

    [Fact]
    public async Task Summary_aggregates_by_payment_method_via_payment_db()
    {
        await fx.ResetAsync();

        var o1 = Guid.NewGuid();
        var o2 = Guid.NewGuid();
        var o3 = Guid.NewGuid();
        await fx.SeedOrderAsync(o1, total: 100m);
        await fx.SeedOrderAsync(o2, total: 50m);
        await fx.SeedOrderAsync(o3, total: 25m); // no payment txn — falls into "Unknown" bucket

        await fx.SeedPaymentTxnAsync(o1, status: "succeeded", method: "Visa");
        await fx.SeedPaymentTxnAsync(o2, status: "succeeded", method: "Visa");

        var store = fx.CreateStore();
        var df = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var dt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var rows = await store.GetTransactionSummaryAsync(Tenant, Store, df, dt);

        var visa = Assert.Single(rows, r => r.PaymentMethod == "Visa");
        Assert.Equal(2, visa.TransactionCount);
        Assert.Equal(150m, visa.TotalAmount);

        var unknown = Assert.Single(rows, r => r.PaymentMethod == "Unknown");
        Assert.Equal(1, unknown.TransactionCount);
        Assert.Equal(25m, unknown.TotalAmount);
    }

    [Fact]
    public async Task Details_brandCode_filter_actually_filters()
    {
        await fx.ResetAsync();

        var matching = Guid.NewGuid();
        var other = Guid.NewGuid();
        await fx.SeedOrderAsync(matching);
        await fx.SeedOrderAsync(other);
        await fx.SeedOrderItemAsync(matching, brandCode: "VEL-4490");
        await fx.SeedOrderItemAsync(other, brandCode: "AUR-5501");

        var store = fx.CreateStore();
        var df = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var dt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var page = await store.GetTransactionDetailsAsync(
            Tenant, Store, df, dt,
            new TransactionDetailFilter(BrandCode: "VEL-4490"),
            cursor: null, limit: 50);

        Assert.Single(page.Items);
        Assert.Equal(matching, page.Items[0].OrderId);
    }

    [Fact]
    public async Task Details_returns_promotion_arrays_and_payment_type()
    {
        await fx.ResetAsync();

        var oid = Guid.NewGuid();
        await fx.SeedOrderAsync(oid, total: 100m, taxTotal: 8m, orderDiscount: 5m,
            promoCode: "SAVE10", customerName: "Alice", customerEmail: "alice@example.test",
            shippingAddress: "{\"CountryCode\":\"SG\"}");
        await fx.SeedOrderPromotionAsync(oid, editorKind: "order", name: "Welcome", promoCode: "SAVE10");
        await fx.SeedOrderPromotionAsync(oid, editorKind: "item", name: "Bundle", promoCode: "BUN1");
        await fx.SeedOrderPromotionAsync(oid, editorKind: "item", name: "FreeShip", promoCode: "FS5");

        await fx.SeedOrderPaymentAsync(oid, types: new[] { "Gateway", "Voucher" });

        var store = fx.CreateStore();
        var page = await store.GetTransactionDetailsAsync(
            Tenant, Store,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            new TransactionDetailFilter(),
            cursor: null, limit: 50);

        var row = Assert.Single(page.Items);
        Assert.Equal(8m, row.TaxAmount);
        Assert.Equal(5m, row.OrderDiscount);
        Assert.Equal("SAVE10", row.OrderPromoCode);
        Assert.Equal("SG", row.BillingCountry);
        Assert.Equal("Alice", row.CustomerName);
        Assert.Equal("alice@example.test", row.CustomerEmail);
        Assert.Equal("Gateway,Voucher", row.PaymentType);
        Assert.Contains("Welcome", row.Campaigns);
        Assert.Contains("Bundle", row.Campaigns);
        Assert.Contains("BUN1", row.ItemPromoCodes);
        Assert.Contains("FS5", row.ItemPromoCodes);
    }

    [Fact]
    public async Task Overview_computes_totals_and_denominators()
    {
        await fx.ResetAsync();

        var t = DateTime.UtcNow.Date.AddDays(-3);
        await fx.SeedOrderAsync(Guid.NewGuid(), total: 100m, taxTotal: 7m, orderDiscount: 0m, customerId: "c1", createdAt: t);
        await fx.SeedOrderAsync(Guid.NewGuid(), total: 50m,  taxTotal: 4m, orderDiscount: 5m, customerId: "c1", createdAt: t);
        await fx.SeedOrderAsync(Guid.NewGuid(), total: 200m, taxTotal: 14m, orderDiscount: 0m, customerId: "c2", createdAt: t.AddDays(1));
        await fx.SeedOrderAsync(Guid.NewGuid(), total: 25m,  taxTotal: 2m, orderDiscount: 1m, customerId: "c3", createdAt: t.AddDays(2));

        var store = fx.CreateStore();
        var df = DateOnly.FromDateTime(t.AddDays(-1));
        var dt = DateOnly.FromDateTime(t.AddDays(3));

        var ov = await store.GetTransactionsOverviewAsync(Tenant, Store, df, dt);

        Assert.Equal(4, ov.TotalTransactions);
        Assert.Equal(375m, ov.TotalAmount);
        Assert.Equal(27m, ov.TotalTax);
        Assert.Equal(6m, ov.TotalDiscount);
        Assert.Equal(3, ov.UniqueCustomers);
        Assert.Equal(3, ov.DistinctDays);
        Assert.Equal(1, ov.DistinctMonths);
        Assert.Equal(t.AddDays(1).Date, ov.TopDayDate?.Date); // 200 wins over 150 and 25
        Assert.Equal(200m, ov.TopDayAmount);
        Assert.Equal(1, ov.TopDayTransactions);
    }

    [Fact]
    public async Task Overview_returns_zeroes_for_empty_range()
    {
        await fx.ResetAsync();

        var store = fx.CreateStore();
        var ov = await store.GetTransactionsOverviewAsync(
            Tenant, Store,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-29)));

        Assert.Equal(0, ov.TotalTransactions);
        Assert.Equal(0m, ov.TotalAmount);
        Assert.Equal(0, ov.DistinctDays);
        Assert.Equal(0, ov.DistinctMonths);
        Assert.Null(ov.TopDayDate);
    }

    [Fact]
    public async Task Tenant_isolation_holds_across_summary_details_overview()
    {
        await fx.ResetAsync();

        var mine = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        await fx.SeedOrderAsync(mine, total: 100m);
        await fx.SeedOrderAsync(foreign, total: 999m, tenantId: "other-tenant");
        await fx.SeedPaymentTxnAsync(mine, status: "succeeded", method: "Visa");

        var store = fx.CreateStore();
        var df = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var dt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var summary = await store.GetTransactionSummaryAsync(Tenant, Store, df, dt);
        Assert.Equal(100m, summary.Sum(r => r.TotalAmount));

        var details = await store.GetTransactionDetailsAsync(Tenant, Store, df, dt, new TransactionDetailFilter(), null, 50);
        Assert.Single(details.Items);
        Assert.Equal(mine, details.Items[0].OrderId);

        var overview = await store.GetTransactionsOverviewAsync(Tenant, Store, df, dt);
        Assert.Equal(1, overview.TotalTransactions);
        Assert.Equal(100m, overview.TotalAmount);
    }
}

public sealed class OrderReportQueryStoreFixture : IAsyncLifetime
{
    public const string TenantId = "t-report";
    public const string StoreId = "s-report";

    public static readonly Guid TenantGuid = Guid.Parse("00000000-0000-0000-0000-0000000000aa");
    public static readonly Guid StoreGuid = Guid.Parse("00000000-0000-0000-0000-0000000000bb");

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

    public string OrderConnectionString => _orderPg.GetConnectionString();
    public string PaymentConnectionString => _paymentPg.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_orderPg.StartAsync(), _paymentPg.StartAsync());

        await using (var conn = new NpgsqlConnection(OrderConnectionString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS "Orders" (
                    "Id"               UUID         NOT NULL PRIMARY KEY DEFAULT gen_random_uuid(),
                    "TenantId"         VARCHAR(64)  NOT NULL,
                    "StoreId"          VARCHAR(64)  NOT NULL,
                    "CustomerId"       VARCHAR(64)  NOT NULL,
                    "CustomerName"     VARCHAR(200) NULL,
                    "CustomerEmail"    VARCHAR(320) NULL,
                    "Status"           VARCHAR(32)  NOT NULL,
                    "Currency"         VARCHAR(8)   NOT NULL DEFAULT 'USD',
                    "SubTotal"         NUMERIC(18,4) NOT NULL DEFAULT 0,
                    "TaxTotal"         NUMERIC(18,4) NOT NULL DEFAULT 0,
                    "Total"            NUMERIC(18,4) NOT NULL DEFAULT 0,
                    "OrderDiscount"    NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "PromoCode"        VARCHAR(100) NULL,
                    "PromoCodeId"      VARCHAR(36)  NULL,
                    "ShippingAddress"  JSONB        NOT NULL DEFAULT '{}'::jsonb,
                    "PaymentIntentId"  VARCHAR(128) NULL,
                    "IdempotencyKey"   VARCHAR(128) NOT NULL,
                    "CreatedAt"        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    "UpdatedAt"        TIMESTAMPTZ  NOT NULL DEFAULT NOW()
                );

                CREATE TABLE IF NOT EXISTS "OrderItems" (
                    "Id"               UUID         NOT NULL PRIMARY KEY DEFAULT gen_random_uuid(),
                    "OrderId"          UUID         NOT NULL,
                    "ProductId"        VARCHAR(64)  NOT NULL,
                    "VariantId"        VARCHAR(64)  NOT NULL DEFAULT '',
                    "Quantity"         INT          NOT NULL DEFAULT 1,
                    "LineTotal"        NUMERIC(18,4) NOT NULL DEFAULT 0,
                    "LineDiscount"     NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "ProductName"      JSONB        NOT NULL DEFAULT '{}'::jsonb,
                    "VariantSnapshot"  JSONB        NOT NULL DEFAULT '{}'::jsonb
                );

                CREATE TABLE IF NOT EXISTS "OrderPromotions" (
                    "Id"          VARCHAR(36)   NOT NULL PRIMARY KEY,
                    "TenantId"    VARCHAR(64)   NOT NULL,
                    "OrderId"     VARCHAR(64)   NOT NULL,
                    "CampaignId"  VARCHAR(64)   NOT NULL,
                    "EditorKind"  VARCHAR(32)   NOT NULL,
                    "Name"        VARCHAR(200)  NOT NULL DEFAULT '',
                    "Amount"      NUMERIC(18,2) NOT NULL DEFAULT 0,
                    "PromoCode"   VARCHAR(100)  NULL,
                    "AppliedAt"   TIMESTAMPTZ   NOT NULL DEFAULT NOW()
                );

                CREATE TABLE IF NOT EXISTS "OrderPayments" (
                    "Id"      UUID          NOT NULL PRIMARY KEY DEFAULT gen_random_uuid(),
                    "OrderId" UUID          NOT NULL,
                    "Total"   NUMERIC(18,4) NOT NULL DEFAULT 0,
                    "Status"  VARCHAR(32)   NOT NULL DEFAULT 'Pending'
                );

                CREATE TABLE IF NOT EXISTS "PaymentComponents" (
                    "Id"             UUID          NOT NULL PRIMARY KEY DEFAULT gen_random_uuid(),
                    "OrderPaymentId" UUID          NOT NULL,
                    "Type"           VARCHAR(32)   NOT NULL,
                    "Amount"         NUMERIC(18,4) NOT NULL DEFAULT 0,
                    "ExternalRef"    TEXT          NULL,
                    "State"          VARCHAR(32)   NOT NULL DEFAULT 'Pending',
                    "LastError"      TEXT          NULL,
                    "Ordering"       INT           NOT NULL DEFAULT 0,
                    "CreatedAt"      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
                    "UpdatedAt"      TIMESTAMPTZ   NULL
                );
                """);
        }

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
                    "PaymentMethod"   VARCHAR(64)  NOT NULL DEFAULT '',
                    "PaymentIntentId" VARCHAR(128) NOT NULL,
                    "Amount"          NUMERIC(18,4) NOT NULL,
                    "Currency"        VARCHAR(8)   NOT NULL,
                    "Status"          VARCHAR(32)  NOT NULL,
                    "Provider"        VARCHAR(32)  NOT NULL,
                    "CreatedAt"       TIMESTAMPTZ  NOT NULL DEFAULT NOW()
                );
                """);
        }
    }

    public async Task DisposeAsync()
    {
        try { await _paymentPg.DisposeAsync(); } catch { /* ignore */ }
        try { await _orderPg.DisposeAsync(); } catch { /* ignore */ }
    }

    public OrderReportQueryStore CreateStore()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Order"] = OrderConnectionString,
                ["ConnectionStrings:Payment"] = PaymentConnectionString,
            }).Build();
        var paymentStore = new PaymentTransactionQueryStore(cfg);
        return new OrderReportQueryStore(cfg, paymentStore);
    }

    public async Task ResetAsync()
    {
        await using (var c = new NpgsqlConnection(OrderConnectionString))
        {
            await c.OpenAsync();
            await c.ExecuteAsync("""
                TRUNCATE "Orders","OrderItems","OrderPromotions","OrderPayments","PaymentComponents" RESTART IDENTITY;
                """);
        }
        await using (var c = new NpgsqlConnection(PaymentConnectionString))
        {
            await c.OpenAsync();
            await c.ExecuteAsync("""TRUNCATE "PaymentTransactions" RESTART IDENTITY;""");
        }
    }

    public async Task SeedOrderAsync(
        Guid orderId,
        decimal total = 0m,
        decimal taxTotal = 0m,
        decimal orderDiscount = 0m,
        string status = "Confirmed",
        string customerId = "cust-1",
        string? customerName = null,
        string? customerEmail = null,
        string? promoCode = null,
        string shippingAddress = "{}",
        string? tenantId = null,
        string? storeId = null,
        DateTime? createdAt = null)
    {
        await using var conn = new NpgsqlConnection(OrderConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync("""
            INSERT INTO "Orders"
                ("Id","TenantId","StoreId","CustomerId","CustomerName","CustomerEmail",
                 "Status","Currency","Total","TaxTotal","OrderDiscount","PromoCode",
                 "ShippingAddress","IdempotencyKey","CreatedAt","UpdatedAt")
            VALUES
                (@Id,@TenantId,@StoreId,@CustomerId,@CustomerName,@CustomerEmail,
                 @Status,'USD',@Total,@TaxTotal,@OrderDiscount,@PromoCode,
                 @ShippingAddress::jsonb,@Idem,@Now,@Now)
            ON CONFLICT ("Id") DO NOTHING
            """, new
        {
            Id = orderId,
            TenantId = tenantId ?? TenantId,
            StoreId = storeId ?? StoreId,
            CustomerId = customerId,
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            Status = status,
            Total = total,
            TaxTotal = taxTotal,
            OrderDiscount = orderDiscount,
            PromoCode = promoCode,
            ShippingAddress = shippingAddress,
            Idem = $"idem-{orderId:N}",
            Now = createdAt ?? DateTime.UtcNow
        });
    }

    public async Task SeedOrderItemAsync(Guid orderId, string brandCode, string productId = "p1", int quantity = 1, decimal lineTotal = 0m)
    {
        await using var conn = new NpgsqlConnection(OrderConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync("""
            INSERT INTO "OrderItems" ("Id","OrderId","ProductId","Quantity","LineTotal","ProductName","VariantSnapshot")
            VALUES (@Id,@OrderId,@ProductId,@Quantity,@LineTotal,'{}'::jsonb,@Snap::jsonb)
            """, new
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = productId,
            Quantity = quantity,
            LineTotal = lineTotal,
            Snap = $"{{\"brandCode\":\"{brandCode}\"}}",
        });
    }

    public async Task SeedOrderPromotionAsync(Guid orderId, string editorKind, string name, string? promoCode = null)
    {
        await using var conn = new NpgsqlConnection(OrderConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync("""
            INSERT INTO "OrderPromotions" ("Id","TenantId","OrderId","CampaignId","EditorKind","Name","PromoCode")
            VALUES (@Id,@TenantId,@OrderId,@CampaignId,@EditorKind,@Name,@PromoCode)
            """, new
        {
            Id = Guid.NewGuid().ToString(),
            TenantId,
            OrderId = orderId.ToString(),
            CampaignId = "camp-1",
            EditorKind = editorKind,
            Name = name,
            PromoCode = promoCode,
        });
    }

    public async Task SeedOrderPaymentAsync(Guid orderId, string[] types)
    {
        await using var conn = new NpgsqlConnection(OrderConnectionString);
        await conn.OpenAsync();
        var paymentId = Guid.NewGuid();
        await conn.ExecuteAsync("""
            INSERT INTO "OrderPayments" ("Id","OrderId","Total","Status") VALUES (@Id,@OrderId,0,'Pending')
            """, new { Id = paymentId, OrderId = orderId });
        for (int i = 0; i < types.Length; i++)
        {
            await conn.ExecuteAsync("""
                INSERT INTO "PaymentComponents" ("Id","OrderPaymentId","Type","Amount","State","Ordering")
                VALUES (@Id,@OrderPaymentId,@Type,0,'Pending',@Ordering)
                """, new
            {
                Id = Guid.NewGuid(),
                OrderPaymentId = paymentId,
                Type = types[i],
                Ordering = i,
            });
        }
    }

    public async Task SeedPaymentTxnAsync(
        Guid orderId,
        string status,
        decimal amount = 0m,
        string currency = "USD",
        string method = "card",
        DateTime? createdAt = null)
    {
        await using var conn = new NpgsqlConnection(PaymentConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync("""
            INSERT INTO "PaymentTransactions"
                ("Id","OrderId","TenantId","StoreId","CustomerId","PaymentMethod","PaymentIntentId","Amount","Currency","Status","Provider","CreatedAt")
            VALUES
                (@Id,@OrderId,@TenantId,@StoreId,@CustomerId,@Method,@Pi,@Amount,@Currency,@Status,@Provider,@CreatedAt)
            """, new
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            TenantId = TenantGuid,
            StoreId = StoreGuid,
            CustomerId = "cust-1",
            Method = method,
            Pi = $"pi_{orderId:N}",
            Amount = amount,
            Currency = currency,
            Status = status,
            Provider = "stub",
            CreatedAt = createdAt ?? DateTime.UtcNow,
        });
    }
}
