using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using dCMS.AspNetCore.Auth;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

namespace dCMS.Order.Tests.Integration;

/// <summary>DAI-329 — Order API with Auth:Enabled: JWT, tenant/store headers, customer scope, Redis detail cache TTL.</summary>
[CollectionDefinition("OrderApiAuth", DisableParallelization = true)]
public sealed class OrderApiAuthCollection : ICollectionFixture<OrderApiAuthFixture>
{
}

[Collection("OrderApiAuth")]
public sealed class OrderApiAuthIntegrationTests(OrderApiAuthFixture fx)
{
    [Fact]
    public async Task GetOrder_without_bearer_returns_401()
    {
        var client = fx.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", OrderApiAuthFixture.TenantId);
        client.DefaultRequestHeaders.Add("X-Store-Id", OrderApiAuthFixture.StoreId);

        var response = await client.GetAsync($"/api/orders/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_jwt_tenant_mismatch_returns_403()
    {
        var client = fx.Factory.CreateClient();
        AddOrderHeaders(client, Jwt("u1", "other-tenant", OrderApiAuthFixture.StoreId, DcmsRoles.Customer));

        var response = await client.GetAsync($"/api/orders/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_chain_admin_with_store_scope_cannot_access_other_store()
    {
        var client = fx.Factory.CreateClient();
        // Token only allows store "s-allowed", but headers request store "s-denied".
        AddOrderHeaders(client, Jwt("ca-1", OrderApiAuthFixture.TenantId, "s-allowed", DcmsRoles.ChainAdmin, storeIds: ["s-allowed"]));
        client.DefaultRequestHeaders.Remove("X-Store-Id");
        client.DefaultRequestHeaders.Add("X-Store-Id", "s-denied");

        var response = await client.GetAsync($"/api/orders/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_customer_cannot_read_other_customer_order()
    {
        var orderId = Guid.NewGuid();
        await SeedMinimalOrderAsync(fx, orderId, "cust-b");

        var client = fx.Factory.CreateClient();
        AddOrderHeaders(client, Jwt("cust-a", OrderApiAuthFixture.TenantId, OrderApiAuthFixture.StoreId, DcmsRoles.Customer));

        var response = await client.GetAsync($"/api/orders/{orderId:D}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_staff_can_read_customer_order()
    {
        var orderId = Guid.NewGuid();
        await SeedMinimalOrderAsync(fx, orderId, "cust-b");

        var client = fx.Factory.CreateClient();
        AddOrderHeaders(client, Jwt("mgr-1", OrderApiAuthFixture.TenantId, OrderApiAuthFixture.StoreId, DcmsRoles.StoreManager));

        var response = await client.GetAsync($"/api/orders/{orderId:D}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(orderId.ToString("D"), doc.RootElement.GetProperty("orderId").GetString());
        Assert.Equal("cust-b", doc.RootElement.GetProperty("customerId").GetString());
        Assert.True(doc.RootElement.TryGetProperty("shipment", out _));
    }

    [Fact]
    public async Task ListOrders_customer_query_customerId_is_ignored_non_staff()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        await SeedMinimalOrderAsync(fx, id1, "cust-1");
        await SeedMinimalOrderAsync(fx, id2, "cust-2");

        var client = fx.Factory.CreateClient();
        AddOrderHeaders(client, Jwt("cust-1", OrderApiAuthFixture.TenantId, OrderApiAuthFixture.StoreId, DcmsRoles.Customer));

        var response = await client.GetAsync("/api/orders?customerId=cust-2&limit=50");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(id1.ToString("D"), items[0].GetProperty("orderId").GetString());
    }

    [Fact]
    public async Task GetOrder_populates_redis_detail_cache_with_short_ttl()
    {
        var orderId = Guid.NewGuid();
        await SeedMinimalOrderAsync(fx, orderId, "cust-z");

        var client = fx.Factory.CreateClient();
        AddOrderHeaders(client, Jwt("mgr-1", OrderApiAuthFixture.TenantId, OrderApiAuthFixture.StoreId, DcmsRoles.StoreManager));

        var response = await client.GetAsync($"/api/orders/{orderId:D}");
        response.EnsureSuccessStatusCode();

        await using var mux = await ConnectionMultiplexer.ConnectAsync(fx.RedisConnectionString);
        var ttl = await mux.GetDatabase().KeyTimeToLiveAsync($"dcms:order:{orderId:D}");
        Assert.NotNull(ttl);
        Assert.True(ttl!.Value.TotalSeconds > 0 && ttl.Value.TotalSeconds <= 60);
    }

    [Fact]
    public async Task ShipOrder_staff_creates_shipment_and_customer_can_read_shipment()
    {
        var orderId = Guid.NewGuid();
        await SeedMinimalOrderAsync(fx, orderId, "cust-ship", status: nameof(dCMS.Order.Core.Domain.OrderStatus.Processing));

        var staff = fx.Factory.CreateClient();
        AddOrderHeaders(staff, Jwt("staff-1", OrderApiAuthFixture.TenantId, OrderApiAuthFixture.StoreId, DcmsRoles.StoreStaff));
        staff.DefaultRequestHeaders.Add("Idempotency-Key", $"idem-{Guid.NewGuid():N}");

        var shipBody = JsonSerializer.Serialize(new { carrier = "DHL", trackingNumber = "TRK123" });
        var shipRes = await staff.PostAsync($"/api/orders/{orderId:D}/ship", new StringContent(shipBody, Encoding.UTF8, "application/json"));
        if (!shipRes.IsSuccessStatusCode)
        {
            var text = await shipRes.Content.ReadAsStringAsync();
            throw new Exception($"Ship failed: {(int)shipRes.StatusCode} {shipRes.StatusCode} body={text}");
        }

        var customer = fx.Factory.CreateClient();
        AddOrderHeaders(customer, Jwt("cust-ship", OrderApiAuthFixture.TenantId, OrderApiAuthFixture.StoreId, DcmsRoles.Customer));
        var getRes = await customer.GetAsync($"/api/orders/{orderId:D}/shipment");
        if (!getRes.IsSuccessStatusCode)
        {
            var text = await getRes.Content.ReadAsStringAsync();
            throw new Exception($"Get shipment failed: {(int)getRes.StatusCode} {getRes.StatusCode} body={text}");
        }
        using var doc = JsonDocument.Parse(await getRes.Content.ReadAsStringAsync());
        Assert.Equal("DHL", doc.RootElement.GetProperty("carrier").GetString());
        Assert.Equal("TRK123", doc.RootElement.GetProperty("trackingNumber").GetString());
        Assert.True(doc.RootElement.TryGetProperty("events", out var evs) && evs.ValueKind == JsonValueKind.Array);
    }

    [Fact]
    public async Task DeliverOrder_staff_can_mark_delivered_when_shipment_exists()
    {
        var orderId = Guid.NewGuid();
        await SeedMinimalOrderAsync(fx, orderId, "cust-deliver", status: nameof(dCMS.Order.Core.Domain.OrderStatus.Shipped));
        await SeedShipmentAsync(fx, orderId);

        var staff = fx.Factory.CreateClient();
        AddOrderHeaders(staff, Jwt("staff-1", OrderApiAuthFixture.TenantId, OrderApiAuthFixture.StoreId, DcmsRoles.StoreStaff));
        staff.DefaultRequestHeaders.Add("Idempotency-Key", $"idem-{Guid.NewGuid():N}");

        var deliverRes = await staff.PostAsync($"/api/orders/{orderId:D}/deliver", content: null);
        if (!deliverRes.IsSuccessStatusCode)
        {
            var text = await deliverRes.Content.ReadAsStringAsync();
            throw new Exception($"Deliver failed: {(int)deliverRes.StatusCode} {deliverRes.StatusCode} body={text}");
        }

        var getShip = await staff.GetAsync($"/api/orders/{orderId:D}/shipment");
        getShip.EnsureSuccessStatusCode();
        using var shipDoc = JsonDocument.Parse(await getShip.Content.ReadAsStringAsync());
        Assert.Equal("delivered", shipDoc.RootElement.GetProperty("status").GetString());

        var getOrder = await staff.GetAsync($"/api/orders/{orderId:D}");
        getOrder.EnsureSuccessStatusCode();
        using var orderDoc = JsonDocument.Parse(await getOrder.Content.ReadAsStringAsync());
        Assert.Equal("delivered", orderDoc.RootElement.GetProperty("status").GetString());
    }

    private static void AddOrderHeaders(HttpClient client, string bearerToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        client.DefaultRequestHeaders.Remove("X-Store-Id");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", OrderApiAuthFixture.TenantId);
        client.DefaultRequestHeaders.Add("X-Store-Id", OrderApiAuthFixture.StoreId);
    }

    private static async Task SeedMinimalOrderAsync(OrderApiAuthFixture fx, Guid orderId, string customerId, string status = nameof(dCMS.Order.Core.Domain.OrderStatus.PaymentPending))
    {
        var ship = JsonSerializer.Serialize(new
        {
            line1 = "1 Test",
            line2 = (string?)null,
            city = "HCMC",
            region = "SG",
            postalCode = "700000",
            countryCode = "VN",
        });

        await using var conn = new NpgsqlConnection(fx.PostgreSqlConnectionString);
        await conn.OpenAsync().ConfigureAwait(false);
        await conn.ExecuteAsync(
            """
            INSERT INTO "Orders" (
                "Id", "TenantId", "StoreId", "CustomerId", "Status", "Currency",
                "SubTotal", "TaxTotal", "Total", "IdempotencyKey", "ShippingAddress")
            VALUES (
                @Id, @TenantId, @StoreId, @CustomerId, @Status, 'USD',
                1, 0, 1, @Idem, @Ship::jsonb)
            """,
            new
            {
                Id = orderId,
                TenantId = OrderApiAuthFixture.TenantId,
                StoreId = OrderApiAuthFixture.StoreId,
                CustomerId = customerId,
                Status = status,
                Idem = $"seed-{orderId:N}",
                Ship = ship,
            }).ConfigureAwait(false);
    }

    private static async Task SeedShipmentAsync(OrderApiAuthFixture fx, Guid orderId)
    {
        await using var conn = new NpgsqlConnection(fx.PostgreSqlConnectionString);
        await conn.OpenAsync().ConfigureAwait(false);
        await conn.ExecuteAsync(
            """
            INSERT INTO "Shipments" ("OrderId", "Carrier", "TrackingNumber", "Status", "CreatedAt", "UpdatedAt")
            VALUES (@OrderId, 'mock', 'TN-DELIVER', 'pending', NOW(), NOW())
            ON CONFLICT ("OrderId") DO NOTHING
            """,
            new { OrderId = orderId }).ConfigureAwait(false);
    }

    private static string Jwt(
        string sub,
        string tenantId,
        string storeId,
        string role,
        IReadOnlyList<string>? storeIds = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(OrderApiAuthFixture.JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, sub),
            new(DcmsClaims.TenantId, tenantId),
            new(DcmsClaims.StoreId, storeId),
            new(ClaimTypes.Role, role),
            new(DcmsClaims.ClientId, OrderApiAuthFixture.ClientId),
        };

#pragma warning disable CS0618 // Obsolete StoreIds — kept until US-5 (Users module) lands.
        if (storeIds is { Count: > 0 })
            claims.Add(new Claim(DcmsClaims.StoreIds, string.Join(",", storeIds)));
#pragma warning restore CS0618

        var token = new JwtSecurityToken(
            issuer: "dcms",
            audience: "dcms-api",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class OrderApiAuthFixture : IAsyncLifetime
{
    public const string JwtKey = "integration-test-signing-key-32bytes!!";
    public const string TenantId = "t-saas-a";
    public const string StoreId = "s-saas-a1";
    public const string TenantB = "t-saas-b";
    public const string StoreB = "s-saas-b1";
    public const string ClientId = "saas-test-client";

    private PostgreSqlContainer? _postgres;
    private RabbitMqContainer? _rabbit;
    private RedisContainer? _redis;
    private WebApplicationFactory<Program>? _factory;

    public WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException("Fixture not initialized.");
    public string PostgreSqlConnectionString => _postgres?.GetConnectionString() ?? "";
    public string RedisConnectionString => _redis?.GetConnectionString() ?? "";

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("dcms_order_auth_it")
            .WithUsername("dcms")
            .WithPassword("test")
            .Build();
        _rabbit = new RabbitMqBuilder().Build();
        _redis = new RedisBuilder().Build();

        await _postgres.StartAsync().ConfigureAwait(false);
        await _rabbit.StartAsync().ConfigureAwait(false);
        await _redis.StartAsync().ConfigureAwait(false);

        var rabbitPort = _rabbit.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort);
        var orderCs = _postgres.GetConnectionString();
        var redisCs = _redis.GetConnectionString();

        // UseSetting is applied before Program runs so service registration and middleware see the same Auth flags.
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("Auth:Enabled", "true");
            b.UseSetting("Auth:JwtSigningKey", JwtKey);
            b.UseSetting("Auth:Issuer", "dcms");
            b.UseSetting("Auth:Audience", "dcms-api");
            b.UseSetting("Dcms:Client:Id", ClientId);
            b.UseSetting("ConnectionStrings:Order", orderCs);
            b.UseSetting("ConnectionStrings:Redis", redisCs);
            b.UseSetting("RabbitMq:Host", "127.0.0.1");
            b.UseSetting("RabbitMq:Port", rabbitPort.ToString());
            b.UseSetting("RabbitMq:User", RabbitMqBuilder.DefaultUsername);
            b.UseSetting("RabbitMq:Pass", RabbitMqBuilder.DefaultPassword);
            b.UseSetting("Inventory:BaseUrl", "http://127.0.0.1:59999/");
            b.UseSetting("Payment:BaseUrl", "http://127.0.0.2:59998/");
        });

        // First client build starts the host (DbUp migrations + MassTransit) before tests seed data.
        _factory.CreateClient().Dispose();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
            await _factory.DisposeAsync().ConfigureAwait(false);

        if (_redis is not null)
            await _redis.DisposeAsync().ConfigureAwait(false);
        if (_rabbit is not null)
            await _rabbit.DisposeAsync().ConfigureAwait(false);
        if (_postgres is not null)
            await _postgres.DisposeAsync().ConfigureAwait(false);
    }
}
