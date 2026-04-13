namespace dCMS.Order.Api.Contracts;

/// <summary>US-22 / DAI-332 — request body for <c>POST /api/orders/{id}/ship</c>.</summary>
public sealed class ShipOrderApiRequest
{
    public string Carrier { get; set; } = "";
    public string TrackingNumber { get; set; } = "";
}

