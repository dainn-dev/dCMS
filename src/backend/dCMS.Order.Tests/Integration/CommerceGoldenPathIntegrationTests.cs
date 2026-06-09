using dCMS.Order.Core.Cart;
using dCMS.Order.Core.Integration;
using dCMS.Order.Core.Ordering;
using dCMS.Order.Infrastructure.Cart;
using Moq;

namespace dCMS.Order.Tests.Integration;

/// <summary>Commerce MVP golden path evidence: cart storage and sync payment intent contract.</summary>
public sealed class CommerceGoldenPathIntegrationTests
{
    [Fact]
    public async Task Cart_lines_feed_checkout_command_shape()
    {
        var carts = new InMemoryCartStore();
        await carts.UpsertLineAsync("t1", "s1", "cust-1", new UpsertCartLineRequest(
            "line-1", "prod-1", "var-1", "wh-1", 2, 9.99m, "USD", "Widget", """{"sku":"W"}"""));

        var snapshot = await carts.GetAsync("t1", "s1", "cust-1");
        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot!.Lines[0].Quantity);

        var cmd = new CreateOrderCommand(
            Guid.NewGuid().ToString(),
            snapshot.TenantId,
            snapshot.StoreId,
            "cust-1",
            $"idem-{Guid.NewGuid():N}",
            snapshot.Lines.Select(l => new CreateOrderLine(
                l.LineId, l.ProductId, l.VariantId, l.WarehouseId, l.Quantity,
                new Core.Domain.Money(l.UnitPriceAmount, l.Currency), l.ProductNameSnapshot, l.VariantSnapshotJson)).ToList(),
            new Core.Domain.ShippingAddress("1 Main", null, "City", "Region", "1", "VN"),
            DateTimeOffset.UtcNow);

        Assert.Single(cmd.Lines);
        Assert.Equal("var-1", cmd.Lines[0].VariantId);
    }

    [Fact]
    public async Task Payment_client_create_intent_returns_checkout_url_for_golden_path()
    {
        var payment = new Mock<IPaymentClient>();
        var orderId = Guid.NewGuid().ToString();
        payment.Setup(p => p.CreatePaymentIntentAsync(It.IsAny<CreatePaymentIntentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntentResult("pi_test", "https://pay.example/checkout"));

        var result = await payment.Object.CreatePaymentIntentAsync(
            new CreatePaymentIntentRequest(orderId, "t1", "s1", "cust-1", 5m, "USD"));

        Assert.Equal("https://pay.example/checkout", result.PaymentUrl);
        Assert.Equal("pi_test", result.PaymentIntentId);
    }
}
