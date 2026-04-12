using System.Text.Json;
using dCMS.Core.Messaging;
using PactNet;
using Xunit;

namespace dCMS.Tests.Pact;

/// <summary>DAI-307 — Consumer-driven message pact: Inventory consumes <see cref="ProductUpdatedV1"/> from Catalog.</summary>
public sealed class InventoryCatalogProductMessagePactConsumerTests
{
    private static string PactOutputDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "pacts"));

    [Fact]
    public void Inventory_defines_expected_ProductUpdated_v1_from_catalog()
    {
        Directory.CreateDirectory(PactOutputDir);

        var pact = global::PactNet.Pact.V4("Inventory", "Catalog", new PactConfig
        {
            PactDir = PactOutputDir,
            DefaultJsonSettings = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
        });

        var sample = new ProductUpdatedV1(
            "00000000-0000-0000-0000-000000000010",
            "t1",
            "s1",
            new DateTimeOffset(2026, 4, 12, 12, 0, 0, TimeSpan.Zero));

        pact.WithMessageInteractions()
            .ExpectsToReceive("ProductUpdated.v1 from catalog outbox")
            .Given("product exists for tenant")
            .WithJsonContent(sample)
            .Verify<ProductUpdatedV1>(m =>
            {
                Assert.Equal(sample.ProductId, m.ProductId);
                Assert.Equal(sample.TenantId, m.TenantId);
                Assert.Equal(sample.StoreId, m.StoreId);
                Assert.Equal(sample.OccurredAt, m.OccurredAt);
            });
    }
}
