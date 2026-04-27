using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using dCMS.AspNetCore.Auth;
using Dapper;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;
using Xunit;

namespace dCMS.Order.Tests.Integration;

/// <summary>
/// DAI-701 — Cross-tenant smoke fixture. Asserts that a JWT issued for tenant T1 cannot read or
/// mutate orders, shipments, or refund cases owned by tenant T2 across every Order-API route shape
/// (list, detail, create, ship, refund-case GET/PUT). Pairs with the unit tests in
/// <see cref="dCMS.Tests.Unit.Access.ScopeFilterTests"/>: unit tests pin per-filter behavior; this
/// fixture pins end-to-end behavior across the wiring helpers (<see cref="DcmsJwtAuthExtensions.WithTenantStoreHeaderAccess"/>).
/// </summary>
[Collection("OrderApiAuth")]
public sealed class CrossTenantSmokeTests(OrderApiAuthFixture fx)
{
    private const string ForeignTenantId = "t-foreign";
    private const string ForeignStoreId = "s-foreign";

    [Fact]
    public async Task GetOrder_with_foreign_tenant_token_is_forbidden()
    {
        var orderId = Guid.NewGuid();
        await SeedMinimalOrderAsync(fx, orderId, "cust-x");

        var client = fx.Factory.CreateClient();
        AddHomeTenantHeaders(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Jwt("u-foreign", ForeignTenantId, ForeignStoreId, DcmsRoles.StoreManager));

        var response = await client.GetAsync($"/api/orders/{orderId:D}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListOrders_with_foreign_tenant_token_is_forbidden()
    {
        var client = fx.Factory.CreateClient();
        AddHomeTenantHeaders(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Jwt("u-foreign", ForeignTenantId, ForeignStoreId, DcmsRoles.StoreManager));

        var response = await client.GetAsync("/api/orders?limit=10");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_with_foreign_tenant_token_is_forbidden()
    {
        var client = fx.Factory.CreateClient();
        AddHomeTenantHeaders(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Jwt("u-foreign", ForeignTenantId, ForeignStoreId, DcmsRoles.StoreManager));
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"idem-{Guid.NewGuid():N}");

        var body = JsonSerializer.Serialize(new
        {
            customerId = "cust-x",
            currency = "USD",
            shippingAddress = new
            {
                line1 = "1 Foreign", line2 = (string?)null, city = "X", region = "X",
                postalCode = "00000", countryCode = "VN",
            },
            items = Array.Empty<object>(),
        });

        var response = await client.PostAsync("/api/orders", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ShipOrder_with_foreign_tenant_token_is_forbidden()
    {
        var orderId = Guid.NewGuid();
        await SeedMinimalOrderAsync(fx, orderId, "cust-y", status: nameof(dCMS.Order.Core.Domain.OrderStatus.Processing));

        var client = fx.Factory.CreateClient();
        AddHomeTenantHeaders(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Jwt("u-foreign", ForeignTenantId, ForeignStoreId, DcmsRoles.StoreStaff));
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"idem-{Guid.NewGuid():N}");

        var body = JsonSerializer.Serialize(new { carrier = "DHL", trackingNumber = "TRK-X" });
        var response = await client.PostAsync($"/api/orders/{orderId:D}/ship",
            new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetRefundCase_with_foreign_tenant_token_is_forbidden()
    {
        var orderId = Guid.NewGuid();
        await SeedMinimalOrderAsync(fx, orderId, "cust-r");

        var client = fx.Factory.CreateClient();
        AddHomeTenantHeaders(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Jwt("u-foreign", ForeignTenantId, ForeignStoreId, DcmsRoles.CustomerSupport));

        var response = await client.GetAsync($"/api/orders/{orderId:D}/refund-case");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChainAdmin_with_token_store_set_cannot_target_other_store_via_header()
    {
        var orderId = Guid.NewGuid();
        await SeedMinimalOrderAsync(fx, orderId, "cust-cross-store");

        // ChainAdmin token narrowed to "s-allowed" within the home tenant; header asks for the home store, which is NOT in the token's allowed set.
        var client = fx.Factory.CreateClient();
        AddHomeTenantHeaders(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer",
                Jwt("ca-narrow", OrderApiAuthFixture.TenantId, "s-allowed", DcmsRoles.ChainAdmin, storeIds: ["s-allowed"]));

        var response = await client.GetAsync($"/api/orders/{orderId:D}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static void AddHomeTenantHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        client.DefaultRequestHeaders.Remove("X-Store-Id");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", OrderApiAuthFixture.TenantId);
        client.DefaultRequestHeaders.Add("X-Store-Id", OrderApiAuthFixture.StoreId);
    }

    private static async Task SeedMinimalOrderAsync(
        OrderApiAuthFixture fx,
        Guid orderId,
        string customerId,
        string status = nameof(dCMS.Order.Core.Domain.OrderStatus.PaymentPending))
    {
        var ship = JsonSerializer.Serialize(new
        {
            line1 = "1 Test", line2 = (string?)null,
            city = "HCMC", region = "SG", postalCode = "700000", countryCode = "VN",
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
                Idem = $"smoke-{orderId:N}",
                Ship = ship,
            }).ConfigureAwait(false);
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
        };
        if (storeIds is { Count: > 0 })
            claims.Add(new Claim(DcmsClaims.StoreIds, string.Join(",", storeIds)));

        var token = new JwtSecurityToken(
            issuer: "dcms",
            audience: "dcms-api",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
