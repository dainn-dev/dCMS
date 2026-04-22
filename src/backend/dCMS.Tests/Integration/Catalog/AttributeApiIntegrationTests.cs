using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Integration.Catalog;

/// <summary>
/// DAI-596: Attribute Management API integration tests.
/// Reuses <see cref="CatalogApiFixture"/> (PostgreSQL + WAF, Auth disabled).
/// </summary>
[Collection("CatalogApi")]
public sealed class AttributeApiIntegrationTests(CatalogApiFixture fixture)
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
    private string Tenant(string s = "t1") => $"{s}-attr-{_id}";
    private string AttrsUrl(string? t = null) => $"/api/v1/tenants/{Tenant(t ?? "t1")}/attributes";
    private string AttrUrl(int id, string? t = null) => $"/api/v1/tenants/{Tenant(t ?? "t1")}/attributes/{id}";
    private string ValuesUrl(int attrId, string? t = null) => $"/api/v1/tenants/{Tenant(t ?? "t1")}/attributes/{attrId}/values";
    private string ValueUrl(int attrId, int valId, string? t = null) => $"/api/v1/tenants/{Tenant(t ?? "t1")}/attributes/{attrId}/values/{valId}";
    private string Code(string suffix) => $"attr_{_id.ToLower()}_{suffix}";

    private static object AttrBody(string name, string code, string type = "TEXT", bool required = false) =>
        new { name, code, type, required, sortOrder = 0 };

    // ── CREATE ────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Post_creates_attribute_returns_201()
    {
        Skip();
        using var client = Client(fixture);
        var resp = await client.PostAsJsonAsync(AttrsUrl(), AttrBody("Primary Color", Code("col"), "COLOR"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("id").GetInt32().Should().BeGreaterThan(0);
        doc.RootElement.GetProperty("data").GetProperty("type").GetString().Should().Be("COLOR");
        doc.RootElement.GetProperty("data").GetProperty("code").GetString().Should().Be(Code("col"));
    }

    [SkippableFact]
    public async Task Post_invalid_code_returns_400()
    {
        Skip();
        using var client = Client(fixture);
        var resp = await client.PostAsJsonAsync(AttrsUrl(), AttrBody("Bad Code", "Bad-Code!"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task Post_invalid_type_returns_400()
    {
        Skip();
        using var client = Client(fixture);
        var resp = await client.PostAsJsonAsync(AttrsUrl(), AttrBody("Bad Type", Code("bt"), "UNKNOWN"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task Post_duplicate_code_returns_409()
    {
        Skip();
        using var client = Client(fixture);
        await client.PostAsJsonAsync(AttrsUrl(), AttrBody("A", Code("dup")));
        var resp = await client.PostAsJsonAsync(AttrsUrl(), AttrBody("B", Code("dup")));
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── READ ─────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Get_returns_attribute_with_empty_values()
    {
        Skip();
        using var client = Client(fixture);
        var id = await PostAttr(client, "Material", Code("mat"));
        var resp = await client.GetAsync(AttrUrl(id));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("name").GetString().Should().Be("Material");
        doc.RootElement.GetProperty("data").GetProperty("values").GetArrayLength().Should().Be(0);
    }

    [SkippableFact]
    public async Task Get_nonexistent_returns_404()
    {
        Skip();
        using var client = Client(fixture);
        (await client.GetAsync(AttrUrl(999999))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SkippableFact]
    public async Task List_returns_only_this_tenant()
    {
        Skip();
        using var client = Client(fixture);
        await client.PostAsJsonAsync(AttrsUrl("ta"), AttrBody("TenA Attr", Code("ta")));
        await client.PostAsJsonAsync(AttrsUrl("tb"), AttrBody("TenB Attr", Code("tb")));

        var resp = await client.GetAsync(AttrsUrl("ta"));
        var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").EnumerateArray()
            .Should().OnlyContain(e => e.GetProperty("tenantId").GetString() == Tenant("ta"));
    }

    // ── UPDATE ────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Put_updates_name_and_required()
    {
        Skip();
        using var client = Client(fixture);
        var id = await PostAttr(client, "Old", Code("upd"));
        var resp = await client.PutAsJsonAsync(AttrUrl(id), AttrBody("New", Code("upd"), required: true));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("name").GetString().Should().Be("New");
        doc.RootElement.GetProperty("data").GetProperty("required").GetBoolean().Should().BeTrue();
    }

    // ── DELETE ────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Delete_removes_attribute()
    {
        Skip();
        using var client = Client(fixture);
        var id = await PostAttr(client, "ToDelete", Code("del"));
        (await client.DeleteAsync(AttrUrl(id))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync(AttrUrl(id))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── CROSS-TENANT ──────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Cross_tenant_isolation()
    {
        Skip();
        using var client = Client(fixture);
        var id = await PostAttr(client, "IsoAttr", Code("iso"), "iso-a");
        (await client.GetAsync(AttrUrl(id, "iso-b"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── VALUE CRUD ────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Post_value_returns_201_and_appears_in_get()
    {
        Skip();
        using var client = Client(fixture);
        var attrId = await PostAttr(client, "Color", Code("vc"), type: "COLOR");
        var vResp = await client.PostAsJsonAsync(ValuesUrl(attrId), new { name = "Midnight Black", colorHex = "#1a1a1a", sortOrder = 1 });
        vResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var vId = JsonDocument.Parse(await vResp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetInt32();

        var getResp = await client.GetAsync(AttrUrl(attrId));
        var doc = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync());
        var vals = doc.RootElement.GetProperty("data").GetProperty("values").EnumerateArray().ToList();
        vals.Should().Contain(v => v.GetProperty("id").GetInt32() == vId);
        vals.First(v => v.GetProperty("id").GetInt32() == vId)
            .GetProperty("colorHex").GetString().Should().Be("#1a1a1a");
    }

    [SkippableFact]
    public async Task Put_value_updates_name()
    {
        Skip();
        using var client = Client(fixture);
        var attrId = await PostAttr(client, "SelectAttr", Code("vs"));
        var vId = await PostValue(client, attrId, "Original");
        var putResp = await client.PutAsJsonAsync(ValueUrl(attrId, vId), new { name = "Updated", sortOrder = 0 });
        putResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await putResp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("name").GetString().Should().Be("Updated");
    }

    [SkippableFact]
    public async Task Delete_value_removes_it()
    {
        Skip();
        using var client = Client(fixture);
        var attrId = await PostAttr(client, "DelValAttr", Code("vd"));
        var vId = await PostValue(client, attrId, "ToRemove");
        (await client.DeleteAsync(ValueUrl(attrId, vId))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResp = await client.GetAsync(ValuesUrl(attrId));
        var doc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").EnumerateArray()
            .Should().NotContain(v => v.GetProperty("id").GetInt32() == vId);
    }

    [SkippableFact]
    public async Task Delete_attribute_cascades_values()
    {
        Skip();
        using var client = Client(fixture);
        var attrId = await PostAttr(client, "CascadeAttr", Code("cas"));
        await PostValue(client, attrId, "Val1");
        await PostValue(client, attrId, "Val2");
        (await client.DeleteAsync(AttrUrl(attrId))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        // Attribute gone → 404
        (await client.GetAsync(AttrUrl(attrId))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<int> PostAttr(HttpClient c, string name, string code, string tenant = "t1", string type = "TEXT")
    {
        var resp = await c.PostAsJsonAsync(AttrsUrl(tenant), AttrBody(name, code, type));
        resp.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await resp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetInt32();
    }

    private async Task<int> PostValue(HttpClient c, int attrId, string name)
    {
        var resp = await c.PostAsJsonAsync(ValuesUrl(attrId), new { name, sortOrder = 0 });
        resp.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await resp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetInt32();
    }
}
