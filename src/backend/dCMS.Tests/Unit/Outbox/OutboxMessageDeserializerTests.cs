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

    [Fact]
    public void Deserializes_OrderPlaced_payload_to_OrderPlacedV1()
    {
        const string json =
            """{"orderId":"11111111-1111-1111-1111-111111111111","tenantId":"t1","storeId":"s1","customerId":"c1","totalAmount":10,"currency":"USD","lines":[{"variantId":"v1","warehouseId":"w1","quantity":2}],"occurredAt":"2026-04-12T12:00:00Z"}""";
        var o = OutboxMessageDeserializer.Deserialize("OrderPlaced", json);
        o.Should().BeOfType<OrderPlacedV1>();
        var m = (OrderPlacedV1)o!;
        m.OrderId.Should().Be("11111111-1111-1111-1111-111111111111");
        m.Lines.Should().ContainSingle();
        m.Lines[0].VariantId.Should().Be("v1");
        m.Lines[0].WarehouseId.Should().Be("w1");
        m.Lines[0].Quantity.Should().Be(2);
    }

    [Fact]
    public void Deserializes_OrderShipped_payload()
    {
        const string json =
            """{"orderId":"11111111-1111-1111-1111-111111111111","occurredAt":"2026-04-12T12:00:00Z"}""";
        var o = OutboxMessageDeserializer.Deserialize("OrderShipped", json);
        o.Should().BeOfType<OrderShippedV1>();
        var m = (OrderShippedV1)o!;
        m.OrderId.Should().Be("11111111-1111-1111-1111-111111111111");
    }

    [Fact]
    public void Deserializes_OrderDelivered_payload()
    {
        const string json =
            """{"orderId":"11111111-1111-1111-1111-111111111111","occurredAt":"2026-04-12T12:00:00Z"}""";
        var o = OutboxMessageDeserializer.Deserialize("OrderDelivered", json);
        o.Should().BeOfType<OrderDeliveredV1>();
        var m = (OrderDeliveredV1)o!;
        m.OrderId.Should().Be("11111111-1111-1111-1111-111111111111");
    }
}
