using System.Text.Json;

namespace dCMS.Order.Api.Contracts;

public sealed class CreateOrderApiRequest
{
    public string? CustomerId { get; set; }
    public List<CreateOrderLineApiRequest>? Lines { get; set; }
    public CreateOrderShippingApiRequest? ShippingAddress { get; set; }
    /// <summary>DAI-649: optional customer profile snapshot fields (name, email, phone).</summary>
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    /// <summary>DAI-693: optional promo code applied to this order — evaluated against Promotions.Api at create time.</summary>
    public string? PromoCode { get; set; }
}

public sealed class CreateOrderLineApiRequest
{
    public string? LineId { get; set; }
    public string? ProductId { get; set; }
    public string? VariantId { get; set; }
    public string? WarehouseId { get; set; }
    public int Quantity { get; set; }
    public MoneyApiRequest? UnitPrice { get; set; }
    public string? ProductNameSnapshot { get; set; }
    public JsonElement VariantSnapshot { get; set; }
}

public sealed class MoneyApiRequest
{
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
}

public sealed class CreateOrderShippingApiRequest
{
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
}
