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
    public void Create_persists_customer_snapshot_when_provided()
    {
        var order = OrderRoot.Create(
            "ord-1", "t1", "s1", "c1",
            [Line()], PlaceLines(), Address(), T0,
            customerName: "  Jane Doe  ",
            customerEmail: "jane@example.com",
            customerPhone: "+1-555-0100");

        Assert.Equal("Jane Doe", order.CustomerName);
        Assert.Equal("jane@example.com", order.CustomerEmail);
        Assert.Equal("+1-555-0100", order.CustomerPhone);
    }

    [Fact]
    public void Create_normalizes_blank_snapshot_to_null()
    {
        var order = OrderRoot.Create(
            "ord-1", "t1", "s1", "c1",
            [Line()], PlaceLines(), Address(), T0,
            customerName: "   ",
            customerEmail: "",
            customerPhone: null);

        Assert.Null(order.CustomerName);
        Assert.Null(order.CustomerEmail);
        Assert.Null(order.CustomerPhone);
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

    // ── DAI-725 / DAI-693 — promotion snapshot invariants ────────────────────

    [Fact]
    public void Create_with_no_promotion_keeps_default_zero_discounts()
    {
        var order = OrderRoot.Create("ord-1", "t1", "s1", "c1", [Line(qty: 2, unit: 10m)], PlaceLines(), Address(), T0);

        Assert.Equal(0m, order.OrderDiscount);
        Assert.Null(order.PromoCode);
        Assert.Null(order.PromoCodeId);
        Assert.Empty(order.AppliedPromotions);
        Assert.Equal(20m, order.Total.Amount);
        Assert.Equal(0m, order.Items[0].LineDiscount);
        Assert.Equal(20m, order.Items[0].LineTotal().Amount);
    }

    [Fact]
    public void Create_with_line_discount_subtracts_from_line_total()
    {
        var item = new OrderItem(
            "line-1", "prod-1", "var-1", 2, new Money(10m, "USD"), "Widget", "{\"sku\":\"W-1\"}",
            lineDiscount: 5m);

        var order = OrderRoot.Create("ord-1", "t1", "s1", "c1", [item], PlaceLines(), Address(), T0);

        Assert.Equal(5m, order.Items[0].LineDiscount);
        Assert.Equal(15m, order.Items[0].LineTotal().Amount);
        Assert.Equal(15m, order.Total.Amount);
    }

    [Fact]
    public void Create_with_order_discount_subtracts_from_total()
    {
        var snapshot = new AppliedPromotionSnapshot("p-1", "camp-1", "product-discount", "10% off", 2m, null);
        var order = OrderRoot.Create(
            "ord-1", "t1", "s1", "c1", [Line(qty: 2, unit: 10m)], PlaceLines(), Address(), T0,
            orderDiscount: 2m,
            appliedPromotions: [snapshot]);

        Assert.Equal(2m, order.OrderDiscount);
        Assert.Equal(18m, order.Total.Amount);
        Assert.Single(order.AppliedPromotions);
        Assert.Equal("camp-1", order.AppliedPromotions[0].CampaignId);
    }

    [Fact]
    public void Create_with_promo_code_persists_code_and_id()
    {
        var snapshot = new AppliedPromotionSnapshot("p-1", "camp-2", "mix-match", "Bundle deal", 5m, "SUMMER10");
        var order = OrderRoot.Create(
            "ord-1", "t1", "s1", "c1", [Line(qty: 2, unit: 10m)], PlaceLines(), Address(), T0,
            orderDiscount: 5m,
            promoCode: " SUMMER10 ",
            promoCodeId: "code-uuid-1",
            appliedPromotions: [snapshot]);

        Assert.Equal("SUMMER10", order.PromoCode);
        Assert.Equal("code-uuid-1", order.PromoCodeId);
        Assert.Equal(15m, order.Total.Amount);
    }

    [Fact]
    public void Create_normalises_blank_promo_code_to_null()
    {
        var order = OrderRoot.Create(
            "ord-1", "t1", "s1", "c1", [Line()], PlaceLines(), Address(), T0,
            promoCode: "   ",
            promoCodeId: "");

        Assert.Null(order.PromoCode);
        Assert.Null(order.PromoCodeId);
    }

    [Fact]
    public void Create_with_negative_order_discount_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrderRoot.Create(
            "ord-1", "t1", "s1", "c1", [Line()], PlaceLines(), Address(), T0,
            orderDiscount: -1m));
    }

    [Fact]
    public void Create_with_order_discount_above_subtotal_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrderRoot.Create(
            "ord-1", "t1", "s1", "c1", [Line(qty: 2, unit: 10m)], PlaceLines(), Address(), T0,
            orderDiscount: 50m));
    }

    [Fact]
    public void OrderItem_with_negative_line_discount_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrderItem(
            "line-1", "prod-1", "var-1", 1, new Money(10m, "USD"), "Widget", "{}",
            lineDiscount: -1m));
    }

    [Fact]
    public void OrderItem_with_line_discount_above_gross_total_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrderItem(
            "line-1", "prod-1", "var-1", 1, new Money(10m, "USD"), "Widget", "{}",
            lineDiscount: 11m));
    }

    [Fact]
    public void Total_clamps_at_zero_when_full_discount_applied()
    {
        var item = new OrderItem(
            "line-1", "prod-1", "var-1", 2, new Money(10m, "USD"), "Widget", "{}",
            lineDiscount: 20m);

        var order = OrderRoot.Create("ord-1", "t1", "s1", "c1", [item], PlaceLines(), Address(), T0);

        Assert.Equal(0m, order.Total.Amount);
        Assert.Equal(0m, order.Items[0].LineTotal().Amount);
    }

    [Fact]
    public void Create_emits_OrderPlaced_with_discounted_total()
    {
        var item = new OrderItem(
            "line-1", "prod-1", "var-1", 2, new Money(10m, "USD"), "Widget", "{}",
            lineDiscount: 4m);

        var order = OrderRoot.Create("ord-1", "t1", "s1", "c1", [item], PlaceLines(), Address(), T0,
            orderDiscount: 2m);

        var placed = Assert.IsType<OrderPlaced>(Assert.Single(order.DomainEvents));
        Assert.Equal(14m, placed.TotalAmount);
        Assert.Equal("USD", placed.Currency);
    }

    [Fact]
    public void FromPersistence_round_trips_promotion_fields()
    {
        var item = new OrderItem(
            "line-1", "prod-1", "var-1", 2, new Money(10m, "USD"), "Widget", "{}",
            lineDiscount: 4m);
        var snap = new AppliedPromotionSnapshot("p-1", "camp-1", "product-discount", "10% off", 2m, "SAVE10");

        var order = OrderRoot.FromPersistence(
            "ord-1", "t1", "s1", "c1",
            OrderStatus.Confirmed,
            new Money(14m, "USD"),
            Address(),
            [item],
            paymentIntentId: "pi-1",
            orderDiscount: 2m,
            promoCode: "SAVE10",
            promoCodeId: "code-1",
            appliedPromotions: [snap]);

        Assert.Equal(2m, order.OrderDiscount);
        Assert.Equal("SAVE10", order.PromoCode);
        Assert.Equal("code-1", order.PromoCodeId);
        Assert.Single(order.AppliedPromotions);
        Assert.Empty(order.DomainEvents);
    }
}
