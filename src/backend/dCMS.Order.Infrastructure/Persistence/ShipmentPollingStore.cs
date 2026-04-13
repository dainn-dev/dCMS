using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Order.Infrastructure.Persistence;

/// <summary>DAI-335 — query stale shipments for polling.</summary>
public sealed class ShipmentPollingStore
{
    private readonly string _connectionString;

    public ShipmentPollingStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
    }

    public async Task<IReadOnlyList<ShipmentToPoll>> ListStaleAsync(
        int limit,
        TimeSpan staleFor,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 500);
        var staleAt = DateTimeOffset.UtcNow - staleFor;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            SELECT "Id"::uuid AS Id, "Carrier", "TrackingNumber", "Status"
            FROM "Shipments"
            WHERE "Status" NOT IN ('delivered', 'failed')
              AND "UpdatedAt" < @StaleAt
            ORDER BY "UpdatedAt" ASC
            LIMIT @Take
            """;

        var rows = (await conn.QueryAsync<ShipmentToPoll>(
                new CommandDefinition(sql, new { StaleAt = staleAt.UtcDateTime, Take = take }, cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        return rows;
    }

    public sealed record ShipmentToPoll(
        Guid Id,
        string Carrier,
        string TrackingNumber,
        string Status);
}

