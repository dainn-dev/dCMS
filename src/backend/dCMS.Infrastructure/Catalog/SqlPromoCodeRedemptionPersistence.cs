using Dapper;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using Npgsql;

namespace dCMS.Infrastructure.Catalog;

/// <summary>
/// DAI-692: Dapper + PostgreSQL implementation of <see cref="IPromoCodeRedemptionPersistence"/>.
/// </summary>
public sealed class SqlPromoCodeRedemptionPersistence(string connectionString) : IPromoCodeRedemptionPersistence
{
    private readonly string _cs = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<int> GetUsageCountByCustomerAsync(
        string tenantId, string promoCodeId, string customerId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            SELECT COUNT(*)::INT FROM "PromoCodeRedemptions"
            WHERE "TenantId" = @TenantId
              AND "PromoCodeId" = @PromoCodeId
              AND "CustomerId" = @CustomerId
              AND "Status" = 'confirmed'
            """;
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { TenantId = tenantId, PromoCodeId = promoCodeId, CustomerId = customerId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<int> GetTotalUsageAsync(
        string tenantId, string promoCodeId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            SELECT COUNT(*)::INT FROM "PromoCodeRedemptions"
            WHERE "TenantId" = @TenantId
              AND "PromoCodeId" = @PromoCodeId
              AND "Status" = 'confirmed'
            """;
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { TenantId = tenantId, PromoCodeId = promoCodeId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> HasGroupConflictAsync(
        string tenantId, string customerId, string groupId, string excludePromoCodeId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            SELECT EXISTS(
              SELECT 1 FROM "PromoCodeRedemptions"
              WHERE "TenantId" = @TenantId
                AND "CustomerId" = @CustomerId
                AND "GroupId" = @GroupId
                AND "PromoCodeId" <> @ExcludeId
                AND "Status" = 'confirmed'
            )
            """;
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, CustomerId = customerId, GroupId = groupId, ExcludeId = excludePromoCodeId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> InsertConfirmedAsync(
        PromoCodeRedemptionRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            INSERT INTO "PromoCodeRedemptions"
                ("Id","TenantId","PromoCodeId","OrderId","CustomerId","GroupId",
                 "Amount","Currency","Status","RedeemedAt")
            VALUES (@Id,@TenantId,@PromoCodeId,@OrderId,@CustomerId,@GroupId,
                    @Amount,@Currency,'confirmed',@RedeemedAt)
            ON CONFLICT ("TenantId","PromoCodeId","OrderId") DO NOTHING
            """;
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            row.Id,
            row.TenantId,
            row.PromoCodeId,
            row.OrderId,
            row.CustomerId,
            row.GroupId,
            row.Amount,
            row.Currency,
            RedeemedAt = row.RedeemedAt.UtcDateTime,
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows == 1;
    }

    public async Task<int> MarkReleasedAsync(
        string tenantId, string orderId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            UPDATE "PromoCodeRedemptions"
               SET "Status" = 'released', "ReleasedAt" = NOW()
             WHERE "TenantId" = @TenantId
               AND "OrderId"  = @OrderId
               AND "Status"   = 'confirmed'
            """;
        return await conn.ExecuteAsync(new CommandDefinition(
            sql, new { TenantId = tenantId, OrderId = orderId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<PromoCodeRedemptionRow?> GetByOrderAsync(
        string tenantId, string orderId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            SELECT "Id","TenantId","PromoCodeId","OrderId","CustomerId","GroupId",
                   "Amount","Currency","Status","RedeemedAt","ReleasedAt"
              FROM "PromoCodeRedemptions"
             WHERE "TenantId" = @TenantId
               AND "OrderId"  = @OrderId
            """;
        var row = await conn.QuerySingleOrDefaultAsync<RedemptionDapperRow>(new CommandDefinition(
            sql, new { TenantId = tenantId, OrderId = orderId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row?.ToModel();
    }

    private sealed class RedemptionDapperRow
    {
        public string    Id          { get; init; } = null!;
        public string    TenantId    { get; init; } = null!;
        public string    PromoCodeId { get; init; } = null!;
        public string    OrderId     { get; init; } = null!;
        public string?   CustomerId  { get; init; }
        public string?   GroupId     { get; init; }
        public decimal   Amount      { get; init; }
        public string    Currency    { get; init; } = "";
        public string    Status      { get; init; } = "confirmed";
        public DateTime  RedeemedAt  { get; init; }
        public DateTime? ReleasedAt  { get; init; }

        public PromoCodeRedemptionRow ToModel() => new(
            Id, TenantId, PromoCodeId, OrderId, CustomerId, GroupId, Amount, Currency, Status,
            new DateTimeOffset(RedeemedAt, TimeSpan.Zero),
            ReleasedAt.HasValue ? new DateTimeOffset(ReleasedAt.Value, TimeSpan.Zero) : null);
    }
}
