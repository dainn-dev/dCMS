namespace dCMS.Order.Infrastructure.Shipping;

/// <summary>US-22 / DAI-334 — normalized shipment statuses.</summary>
public enum MappedStatus
{
    Unknown = 0,
    Pending = 1,
    InTransit = 2,
    Delivered = 3,
    Failed = 4,
    Cancelled = 5,
}

public static class MappedStatusExtensions
{
    public static string ToDbValue(this MappedStatus s) =>
        s switch
        {
            MappedStatus.Pending => "pending",
            MappedStatus.InTransit => "in_transit",
            MappedStatus.Delivered => "delivered",
            MappedStatus.Failed => "failed",
            MappedStatus.Cancelled => "cancelled",
            _ => "unknown",
        };
}

