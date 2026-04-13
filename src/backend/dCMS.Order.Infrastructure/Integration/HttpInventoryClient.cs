using System.Net.Http.Json;
using System.Text.Json;
using dCMS.Order.Core.Integration;
using Microsoft.Extensions.Logging;

namespace dCMS.Order.Infrastructure.Integration;

/// <summary>DAI-314 — Calls Inventory <c>POST /internal/inventory/check</c> (one HTTP call per line; envelope <c>data.sufficient</c>).</summary>
public sealed class HttpInventoryClient : IInventoryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;
    private readonly ILogger<HttpInventoryClient> _logger;

    public HttpInventoryClient(HttpClient http, ILogger<HttpInventoryClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task EnsureStockAvailableAsync(
        string tenantId,
        string storeId,
        IReadOnlyList<InventoryCheckLine> lines,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
            return;

        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(lines), "Quantity must be positive for stock check.");

            var body = new CheckBody(tenantId, storeId, line.VariantId, line.WarehouseId, line.Quantity);
            using var response = await _http
                .PostAsJsonAsync("internal/inventory/check", body, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Inventory check HTTP {Status} for variant {VariantId}: {Body}",
                    (int)response.StatusCode,
                    line.VariantId,
                    payload);
                response.EnsureSuccessStatusCode();
            }

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var err) && err.ValueKind != JsonValueKind.Null)
            {
                var code = err.TryGetProperty("code", out var c) ? c.GetString() : "unknown";
                var msg = err.TryGetProperty("message", out var m) ? m.GetString() : payload;
                throw new InvalidOperationException($"Inventory check failed ({code}): {msg}");
            }

            if (!root.TryGetProperty("data", out var data) || data.ValueKind == JsonValueKind.Null)
                throw new InvalidOperationException("Inventory check returned no data envelope.");

            var sufficient = data.GetProperty("sufficient").GetBoolean();
            if (!sufficient)
            {
                var available = data.TryGetProperty("available", out var a) ? a.GetInt32() : 0;
                var requested = data.TryGetProperty("requested", out var r) ? r.GetInt32() : line.Quantity;
                throw new OutOfStockException(line.VariantId, line.WarehouseId, requested, available);
            }
        }
    }

    private sealed record CheckBody(string TenantId, string StoreId, string VariantId, string WarehouseId, int Quantity);
}
