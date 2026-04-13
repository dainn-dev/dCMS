using dCMS.Order.Core.Domain;
using OrderRoot = dCMS.Order.Core.Domain.Order;

namespace dCMS.Order.Tests.Domain;

public sealed class OrderAggregateTests
{
    private static readonly DateTimeOffset T0 = new(2026, 4, 13, 12, 0, 0, TimeSpan.Zero);

    private static ShippingAddress Address() =>
        new("1 Main St", null, "HCMC", "SG", "700000", "VN");

    private static OrderItem Line(string id = "line-1", int qty = 2, decimal unit = 10m) =>
        new(id, "prod-1", "var-1", qty, new Money(unit, "USD"), "Widget", "{\"sku\":\"W-1\"}");

    private static IReadOnlyList<OrderPlacedLine> PlaceLines(int qty = 2) =>
        [new OrderPlacedLine("var-1", "wh-1", qty)];

    [Fact]
    public void Create_raises_OrderPlaced_with_totals()
    {
        var order = OrderRoot.Create(
            "ord-1",
            "tenant-1",
            "store-1",
            "cust-1",
            [Line()],
            PlaceLines(),
            Address(),
            T0);

        Assert.Equal(OrderStatus.PaymentPending, order.Status);
        var evt = Assert.Single(order.DomainEvents);
        var placed = Assert.IsType<OrderPlaced>(evt);
        Assert.Equal("ord-1", placed.OrderId);
        Assert.Equal("tenant-1", placed.TenantId);
        Assert.Equal("store-1", placed.StoreId);
        Assert.Equal("cust-1", placed.CustomerId);
        Assert.Equal(20m, placed.TotalAmount);
        Assert.Equal("USD", placed.Currency);
        Assert.Single(placed.Lines);
        Assert.Equal("var-1", placed.Lines[0].VariantId);
        Assert.Equal("wh-1", placed.Lines[0].WarehouseId);
        Assert.Equal(2, placed.Lines[0].Quantity);
        Assert.Equal(T0, placed.OccurredAt);
    }

    [Fact]
    public void Cancel_from_payment_pending_raises_OrderCancelled()
    {
        var order = OrderRoot.Create("ord-1", "t1", "s1", "c1", [Line()], PlaceLines(), Address(), T0);
        order.ClearDomainEvents();

        order.Cancel("customer changed mind", T0.AddMinutes(1));

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        var evt = Assert.Single(order.DomainEvents);
        var cancelled = Assert.IsType<OrderCancelled>(evt);
        Assert.Equal("ord-1", cancelled.OrderId);
        Assert.Equal("customer changed mind", cancelled.Reason);
    }

    [Fact]
    public void AssignPaymentIntent_sets_id_when_payment_pending()
    {
        var order = OrderRoot.Create("ord-1", "t1", "s1", "c1", [Line()], PlaceLines(), Address(), T0);
        order.ClearDomainEvents();

        order.AssignPaymentIntent("pi_123");

        Assert.Equal("pi_123", order.PaymentIntentId);
        Assert.Empty(order.DomainEvents);
    }

    [Fact]
    public void AssignPaymentIntent_twice_throws()
    {
        var order = OrderRoot.Create("ord-1", "t1", "s1", "c1", [Line()], PlaceLines(), Address(), T0);
        order.AssignPaymentIntent("pi_1");

        Assert.Throws<InvalidOperationException>(() => order.AssignPaymentIntent("pi_2"));
    }

    [Fact]
    public void Cannot_cancel_shipped_order()
    {
        var order = OrderRoot.Create("ord-1", "t1", "s1", "c1", [Line()], PlaceLines(), Address(), T0);
        order.Confirm(T0.AddMinutes(1));
        order.StartProcessing(T0.AddMinutes(1).AddSeconds(30));
        order.MarkShipped(T0.AddMinutes(2));
        order.ClearDomainEvents();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            order.Cancel("too late", T0.AddMinutes(3)));
        Assert.Contains("shipped", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.Empty(order.DomainEvents);
    }
}
