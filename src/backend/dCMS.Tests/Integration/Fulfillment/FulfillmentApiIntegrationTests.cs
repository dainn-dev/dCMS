using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Integration.Fulfillment;

[Collection("FulfillmentApi")]
public sealed class FulfillmentApiIntegrationTests(FulfillmentApiFixture fixture)
{
    private void Skip() =>
        Xunit.Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

    private static HttpClient Client(FulfillmentApiFixture f) => f.Factory!.CreateClient();

    private string Tenant() => $"t1-ff-{Guid.NewGuid():N}".Substring(0, 24);
    private string Base(string tenant) => $"/api/v1/tenants/{tenant}/fulfillment";

    [SkippableFact]
    public async Task Post_grouping_then_get_returns_row()
    {
        Skip();
        var tenant = Tenant();
        using var client = Client(fixture);
        var create = await client.PostAsJsonAsync($"{Base(tenant)}/groupings", new
        {
            groupName = "North Region",
            code = "NORTH_1",
            startDate = "2026-01-01",
            endDate = "2026-12-31",
            deliveryMode = "Local Delivery",
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement.GetProperty("data")
            .GetProperty("id").GetString();
        id.Should().NotBeNullOrEmpty();

        var get = await client.GetAsync($"{Base(tenant)}/groupings/{id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("code").GetString().Should().Be("NORTH_1");
    }

    [SkippableFact]
    public async Task Post_slot_under_grouping_lists_back()
    {
        Skip();
        var tenant = Tenant();
        using var client = Client(fixture);
        var gResp = await client.PostAsJsonAsync($"{Base(tenant)}/groupings", new
        {
            groupName = "G1",
            code = "G1",
            startDate = "2026-04-01",
            endDate = "2026-04-30",
            deliveryMode = "Store Collection",
        });
        var gid = JsonDocument.Parse(await gResp.Content.ReadAsStringAsync()).RootElement.GetProperty("data")
            .GetProperty("id").GetString()!;

        var slotResp = await client.PostAsJsonAsync($"{Base(tenant)}/groupings/{gid}/slots", new
        {
            name = "Morning",
            code = "AM",
            mode = "Store Collection",
            startingDate = "2026-04-01",
            endingDate = "2026-04-30",
            price = "0",
        });
        slotResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetAsync($"{Base(tenant)}/groupings/{gid}/slots");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var arr = JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        arr.GetArrayLength().Should().Be(1);
    }

    [SkippableFact]
    public async Task Put_settings_round_trips()
    {
        Skip();
        var tenant = Tenant();
        using var client = Client(fixture);
        var put = await client.PutAsJsonAsync($"{Base(tenant)}/settings", new
        {
            predefinedFields = new[] { new { key = "timeslot", label = "Slot", enabled = true } },
            dynamicFields = Array.Empty<object>(),
            stockLocations = new[] { new { id = "sl1", name = "DC1", code = "DC1", active = true } },
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await client.GetAsync($"{Base(tenant)}/settings");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("stockLocations").GetArrayLength().Should().Be(1);
    }

    [SkippableFact]
    public async Task Duplicate_grouping_code_returns_409()
    {
        Skip();
        var tenant = Tenant();
        using var client = Client(fixture);
        await client.PostAsJsonAsync($"{Base(tenant)}/groupings", new
        {
            groupName = "A",
            code = "DUP",
            startDate = "2026-01-01",
            endDate = "2026-12-31",
            deliveryMode = "Local Delivery",
        });
        var second = await client.PostAsJsonAsync($"{Base(tenant)}/groupings", new
        {
            groupName = "B",
            code = "DUP",
            startDate = "2026-01-01",
            endDate = "2026-12-31",
            deliveryMode = "Local Delivery",
        });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [SkippableFact]
    public async Task Two_tenants_have_isolated_groupings_DAI_615()
    {
        Skip();
        var a = Tenant();
        var b = Tenant();
        using var client = Client(fixture);
        var createA = await client.PostAsJsonAsync($"{Base(a)}/groupings", new
        {
            groupName = "Tenant A only",
            code = "ISO_A",
            startDate = "2026-01-01",
            endDate = "2026-12-31",
            deliveryMode = "Local Delivery",
        });
        createA.StatusCode.Should().Be(HttpStatusCode.Created);
        var idA = JsonDocument.Parse(await createA.Content.ReadAsStringAsync()).RootElement.GetProperty("data")
            .GetProperty("id").GetString()!;

        var getCross = await client.GetAsync($"{Base(b)}/groupings/{idA}");
        getCross.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listB = await client.GetAsync($"{Base(b)}/groupings?pageSize=200");
        listB.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = JsonDocument.Parse(await listB.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        items.GetArrayLength().Should().Be(0);
    }
}
