using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace dCMS.Tests.Integration.Inventory;

[Collection("InventoryApi")]
public sealed class InventoryApiIntegrationTests(InventoryApiPostgresFixture fixture)
{
    [SkippableFact]
    public async Task Health_returns_inventory_payload()
    {
        Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

        using var client = fixture.Factory!.CreateClient();
        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"service\":\"inventory\"");
    }

    [SkippableFact]
    public async Task Adjust_persists_quantity()
    {
        Skip.IfNot(fixture.IsReady && fixture.Factory is not null && fixture.TestConnectionString is not null,
            "Docker / Testcontainers not available.");

        await using (var conn = new NpgsqlConnection(fixture.TestConnectionString))
        {
            await conn.OpenAsync();
            var before = await conn.QuerySingleAsync<int>(
                """SELECT "Quantity" FROM "VariantStock" WHERE "VariantId" = 'var_1' AND "WarehouseId" = 'wh_1'""");

            using var client = fixture.Factory!.CreateClient();
            var body = new { variantId = "var_1", warehouseId = "wh_1", delta = 4, createdBy = "itest" };
            var response = await client.PostAsJsonAsync(
                new Uri("/api/v1/tenants/t1/stores/s1/stock/adjust", UriKind.Relative), body);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            doc.RootElement.GetProperty("data").GetProperty("ok").GetBoolean().Should().BeTrue();

            var after = await conn.QuerySingleAsync<int>(
                """SELECT "Quantity" FROM "VariantStock" WHERE "VariantId" = 'var_1' AND "WarehouseId" = 'wh_1'""");
            after.Should().Be(before + 4);
        }
    }

    [SkippableFact]
    public async Task Bulk_returns_succeeded_and_failed_rows()
    {
        Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

        using var client = fixture.Factory!.CreateClient();
        var body = new
        {
            createdBy = "itest",
            items = new object[]
            {
                new { op = "adjust", variantId = "var_1", warehouseId = "wh_1", delta = -1 },
                new { op = "reserve", variantId = "ghost", warehouseId = "wh_1", quantity = 1 }
            }
        };

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/tenants/t1/stores/s1/stock/bulk", UriKind.Relative), body);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("meta").GetProperty("requested").GetInt32().Should().Be(2);
        doc.RootElement.GetProperty("meta").GetProperty("succeeded").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("meta").GetProperty("failed").GetInt32().Should().Be(1);

        var failed = doc.RootElement.GetProperty("data").GetProperty("failed");
        failed.GetArrayLength().Should().Be(1);
        failed[0].GetProperty("code").GetString().Should().Be("not_found");
    }

    [SkippableFact]
    public async Task List_warehouses_returns_seed_rows()
    {
        Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

        using var client = fixture.Factory!.CreateClient();
        var response = await client.GetAsync(new Uri("/api/v1/tenants/t1/stores/s1/warehouses", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCountGreaterThanOrEqualTo(2);
        var ids = items.Select(e => e.GetProperty("id").GetString()).ToList();
        ids.Should().Contain("wh_1");
        ids.Should().Contain("wh_2");
    }

    [SkippableFact]
    public async Task Get_variant_stock_lists_warehouse_rows()
    {
        Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

        using var client = fixture.Factory!.CreateClient();
        var response = await client.GetAsync(new Uri("/api/v1/tenants/t1/stores/s1/stock/variants/var_1", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        items.Should().NotBeEmpty();
        items.Should().ContainSingle(i => i.GetProperty("warehouseId").GetString() == "wh_1");
    }

    [SkippableFact]
    public async Task Create_warehouse_then_duplicate_returns_409()
    {
        Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

        var id = "wh_itest_" + Guid.NewGuid().ToString("N")[..8];
        using var client = fixture.Factory!.CreateClient();
        var ok = await client.PostAsJsonAsync(new Uri("/api/v1/tenants/t1/stores/s1/warehouses", UriKind.Relative),
            new { id, name = "Integration test WH", address = (string?)null });
        ok.StatusCode.Should().Be(HttpStatusCode.OK);

        var dup = await client.PostAsJsonAsync(new Uri("/api/v1/tenants/t1/stores/s1/warehouses", UriKind.Relative),
            new { id, name = "Duplicate", address = (string?)null });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [SkippableFact]
    public async Task Internal_check_requires_api_key()
    {
        Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

        using var client = fixture.Factory!.CreateClient();
        var body = new { tenantId = "t1", storeId = "s1", variantId = "var_1", warehouseId = "wh_1", quantity = 1 };
        var noKey = await client.PostAsJsonAsync(new Uri("/internal/inventory/check", UriKind.Relative), body);
        noKey.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var bad = fixture.Factory.CreateClient();
        bad.DefaultRequestHeaders.Add("X-Internal-Api-Key", "wrong-key-wrong-key-wrong-key-wrong!!");
        var forbidden = await bad.PostAsJsonAsync(new Uri("/internal/inventory/check", UriKind.Relative), body);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task Internal_check_returns_sufficient_when_stock_allows()
    {
        Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

        using var client = fixture.Factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", "integration-test-internal-inventory-key-min-32!");
        var body = new { tenantId = "t1", storeId = "s1", variantId = "var_1", warehouseId = "wh_1", quantity = 1 };
        var response = await client.PostAsJsonAsync(new Uri("/internal/inventory/check", UriKind.Relative), body);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("found").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("data").GetProperty("sufficient").GetBoolean().Should().BeTrue();
    }
}
