using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace dCMS.Order.Infrastructure.Shipping;

/// <summary>
/// Config-driven mapping (DAI-334).
///
/// Supported formats:
/// - Nested section (recommended): Shipment:CarrierStatusMapping:{carrier}:{external} = delivered|in_transit|...
/// - Legacy keys: Shipment:Carriers:{carrier}:StatusMap:{external} = delivered|in_transit|...
/// </summary>
public sealed class ConfigCarrierStatusMapper(IConfiguration configuration, ILogger<ConfigCarrierStatusMapper> logger)
    : ICarrierStatusMapper
{
    private readonly Dictionary<string, Dictionary<string, MappedStatus>> _map =
        LoadNested(configuration.GetSection("Shipment:CarrierStatusMapping"))
        ?? LoadNested(configuration.GetSection("CarrierStatusMapping"))
        ?? new Dictionary<string, Dictionary<string, MappedStatus>>(StringComparer.OrdinalIgnoreCase);

    public MappedStatus Map(string carrier, string externalStatus)
    {
        if (string.IsNullOrWhiteSpace(carrier) || string.IsNullOrWhiteSpace(externalStatus))
            return MappedStatus.Unknown;

        carrier = carrier.Trim();
        externalStatus = externalStatus.Trim();

        if (_map.TryGetValue(carrier, out var statuses) && statuses.TryGetValue(externalStatus, out var mapped))
            return mapped;

        // Legacy lookup for backward compatibility.
        var key = $"Shipment:Carriers:{carrier}:StatusMap:{externalStatus}";
        var legacy = configuration[key];
        if (!string.IsNullOrWhiteSpace(legacy) && TryParseMappedStatus(legacy.Trim(), out var parsed))
            return parsed;

        logger.LogWarning("Unknown carrier status mapping: {Carrier} {ExternalStatus}", carrier, externalStatus);
        return MappedStatus.Unknown;
    }

    private static Dictionary<string, Dictionary<string, MappedStatus>>? LoadNested(IConfigurationSection root)
    {
        if (!root.Exists())
            return null;

        var carriers = new Dictionary<string, Dictionary<string, MappedStatus>>(StringComparer.OrdinalIgnoreCase);
        foreach (var carrierSection in root.GetChildren())
        {
            if (string.IsNullOrWhiteSpace(carrierSection.Key))
                continue;

            var statusMap = new Dictionary<string, MappedStatus>(StringComparer.OrdinalIgnoreCase);
            foreach (var statusSection in carrierSection.GetChildren())
            {
                var external = statusSection.Key?.Trim();
                var mappedRaw = statusSection.Value?.Trim();
                if (string.IsNullOrWhiteSpace(external) || string.IsNullOrWhiteSpace(mappedRaw))
                    continue;

                if (TryParseMappedStatus(mappedRaw, out var mapped))
                    statusMap[external] = mapped;
            }

            if (statusMap.Count > 0)
                carriers[carrierSection.Key.Trim()] = statusMap;
        }

        return carriers.Count == 0 ? null : carriers;
    }

    private static bool TryParseMappedStatus(string raw, out MappedStatus mapped)
    {
        mapped = raw.Trim().ToLowerInvariant() switch
        {
            "pending" => MappedStatus.Pending,
            "in_transit" => MappedStatus.InTransit,
            "intransit" => MappedStatus.InTransit,
            "delivered" => MappedStatus.Delivered,
            "failed" => MappedStatus.Failed,
            "cancelled" => MappedStatus.Cancelled,
            "canceled" => MappedStatus.Cancelled,
            _ => MappedStatus.Unknown,
        };
        return mapped != MappedStatus.Unknown;
    }
}

