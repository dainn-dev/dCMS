using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Order.Infrastructure.Persistence;

/// <summary>US-22 / DAI-332 — Shipment read model + events.</summary>
public sealed class ShipmentQueryStore
{
    private readonly string _connectionString;

    public ShipmentQueryStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
    }

    public async Task<ShipmentDetail?> GetByOrderIdAsync(string orderId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(orderId, out var oid))
            return null;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string shipSql = """
            SELECT "Id", "OrderId", "Carrier", "TrackingNumber", "Status", "EstimatedAt", "DeliveredAt", "CreatedAt", "UpdatedAt"
            FROM "Shipments"
            WHERE "OrderId" = @OrderId
            """;

        var ship = await conn.QuerySingleOrDefaultAsync<ShipmentRow>(
            new CommandDefinition(shipSql, new { OrderId = oid }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (ship is null)
            return null;

        const string evSql = """
            SELECT "Status", "Location", "OccurredAt", "Payload"::text AS PayloadJson
            FROM "ShipmentEvents"
            WHERE "ShipmentId" = @ShipmentId
            ORDER BY "OccurredAt" ASC, "Id" ASC
            """;

        var events = (await conn.QueryAsync<ShipmentEventRow>(
                new CommandDefinition(evSql, new { ShipmentId = ship.Id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        return new ShipmentDetail(
            ship.OrderId.ToString("D"),
            ship.Carrier,
            ship.TrackingNumber,
            ship.Status,
            ToUtcOffset(ship.EstimatedAt),
            ToUtcOffset(ship.DeliveredAt),
            events.Select(e => new ShipmentEventDetail(e.Status, e.Location, ToUtcOffset(e.OccurredAt)!.Value, e.PayloadJson)).ToList());
    }

    public sealed record ShipmentDetail(
        string OrderId,
        string Carrier,
        string TrackingNumber,
        string Status,
        DateTimeOffset? EstimatedAt,
        DateTimeOffset? DeliveredAt,
        IReadOnlyList<ShipmentEventDetail> Events);

    public sealed record ShipmentEventDetail(
        string Status,
        string? Location,
        DateTimeOffset OccurredAt,
        string PayloadJson);

    private sealed record ShipmentRow(
        Guid Id,
        Guid OrderId,
        string Carrier,
        string TrackingNumber,
        string Status,
        DateTime? EstimatedAt,
        DateTime? DeliveredAt,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    private sealed record ShipmentEventRow(
        string Status,
        string? Location,
        DateTime OccurredAt,
        string PayloadJson);

    private static DateTimeOffset? ToUtcOffset(DateTime? dt) =>
        dt is null ? null : new DateTimeOffset(DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc));

    private static DateTimeOffset? ToUtcOffset(DateTime dt) =>
        new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
}

