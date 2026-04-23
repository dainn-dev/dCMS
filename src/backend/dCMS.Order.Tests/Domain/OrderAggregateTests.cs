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

    [Fact]
    public void MarkFailed_sets_failure_fields_and_raises_OrderFailed()
    {
        var order = OrderRoot.Create("ord-1", "t1", "s1", "c1", [Line()], PlaceLines(), Address(), T0);
        order.ClearDomainEvents();

        var at = T0.AddMinutes(5);
        order.MarkFailed(OrderStatus.PaymentFailed, "gateway_timeout", "PAY_408", at);

        Assert.Equal(OrderStatus.PaymentFailed, order.Status);
        Assert.Equal("gateway_timeout", order.FailureReason);
        Assert.Equal("PAY_408", order.FailureErrorCode);
        Assert.Equal(at, order.FailedAt);
        Assert.Equal(0, order.RetryCount);

        var ev = Assert.Single(order.DomainEvents);
        var failed = Assert.IsType<OrderFailed>(ev);
        Assert.Equal("ord-1", failed.OrderId);
        Assert.Equal("t1", failed.TenantId);
        Assert.Equal("s1", failed.StoreId);
        Assert.Equal(nameof(OrderStatus.PaymentFailed), failed.FailureStatus);
        Assert.Equal("gateway_timeout", failed.FailureReason);
        Assert.Equal("PAY_408", failed.FailureErrorCode);
        Assert.Equal(at, failed.FailedAt);
    }

    [Fact]
    public void RetryFailure_increments_RetryCount_and_clears_failure_context()
    {
        var order = OrderRoot.Create("ord-1", "t1", "s1", "c1", [Line()], PlaceLines(), Address(), T0);
        order.MarkFailed(OrderStatus.SystemError, "boom", null, T0.AddMinutes(1));
        order.ClearDomainEvents();

        order.RetryFailure(T0.AddMinutes(2));

        Assert.Equal(OrderStatus.PaymentPending, order.Status);
        Assert.Equal(1, order.RetryCount);
        Assert.Null(order.FailureReason);
        Assert.Null(order.FailureErrorCode);
        Assert.Null(order.FailedAt);
        Assert.Single(order.DomainEvents);
    }
}
