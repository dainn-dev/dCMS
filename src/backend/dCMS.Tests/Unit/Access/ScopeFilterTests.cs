using System.Security.Claims;
using System.Text;
using System.Text.Json;
using dCMS.AspNetCore.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace dCMS.Tests.Unit.Access;

/// <summary>
/// DAI-700 — Unit coverage for the four scope variants required by the spec:
///   AC1 Tenant, AC2 Store, AC3 Brand, AC4 StoreFromBody.
/// </summary>
public sealed class ScopeFilterTests
{
    // ── helpers ────────────────────────────────────────────────────────────────
    private static HttpContext NewContext(
        IDictionary<string, string?>? routeValues = null,
        IDictionary<string, string>? headers = null,
        IDictionary<string, string>? query = null,
        ClaimsPrincipal? user = null,
        string? jsonBody = null,
        string method = "GET")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        if (routeValues != null)
            foreach (var (k, v) in routeValues) ctx.Request.RouteValues[k] = v;
        if (headers != null)
            foreach (var (k, v) in headers) ctx.Request.Headers[k] = v;
        if (query != null)
        {
            var qs = string.Join("&", query.Select(kv => $"{kv.Key}={kv.Value}"));
            ctx.Request.QueryString = new QueryString("?" + qs);
        }
        if (user != null) ctx.User = user;
        if (jsonBody != null)
        {
            var body = new MemoryStream(Encoding.UTF8.GetBytes(jsonBody));
            ctx.Request.Body = body;
            ctx.Request.ContentLength = body.Length;
            ctx.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetection(true));
        }
        return ctx;
    }

    private static ClaimsPrincipal User(string? tenantId, string? storeId = null, string? storeIdsCsv = null,
        string? brandIdsCsv = null, params string[] roles)
    {
        var claims = new List<Claim>();
        if (tenantId is not null) claims.Add(new Claim(DcmsClaims.TenantId, tenantId));
        if (storeId is not null) claims.Add(new Claim(DcmsClaims.StoreId, storeId));
#pragma warning disable CS0618 // Obsolete StoreIds/BrandIds — tests validate legacy claim path until US-5.
        if (storeIdsCsv is not null) claims.Add(new Claim(DcmsClaims.StoreIds, storeIdsCsv));
        if (brandIdsCsv is not null) claims.Add(new Claim(DcmsClaims.BrandIds, brandIdsCsv));
#pragma warning restore CS0618
        foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    private sealed class BodyDetection(bool can) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => can;
    }

    private static EndpointFilterInvocationContext Invocation(HttpContext ctx) =>
        new DefaultEndpointFilterInvocationContext(ctx);

    private static EndpointFilterDelegate AlwaysOk() =>
        _ => ValueTask.FromResult<object?>(Results.Ok());

    private static int? StatusOf(object? r) =>
        r is IStatusCodeHttpResult s ? s.StatusCode : null;

    // ── AC1 Tenant ────────────────────────────────────────────────────────────
    [Fact]
    public async Task TenantOnly_filter_passes_when_token_tenant_matches()
    {
        var ctx = NewContext(
            routeValues: new Dictionary<string, string?> { ["tenantId"] = "T1" },
            user: User("T1", roles: DcmsRoles.ChainAdmin));

        var result = await new TenantOnlyAccessEndpointFilter().InvokeAsync(Invocation(ctx), AlwaysOk());
        StatusOf(result).Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task TenantOnly_filter_blocks_cross_tenant_request()
    {
        var ctx = NewContext(
            routeValues: new Dictionary<string, string?> { ["tenantId"] = "T2" },
            user: User("T1", roles: DcmsRoles.ChainAdmin));

        var result = await new TenantOnlyAccessEndpointFilter().InvokeAsync(Invocation(ctx), AlwaysOk());
        StatusOf(result).Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task TenantOnly_filter_lets_super_admin_through()
    {
        var ctx = NewContext(
            routeValues: new Dictionary<string, string?> { ["tenantId"] = "T2" },
            user: User("T1", roles: DcmsRoles.SuperAdmin));

        var result = await new TenantOnlyAccessEndpointFilter().InvokeAsync(Invocation(ctx), AlwaysOk());
        StatusOf(result).Should().Be(StatusCodes.Status200OK);
    }

    // ── AC2 Store (header variant covers Order API path) ──────────────────────
    [Fact]
    public async Task TenantStoreHeader_filter_passes_when_token_matches()
    {
        var ctx = NewContext(
            headers: new Dictionary<string, string> { ["X-Tenant-Id"] = "T1", ["X-Store-Id"] = "S1" },
            user: User("T1", "S1", roles: DcmsRoles.StoreManager));

        var result = await new TenantStoreHeaderAccessEndpointFilter().InvokeAsync(Invocation(ctx), AlwaysOk());
        StatusOf(result).Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task TenantStoreHeader_filter_blocks_when_store_not_in_token_set()
    {
        var ctx = NewContext(
            headers: new Dictionary<string, string> { ["X-Tenant-Id"] = "T1", ["X-Store-Id"] = "S9" },
            user: User("T1", "S1", storeIdsCsv: "S1,S2", roles: DcmsRoles.StoreManager));

        var result = await new TenantStoreHeaderAccessEndpointFilter().InvokeAsync(Invocation(ctx), AlwaysOk());
        StatusOf(result).Should().Be(StatusCodes.Status403Forbidden);
    }

    // ── AC3 Brand ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task BrandScope_filter_passes_for_chain_admin_within_tenant()
    {
        var ctx = NewContext(
            routeValues: new Dictionary<string, string?> { ["brandId"] = "B1" },
            user: User("T1", roles: DcmsRoles.ChainAdmin));

        var result = await new BrandScopeAccessEndpointFilter().InvokeAsync(Invocation(ctx), AlwaysOk());
        StatusOf(result).Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task BrandScope_filter_blocks_brand_manager_outside_brand_claim()
    {
        var ctx = NewContext(
            routeValues: new Dictionary<string, string?> { ["brandId"] = "BX" },
            user: User("T1", brandIdsCsv: "B1,B2", roles: DcmsRoles.BrandManager));

        var result = await new BrandScopeAccessEndpointFilter().InvokeAsync(Invocation(ctx), AlwaysOk());
        StatusOf(result).Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task BrandScope_filter_returns_400_when_brand_not_resolvable()
    {
        var ctx = NewContext(user: User("T1", roles: DcmsRoles.ChainAdmin));
        var result = await new BrandScopeAccessEndpointFilter().InvokeAsync(Invocation(ctx), AlwaysOk());
        StatusOf(result).Should().Be(StatusCodes.Status400BadRequest);
    }

    // ── AC2b TenantStore route (Notification Feed) ────────────────────────────
    [Fact]
    public async Task TenantStore_route_filter_passes_when_tenant_and_store_match()
    {
        var ctx = NewContext(
            routeValues: new Dictionary<string, string?> { ["tenantId"] = "T1", ["storeId"] = "S1" },
            user: User("T1", "S1", roles: DcmsRoles.StoreManager));

        var result = await new TenantStoreAccessEndpointFilter().InvokeAsync(Invocation(ctx), AlwaysOk());
        StatusOf(result).Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task TenantStore_route_filter_blocks_tenant_mismatch()
    {
        var ctx = NewContext(
            routeValues: new Dictionary<string, string?> { ["tenantId"] = "T2", ["storeId"] = "S1" },
            user: User("T1", "S1", roles: DcmsRoles.StoreManager));

        var result = await new TenantStoreAccessEndpointFilter().InvokeAsync(Invocation(ctx), AlwaysOk());
        StatusOf(result).Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task TenantStore_route_filter_blocks_store_mismatch()
    {
        var ctx = NewContext(
            routeValues: new Dictionary<string, string?> { ["tenantId"] = "T1", ["storeId"] = "S9" },
            user: User("T1", "S1", roles: DcmsRoles.StoreManager));

        var result = await new TenantStoreAccessEndpointFilter().InvokeAsync(Invocation(ctx), AlwaysOk());
        StatusOf(result).Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task TenantStore_route_filter_lets_super_admin_through()
    {
        var ctx = NewContext(
            routeValues: new Dictionary<string, string?> { ["tenantId"] = "T9", ["storeId"] = "S9" },
            user: User("T1", "S1", roles: DcmsRoles.SuperAdmin));

        var result = await new TenantStoreAccessEndpointFilter().InvokeAsync(Invocation(ctx), AlwaysOk());
        StatusOf(result).Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task TenantStore_route_filter_returns_401_for_unauthenticated()
    {
        var ctx = NewContext(
            routeValues: new Dictionary<string, string?> { ["tenantId"] = "T1", ["storeId"] = "S1" },
            user: new ClaimsPrincipal()); // no identity

        var result = await new TenantStoreAccessEndpointFilter().InvokeAsync(Invocation(ctx), AlwaysOk());
        StatusOf(result).Should().Be(StatusCodes.Status401Unauthorized);
    }

    // ── AC4 StoresFromBody ────────────────────────────────────────────────────
    [Fact]
    public async Task StoresFromBody_filter_passes_when_token_covers_all_ids()
    {
        var body = JsonSerializer.Serialize(new { storeIds = new[] { "S1", "S2" } });
        var ctx = NewContext(
            jsonBody: body, method: HttpMethods.Post,
            user: User("T1", storeIdsCsv: "S1,S2,S3", roles: DcmsRoles.StoreManager));

        var result = await new StoresFromBodyAccessEndpointFilter().InvokeAsync(Invocation(ctx), AlwaysOk());
        StatusOf(result).Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task StoresFromBody_filter_blocks_when_token_lacks_one_id()
    {
        var body = JsonSerializer.Serialize(new { storeIds = new[] { "S1", "S9" } });
        var ctx = NewContext(
            jsonBody: body, method: HttpMethods.Post,
            user: User("T1", storeIdsCsv: "S1,S2", roles: DcmsRoles.StoreManager));

        var result = await new StoresFromBodyAccessEndpointFilter().InvokeAsync(Invocation(ctx), AlwaysOk());
        StatusOf(result).Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task StoresFromBody_filter_returns_400_when_array_missing()
    {
        var body = JsonSerializer.Serialize(new { other = "x" });
        var ctx = NewContext(
            jsonBody: body, method: HttpMethods.Post,
            user: User("T1", storeIdsCsv: "S1", roles: DcmsRoles.StoreManager));

        var result = await new StoresFromBodyAccessEndpointFilter().InvokeAsync(Invocation(ctx), AlwaysOk());
        StatusOf(result).Should().Be(StatusCodes.Status400BadRequest);
    }
}
