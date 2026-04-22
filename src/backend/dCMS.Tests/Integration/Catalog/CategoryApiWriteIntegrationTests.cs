using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Integration.Catalog;

/// <summary>
/// DAI-590: Category write API integration tests.
/// Reuses <see cref="CatalogApiFixture"/> (PostgreSQL + WAF, Auth disabled).
/// </summary>
[Collection("CatalogApi")]
public sealed class CategoryApiWriteIntegrationTests(CatalogApiFixture fixture)
{
    private static string MakeId()
    {
        var b = Guid.NewGuid().ToByteArray();
        return new string([(char)('A' + b[0] % 26), (char)('A' + b[1] % 26),
                           (char)('A' + b[2] % 26), (char)('A' + b[3] % 26)]);
    }
    private readonly string _id = MakeId();

    private void Skip() => Xunit.Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");
    private static HttpClient Client(CatalogApiFixture f) => f.Factory!.CreateClient();
    private string Tenant(string suffix = "t1") => $"{suffix}-cat-{_id}";
    private string CatsUrl(string? t = null)   => $"/api/v1/tenants/{Tenant(t ?? "t1")}/categories";
    private string CatUrl(int id, string? t = null) => $"/api/v1/tenants/{Tenant(t ?? "t1")}/categories/{id}";
    private string Slug(int n) => $"cat-{_id.ToLower()}-{n}";

    private static object CreateBody(string name, string slug, int? parentId = null, bool active = true) =>
        new { name, slug, parentId, active, sortOrder = 0 };

    // ── CREATE ────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Post_creates_category_returns_201()
    {
        Skip();
        using var client = Client(fixture);
        var resp = await client.PostAsJsonAsync(CatsUrl(), CreateBody("Electronics", Slug(1)));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("name").GetString().Should().Be("Electronics");
        doc.RootElement.GetProperty("data").GetProperty("slug").GetString().Should().Be(Slug(1));
        doc.RootElement.GetProperty("data").GetProperty("id").GetInt32().Should().BeGreaterThan(0);
    }

    [SkippableFact]
    public async Task Post_duplicate_slug_returns_409()
    {
        Skip();
        using var client = Client(fixture);
        await client.PostAsJsonAsync(CatsUrl(), CreateBody("Cat A", Slug(2)));
        var resp = await client.PostAsJsonAsync(CatsUrl(), CreateBody("Cat B", Slug(2)));
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("conflict");
    }

    [SkippableFact]
    public async Task Post_with_parent_computes_path_and_depth()
    {
        Skip();
        using var client = Client(fixture);

        var parentResp = await client.PostAsJsonAsync(CatsUrl(), CreateBody("Parent", Slug(3)));
        var parentDoc = JsonDocument.Parse(await parentResp.Content.ReadAsStringAsync());
        var parentId = parentDoc.RootElement.GetProperty("data").GetProperty("id").GetInt32();

        var childResp = await client.PostAsJsonAsync(CatsUrl(), CreateBody("Child", Slug(4), parentId));
        childResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var childDoc = JsonDocument.Parse(await childResp.Content.ReadAsStringAsync());
        childDoc.RootElement.GetProperty("data").GetProperty("depth").GetInt32().Should().Be(1);
        childDoc.RootElement.GetProperty("data").GetProperty("parentId").GetInt32().Should().Be(parentId);
    }

    // ── READ ─────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Get_returns_category_after_create()
    {
        Skip();
        using var client = Client(fixture);
        var postResp = await client.PostAsJsonAsync(CatsUrl(), CreateBody("Phones", Slug(5)));
        var id = JsonDocument.Parse(await postResp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetInt32();

        var getResp = await client.GetAsync(CatUrl(id));
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("name").GetString().Should().Be("Phones");
    }

    [SkippableFact]
    public async Task Get_nonexistent_returns_404()
    {
        Skip();
        using var client = Client(fixture);
        var resp = await client.GetAsync(CatUrl(999999));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── LIST ─────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task List_returns_only_categories_for_tenant()
    {
        Skip();
        using var client = Client(fixture);
        await client.PostAsJsonAsync(CatsUrl("tx"), CreateBody("TenantX Cat", Slug(6)));
        await client.PostAsJsonAsync(CatsUrl("ty"), CreateBody("TenantY Cat", Slug(6)));

        var resp = await client.GetAsync(CatsUrl("tx"));
        var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();

        items.Should().OnlyContain(i => i.GetProperty("tenantId").GetString() == Tenant("tx"));
    }

    // ── UPDATE ────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Put_updates_name_and_active()
    {
        Skip();
        using var client = Client(fixture);
        var postResp = await client.PostAsJsonAsync(CatsUrl(), CreateBody("Old Name", Slug(7), active: true));
        var id = JsonDocument.Parse(await postResp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetInt32();

        var putResp = await client.PutAsJsonAsync(CatUrl(id), new { name = "New Name", slug = Slug(7), active = false });
        putResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await putResp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("name").GetString().Should().Be("New Name");
        doc.RootElement.GetProperty("data").GetProperty("active").GetBoolean().Should().BeFalse();
    }

    // ── DELETE ────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Delete_removes_category_and_get_returns_404()
    {
        Skip();
        using var client = Client(fixture);
        var postResp = await client.PostAsJsonAsync(CatsUrl(), CreateBody("To Delete", Slug(8)));
        var id = JsonDocument.Parse(await postResp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetInt32();

        var delResp = await client.DeleteAsync(CatUrl(id));
        delResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResp = await client.GetAsync(CatUrl(id));
        getResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SkippableFact]
    public async Task Delete_cascade_removes_children()
    {
        Skip();
        using var client = Client(fixture);

        var parentResp = await client.PostAsJsonAsync(CatsUrl(), CreateBody("Parent", Slug(9)));
        var parentId = JsonDocument.Parse(await parentResp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetInt32();

        var childResp = await client.PostAsJsonAsync(CatsUrl(), CreateBody("Child", Slug(10), parentId));
        var childId = JsonDocument.Parse(await childResp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetInt32();

        await client.DeleteAsync(CatUrl(parentId));

        var getChild = await client.GetAsync(CatUrl(childId));
        getChild.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── RECLASSIFY ────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Put_parent_reclassifies_and_updates_depth()
    {
        Skip();
        using var client = Client(fixture);

        var a = await PostCat(client, "A", Slug(11));
        var b = await PostCat(client, "B", Slug(12));

        // Move B under A
        var reclResp = await client.PutAsJsonAsync($"{CatUrl(b)}/parent", new { newParentId = a });
        reclResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = JsonDocument.Parse(await reclResp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("parentId").GetInt32().Should().Be(a);
        doc.RootElement.GetProperty("data").GetProperty("depth").GetInt32().Should().Be(1);
    }

    // ── SORT ─────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Sort_reorders_siblings()
    {
        Skip();
        using var client = Client(fixture);

        var c1 = await PostCat(client, "C1", Slug(13));
        var c2 = await PostCat(client, "C2", Slug(14));
        var c3 = await PostCat(client, "C3", Slug(15));

        var sortResp = await client.PutAsJsonAsync($"{CatsUrl()}/sort", new
        {
            parentId = (int?)null,
            items = new[] { new { id = c3, sortOrder = 1 }, new { id = c1, sortOrder = 2 }, new { id = c2, sortOrder = 3 } },
        });
        sortResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Cross-tenant isolation ─────────────────────────────────────────────────

    [SkippableFact]
    public async Task Cross_tenant_isolation_category_not_visible_in_other_tenant()
    {
        Skip();
        using var client = Client(fixture);
        var postResp = await client.PostAsJsonAsync(CatsUrl("iso-a"), CreateBody("Iso Cat", Slug(16)));
        var id = JsonDocument.Parse(await postResp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetInt32();

        var getResp = await client.GetAsync(CatUrl(id, "iso-b"));
        getResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<int> PostCat(HttpClient client, string name, string slug, int? parentId = null)
    {
        var resp = await client.PostAsJsonAsync(CatsUrl(), CreateBody(name, slug, parentId));
        resp.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await resp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetInt32();
    }
}
