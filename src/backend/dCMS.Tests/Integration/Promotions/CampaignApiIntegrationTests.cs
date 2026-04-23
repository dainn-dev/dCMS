using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Integration.Promotions;

/// <summary>DAI-602: Campaign API integration tests.</summary>
[Collection("PromotionsApi")]
public sealed class CampaignApiIntegrationTests(PromotionsApiFixture fixture)
{
    private static string MakeId()
    {
        var b = Guid.NewGuid().ToByteArray();
        return new string([(char)('A' + b[0] % 26), (char)('A' + b[1] % 26),
                           (char)('A' + b[2] % 26), (char)('A' + b[3] % 26)]);
    }
    private readonly string _id = MakeId();
    private void Skip() => Xunit.Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");
    private static HttpClient Client(PromotionsApiFixture f) => f.Factory!.CreateClient();
    private string Tenant(string s = "t1") => $"{s}-cmp-{_id}";
    private string Url(string? t = null) => $"/api/v1/tenants/{Tenant(t ?? "t1")}/campaigns";
    private string CmpUrl(string id, string? t = null) => $"/api/v1/tenants/{Tenant(t ?? "t1")}/campaigns/{id}";
    private string Code(string s) => $"{_id}_{s}";

    private static object CmpBody(string code, string kind = "product-discount", string channel = "Email") =>
        new { code, nameJson = """{"en":"Test Campaign"}""", editorKind = kind, channel,
              qualifiersJson = "{}", mechanicsJson = "{}", promotionDetailsJson = "{}" };

    // ── CREATE ────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Post_creates_campaign_returns_201_with_draft_state()
    {
        Skip();
        using var client = Client(fixture);
        var resp = await client.PostAsJsonAsync(Url(), CmpBody(Code("C1")));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("workflowState").GetString().Should().Be("draft");
        doc.RootElement.GetProperty("data").GetProperty("editorKind").GetString().Should().Be("product-discount");
    }

    [SkippableFact]
    public async Task Post_duplicate_code_returns_409()
    {
        Skip();
        using var client = Client(fixture);
        await client.PostAsJsonAsync(Url(), CmpBody(Code("C2")));
        (await client.PostAsJsonAsync(Url(), CmpBody(Code("C2")))).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
    }

    [SkippableFact]
    public async Task Post_invalid_editor_kind_returns_400()
    {
        Skip();
        using var client = Client(fixture);
        (await client.PostAsJsonAsync(Url(), CmpBody(Code("C3"), kind: "unknown")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact]
    public async Task Post_invalid_code_returns_400()
    {
        Skip();
        using var client = Client(fixture);
        (await client.PostAsJsonAsync(Url(), CmpBody("lowercase-code")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── READ ─────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Get_returns_campaign_after_create()
    {
        Skip();
        using var client = Client(fixture);
        var id = await PostCmp(client, Code("G1"));
        var resp = await client.GetAsync(CmpUrl(id));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("id").GetString().Should().Be(id);
    }

    [SkippableFact]
    public async Task Get_nonexistent_returns_404()
    {
        Skip();
        using var client = Client(fixture);
        (await client.GetAsync(CmpUrl("nonexistent-id"))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    // ── UPDATE ────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Put_updates_nameJson_and_channel()
    {
        Skip();
        using var client = Client(fixture);
        var id = await PostCmp(client, Code("U1"));
        var body = new { code = Code("U1"), nameJson = """{"en":"Updated"}""",
                         editorKind = "product-discount", channel = "SMS",
                         qualifiersJson = "{}", mechanicsJson = "{}", promotionDetailsJson = "{}" };
        var resp = await client.PutAsJsonAsync(CmpUrl(id), body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("channel").GetString().Should().Be("SMS");
    }

    // ── DELETE ────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Delete_draft_returns_204()
    {
        Skip();
        using var client = Client(fixture);
        var id = await PostCmp(client, Code("D1"));
        (await client.DeleteAsync(CmpUrl(id))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync(CmpUrl(id))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SkippableFact]
    public async Task Delete_active_campaign_returns_409()
    {
        Skip();
        using var client = Client(fixture);
        var id = await PostCmp(client, Code("D2"));
        await Transition(client, id, "submit");
        await Transition(client, id, "approve");
        await Transition(client, id, "activate");
        (await client.DeleteAsync(CmpUrl(id))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── WORKFLOW ─────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Full_workflow_happy_path()
    {
        Skip();
        using var client = Client(fixture);
        var id = await PostCmp(client, Code("W1"));

        await AssertTransition(client, id, "submit",   "pending_approval");
        await AssertTransition(client, id, "approve",  "approved");
        await AssertTransition(client, id, "activate", "active");
        await AssertTransition(client, id, "pause",    "deactivated");
        await AssertTransition(client, id, "archive",  "archived");
    }

    [SkippableFact]
    public async Task Reject_workflow()
    {
        Skip();
        using var client = Client(fixture);
        var id = await PostCmp(client, Code("W2"));
        await Transition(client, id, "submit");
        await AssertTransition(client, id, "reject", "rejected");
    }

    [SkippableFact]
    public async Task Invalid_transition_returns_422()
    {
        Skip();
        using var client = Client(fixture);
        var id = await PostCmp(client, Code("W3"));
        // draft → activate is not allowed
        (await client.PostAsJsonAsync(CmpUrl(id) + "/activate", new { }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── HISTORY ───────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task History_records_transitions()
    {
        Skip();
        using var client = Client(fixture);
        var id = await PostCmp(client, Code("H1"));
        await Transition(client, id, "submit");
        await Transition(client, id, "approve");

        var resp = await client.GetAsync(CmpUrl(id) + "/history");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    // ── Cross-tenant isolation ─────────────────────────────────────────────────

    [SkippableFact]
    public async Task Cross_tenant_isolation()
    {
        Skip();
        using var client = Client(fixture);
        var id = await PostCmp(client, Code("ISO1"), "iso-a");
        (await client.GetAsync(CmpUrl(id, "iso-b"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> PostCmp(HttpClient c, string code, string? tenant = null)
    {
        var resp = await c.PostAsJsonAsync(Url(tenant), CmpBody(code));
        resp.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await resp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("id").GetString()!;
    }

    private async Task Transition(HttpClient c, string id, string action)
    {
        var resp = await c.PostAsJsonAsync(CmpUrl(id) + $"/{action}", new { });
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Response status code does not indicate success: {(int)resp.StatusCode} ({resp.StatusCode}). Body: {body}");
        }
    }

    private async Task AssertTransition(HttpClient c, string id, string action, string expectedState)
    {
        var resp = await c.PostAsJsonAsync(CmpUrl(id) + $"/{action}", new { });
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new Exception($"Transition '{action}' failed {(int)resp.StatusCode}: {body}");
        }
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("workflowState").GetString().Should().Be(expectedState);
    }
}
