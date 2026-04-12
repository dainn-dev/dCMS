using dCMS.Core.Messaging;
using dCMS.Infrastructure.Outbox;
using FluentAssertions;

namespace dCMS.Tests.Unit.Outbox;

public sealed class OutboxMessageDeserializerTests
{
    [Fact]
    public void Deserializes_ProductUpdated_payload()
    {
        const string json = """{"productId":"p1","tenantId":"t1","storeId":"s1","occurredAt":"2026-04-12T12:00:00Z"}""";
        var o = OutboxMessageDeserializer.Deserialize("ProductUpdated", json);
        o.Should().BeOfType<ProductUpdatedV1>();
        var m = (ProductUpdatedV1)o!;
        m.ProductId.Should().Be("p1");
        m.TenantId.Should().Be("t1");
        m.StoreId.Should().Be("s1");
    }

    [Fact]
    public void Deserializes_StockUpdated_v1_payload()
    {
        const string json =
            """{"variantId":"v1","warehouseId":"w1","tenantId":"t1","storeId":"s1","quantity":3,"reservedQuantity":1,"occurredAt":"2026-04-12T12:00:00Z"}""";
        var o = OutboxMessageDeserializer.Deserialize("StockUpdated.v1", json);
        o.Should().BeOfType<StockUpdatedV1>();
    }
}
