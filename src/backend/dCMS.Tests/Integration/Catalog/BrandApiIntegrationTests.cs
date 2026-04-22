using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Integration.Catalog;

/// <summary>
/// DAI-577: Brand CRUD integration tests.
/// Reuses <see cref="CatalogApiFixture"/> (PostgreSQL + WAF, Auth disabled).
/// </summary>
[Collection("CatalogApi")]
public sealed class BrandApiIntegrationTests(CatalogApiFixture fixture)
{
    // Per-instance unique 4-letter prefix (A-Z only) to avoid conflicts between parallel runs.
    // Map each byte of the GUID to a letter A-Z to ensure the prefix is always all-letters.
    private static string MakeId()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        return new string(new[] {
            (char)('A' + bytes[0] % 26),
            (char)('A' + bytes[1] % 26),
            (char)('A' + bytes[2] % 26),
            (char)('A' + bytes[3] % 26),
        });
    }
    private readonly string _id = MakeId();

    // ── helpers ───────────────────────────────────────────────────────────────

    private static HttpClient Client(CatalogApiFixture f) => f.Factory!.CreateClient();

    private string BrandsUrl(string? tenantSuffix = null)
    {
        var tenant = tenantSuffix is null ? $"t1-{_id}" : $"{tenantSuffix}-{_id}";
        return $"/api/v1/tenants/{tenant}/brands";
    }

    private string BrandUrl(string code, string? tenantSuffix = null)
    {
        var tenant = tenantSuffix is null ? $"t1-{_id}" : $"{tenantSuffix}-{_id}";
        return $"/api/v1/tenants/{tenant}/brands/{code}";
    }

    private static object Body(string name, bool active = true, string? imageUrl = null, string? imageAlt = null) =>
        new { name, active, imageUrl = imageUrl ?? "", imageAlt = imageAlt ?? "" };

    private static object CreateBody(string code, string name, bool active = true) =>
        new { code, name, active, imageUrl = "https://example.com/logo.png", imageAlt = "logo" };

    // Brand code helpers — prefix is 4 chars from _id, suffix is a numeric discriminator.
    private string Code(int n) => $"{_id}-{n}";

    private void Skip() => Xunit.Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

    // ── CREATE ────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Post_creates_brand_and_returns_201()
    {
        Skip();
        using var client = Client(fixture);
        var code = Code(1);

        var resp = await client.PostAsJsonAsync(BrandsUrl(), CreateBody(code, "Test Brand"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("code").GetString().Should().Be(code);
        doc.RootElement.GetProperty("data").GetProperty("name").GetString().Should().Be("Test Brand");
        doc.RootElement.GetProperty("data").GetProperty("active").GetBoolean().Should().BeTrue();
    }

    [SkippableFact]
    public async Task Post_duplicate_code_returns_409()
    {
        Skip();
        using var client = Client(fixture);
        var code = Code(1);

        await client.PostAsJsonAsync(BrandsUrl(), CreateBody(code, "Brand A"));
        var resp = await client.PostAsJsonAsync(BrandsUrl(), CreateBody(code, "Brand B"));

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("conflict");
    }

    [SkippableFact]
    public async Task Post_invalid_code_format_returns_400()
    {
        Skip();
        using var client = Client(fixture);

        var resp = await client.PostAsJsonAsync(BrandsUrl(), CreateBody("invalid-code", "Brand"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("validation_error");
    }

    [SkippableFact]
    public async Task Post_empty_name_returns_400()
    {
        Skip();
        using var client = Client(fixture);
        var code = Code(1);

        var resp = await client.PostAsJsonAsync(BrandsUrl(), new { code, name = "", active = true });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── READ ──────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Get_returns_brand_after_create()
    {
        Skip();
        using var client = Client(fixture);
        var code = Code(1);

        await client.PostAsJsonAsync(BrandsUrl(), CreateBody(code, "Get Me"));

        var resp = await client.GetAsync(BrandUrl(code));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("name").GetString().Should().Be("Get Me");
    }

    [SkippableFact]
    public async Task Get_nonexistent_returns_404()
    {
        Skip();
        using var client = Client(fixture);

        var resp = await client.GetAsync(BrandUrl("NON-999"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("not_found");
    }

    // ── LIST ──────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task List_returns_only_brands_for_tenant()
    {
        Skip();
        using var client = Client(fixture);
        var code = Code(1);
        var tenantA = $"t1-{_id}";

        await client.PostAsJsonAsync(BrandsUrl("t1"), CreateBody(code, "Tenant 1 Brand"));
        await client.PostAsJsonAsync(BrandsUrl("t2"), CreateBody(code, "Tenant 2 Brand"));

        var resp = await client.GetAsync(BrandsUrl("t1"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("data").EnumerateArray().ToList();

        items.Should().OnlyContain(b => b.GetProperty("tenantId").GetString() == tenantA);
    }

    [SkippableFact]
    public async Task List_with_active_filter_returns_only_matching()
    {
        Skip();
        using var client = Client(fixture);
        var codeAct = Code(1);
        var codeIna = Code(2);

        await client.PostAsJsonAsync(BrandsUrl("tf"), CreateBody(codeAct, "Active Brand", active: true));
        await client.PostAsJsonAsync(BrandsUrl("tf"), new { code = codeIna, name = "Inactive Brand", active = false, imageUrl = "", imageAlt = "" });

        var resp = await client.GetAsync($"{BrandsUrl("tf")}?active=true");
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("data").EnumerateArray().ToList();

        items.Should().OnlyContain(b => b.GetProperty("active").GetBoolean());
    }

    [SkippableFact]
    public async Task List_search_filters_by_name_or_code()
    {
        Skip();
        using var client = Client(fixture);
        var code1 = Code(1);
        var code2 = Code(2);

        await client.PostAsJsonAsync(BrandsUrl("ts"), CreateBody(code1, "Luxe Heritage Group"));
        await client.PostAsJsonAsync(BrandsUrl("ts"), CreateBody(code2, "Velocity Tech"));

        var resp = await client.GetAsync($"{BrandsUrl("ts")}?search=Luxe");
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("data").EnumerateArray().ToList();

        items.Should().ContainSingle();
        items[0].GetProperty("name").GetString().Should().Be("Luxe Heritage Group");
    }

    // ── UPDATE ────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Put_updates_name_and_active()
    {
        Skip();
        using var client = Client(fixture);
        var code = Code(1);

        await client.PostAsJsonAsync(BrandsUrl(), CreateBody(code, "Original Name", active: true));

        var putResp = await client.PutAsJsonAsync(BrandUrl(code), Body("Updated Name", active: false));
        putResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = JsonDocument.Parse(await putResp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("name").GetString().Should().Be("Updated Name");
        doc.RootElement.GetProperty("data").GetProperty("active").GetBoolean().Should().BeFalse();
    }

    [SkippableFact]
    public async Task Put_nonexistent_returns_404()
    {
        Skip();
        using var client = Client(fixture);

        var resp = await client.PutAsJsonAsync(BrandUrl("NON-999"), Body("Name"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE ────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Delete_removes_brand_and_subsequent_get_returns_404()
    {
        Skip();
        using var client = Client(fixture);
        var code = Code(1);

        await client.PostAsJsonAsync(BrandsUrl(), CreateBody(code, "To Delete"));

        var delResp = await client.DeleteAsync(BrandUrl(code));
        delResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResp = await client.GetAsync(BrandUrl(code));
        getResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SkippableFact]
    public async Task Delete_nonexistent_returns_404()
    {
        Skip();
        using var client = Client(fixture);

        var resp = await client.DeleteAsync(BrandUrl("NON-999"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Cross-tenant isolation ────────────────────────────────────────────────

    [SkippableFact]
    public async Task Cross_tenant_isolation_brand_from_tenantA_not_visible_in_tenantB()
    {
        Skip();
        using var client = Client(fixture);
        var code = Code(1);
        var tenantA = $"isoa-{_id}";

        await client.PostAsJsonAsync(BrandsUrl("isoa"), CreateBody(code, "Tenant A Brand"));

        // List for tenant B should not contain tenant A's brand
        var resp = await client.GetAsync(BrandsUrl("isob"));
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("data").EnumerateArray().ToList();

        items.Should().NotContain(b => b.GetProperty("code").GetString() == code &&
                                       b.GetProperty("tenantId").GetString() == tenantA);

        // Direct GET for isob/code should 404
        var getResp = await client.GetAsync(BrandUrl(code, "isob"));
        getResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
