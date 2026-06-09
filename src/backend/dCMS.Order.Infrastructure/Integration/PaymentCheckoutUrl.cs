namespace dCMS.Order.Infrastructure.Integration;

/// <summary>Deterministic checkout URL for stub gateway replays (matches <see cref="dCMS.Payment.Infrastructure.Integration.StubPaymentGateway"/>).</summary>
public static class PaymentCheckoutUrl
{
    public static string ForStubGateway(Guid orderId) =>
        $"https://checkout.local/pay/{Uri.EscapeDataString(orderId.ToString("D"))}";
}
