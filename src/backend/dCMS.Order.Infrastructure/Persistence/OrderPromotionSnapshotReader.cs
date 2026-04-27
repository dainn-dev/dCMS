using Dapper;
using Npgsql;

namespace dCMS.Order.Infrastructure.Persistence;

/// <summary>
/// DAI-693 — Reads applied-promotion snapshots persisted on order create (table <c>OrderPromotions</c>, migration 018).
/// Used by saga side-effect consumers to determine which redemption to confirm/release on Promotions.Api.
/// </summary>
public sealed class OrderPromotionSnapshotReader
{
    public sealed record Row(
        string Id,
        string TenantId,
        string OrderId,
        string CampaignId,
        string EditorKind,
        string Name,
        decimal Amount,
        string? PromoCode);

    private readonly string _connectionString;

    public OrderPromotionSnapshotReader(string connectionString) =>
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<IReadOnlyList<Row>> GetByOrderAsync(string tenantId, string orderId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "TenantId", "OrderId", "CampaignId", "EditorKind",
                   "Name", "Amount", "PromoCode"
            FROM "OrderPromotions"
            WHERE "TenantId" = @TenantId AND "OrderId" = @OrderId
            ORDER BY "AppliedAt" ASC
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, OrderId = orderId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<string?> GetPromoCodeIdAsync(string tenantId, string orderId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "PromoCodeId"
            FROM "Orders"
            WHERE "TenantId" = @TenantId AND "Id" = @OrderId::uuid
            LIMIT 1
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, OrderId = orderId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
