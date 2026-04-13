namespace dCMS.Order.Infrastructure.Shipping;

/// <summary>US-22 / DAI-334 — normalize carrier-specific statuses into dCMS shipment statuses.</summary>
public interface ICarrierStatusMapper
{
    /// <summary>Returns mapped status; unknown input maps to <see cref="MappedStatus.Unknown"/>.</summary>
    MappedStatus Map(string carrier, string externalStatus);
}

