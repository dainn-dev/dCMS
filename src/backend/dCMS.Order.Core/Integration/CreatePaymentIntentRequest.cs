namespace dCMS.Order.Core.Integration;

/// <summary>Payload for Payment Service <c>POST /internal/payment/create-intent</c> (DAI-315).</summary>
public sealed record CreatePaymentIntentRequest(
    string OrderId,
    string TenantId,
    string StoreId,
    string CustomerId,
    decimal Amount,
    string Currency,
    string PaymentMethod = "card");
