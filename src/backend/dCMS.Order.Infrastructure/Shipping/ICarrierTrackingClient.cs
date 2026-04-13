namespace dCMS.Order.Infrastructure.Shipping;

/// <summary>US-22 / DAI-335 — carrier tracking API client used by polling worker.</summary>
public interface ICarrierTrackingClient
{
    /// <summary>Fetch latest carrier status for a tracking number.</summary>
    Task<CarrierTrackingResult?> GetLatestAsync(
        string carrier,
        string trackingNumber,
        CancellationToken cancellationToken = default);
}

public sealed record CarrierTrackingResult(
    string ExternalStatus,
    DateTimeOffset OccurredAt,
    string? Location,
    string RawPayloadJson);

