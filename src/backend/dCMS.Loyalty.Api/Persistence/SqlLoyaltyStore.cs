using Dapper;
using dCMS.Loyalty.Api.Domain;
using Npgsql;

namespace dCMS.Loyalty.Api.Persistence;

public sealed class SqlLoyaltyStore(string connectionString) : ILoyaltyStore
{
    public async Task<LoyaltyBalanceView> GetBalanceAsync(string tenantId, string customerId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        var (balance, held) = await ReadBalanceAndHoldsAsync(conn, null, tenantId, customerId, ct);
        return new LoyaltyBalanceView(customerId, balance, held, balance - held);
    }

    public async Task<LoyaltyReserveResult> ReserveAsync(string tenantId, string customerId, Guid orderId,
        decimal amount, TimeSpan ttl, CancellationToken ct)
    {
        if (amount <= 0m)
            return new LoyaltyReserveResult(false, "invalid_amount", "Amount must be positive.", null);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Idempotency: an active hold on (tenant, order, customer) returns the same row.
        var existing = await conn.QuerySingleOrDefaultAsync<LoyaltyHoldRow>(new CommandDefinition(
            """
            SELECT "Id" AS Id, "TenantId" AS TenantId, "CustomerId" AS CustomerId, "OrderId" AS OrderId,
                   "Amount" AS Amount, "Status" AS Status, "ExpiresAt" AS ExpiresAt,
                   "CreatedAt" AS CreatedAt, "UpdatedAt" AS UpdatedAt
              FROM "LoyaltyHolds"
             WHERE "TenantId" = @TenantId AND "OrderId" = @OrderId AND "CustomerId" = @CustomerId
               AND "Status" = 'Held'
             FOR UPDATE
             LIMIT 1;
            """,
            new { TenantId = tenantId, OrderId = orderId, CustomerId = customerId }, tx, cancellationToken: ct));

        if (existing is not null)
        {
            await tx.CommitAsync(ct);
            return new LoyaltyReserveResult(true, null, null, existing);
        }

        var (balance, held) = await ReadBalanceAndHoldsAsync(conn, tx, tenantId, customerId, ct);
        var available = balance - held;
        if (available < amount)
            return new LoyaltyReserveResult(false, "insufficient_balance",
                $"Available {available}, requested {amount}.", null);

        var holdId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(ttl);

        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO "LoyaltyHolds"
              ("Id","TenantId","CustomerId","OrderId","Amount","Status","ExpiresAt","CreatedAt","UpdatedAt")
            VALUES (@Id,@TenantId,@CustomerId,@OrderId,@Amount,'Held',@ExpiresAt,@Now,@Now);
            """,
            new { Id = holdId, TenantId = tenantId, CustomerId = customerId, OrderId = orderId,
                  Amount = amount, ExpiresAt = expiresAt, Now = now }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);

        var hold = new LoyaltyHoldRow(holdId, tenantId, customerId, orderId, amount,
            LoyaltyHoldStatus.Held, expiresAt, now, now);
        return new LoyaltyReserveResult(true, null, null, hold);
    }

    public async Task<LoyaltyCaptureResult> CaptureAsync(string tenantId, Guid holdId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var hold = await LockHoldAsync(conn, tx, tenantId, holdId, ct);
        if (hold is null)
            return new LoyaltyCaptureResult(false, "not_found", "Hold not found.", null);

        if (hold.Status == LoyaltyHoldStatus.Captured)
            return new LoyaltyCaptureResult(true, null, null, hold);

        if (hold.Status != LoyaltyHoldStatus.Held)
            return new LoyaltyCaptureResult(false, "invalid_state", $"Hold is {hold.Status}, cannot capture.", null);

        if (hold.ExpiresAt <= DateTimeOffset.UtcNow)
            return new LoyaltyCaptureResult(false, "hold_expired", "Hold has expired.", null);

        var now = DateTimeOffset.UtcNow;
        await conn.ExecuteAsync(new CommandDefinition(
            """UPDATE "LoyaltyHolds" SET "Status"='Captured', "UpdatedAt"=@Now WHERE "Id"=@Id;""",
            new { Id = holdId, Now = now }, tx, cancellationToken: ct));

        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO "LoyaltyLedger" ("TenantId","CustomerId","Delta","Reason","OrderId","HoldId","Notes","OccurredAt")
            VALUES (@TenantId,@CustomerId,@Delta,'SPEND',@OrderId,@HoldId,'Hold captured',@Now);
            """,
            new { TenantId = tenantId, CustomerId = hold.CustomerId, Delta = -hold.Amount,
                  OrderId = hold.OrderId, HoldId = holdId, Now = now }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
        return new LoyaltyCaptureResult(true, null, null, hold with { Status = LoyaltyHoldStatus.Captured, UpdatedAt = now });
    }

    public async Task<LoyaltyReleaseResult> ReleaseAsync(string tenantId, Guid holdId, string reason, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var hold = await LockHoldAsync(conn, tx, tenantId, holdId, ct);
        if (hold is null)
            return new LoyaltyReleaseResult(false, "not_found", "Hold not found.", null);

        if (hold.Status == LoyaltyHoldStatus.Released)
            return new LoyaltyReleaseResult(true, null, null, hold);

        if (hold.Status != LoyaltyHoldStatus.Held)
            return new LoyaltyReleaseResult(false, "invalid_state", $"Hold is {hold.Status}, cannot release.", null);

        var now = DateTimeOffset.UtcNow;
        await conn.ExecuteAsync(new CommandDefinition(
            """UPDATE "LoyaltyHolds" SET "Status"='Released', "UpdatedAt"=@Now WHERE "Id"=@Id;""",
            new { Id = holdId, Now = now }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
        return new LoyaltyReleaseResult(true, null, null, hold with { Status = LoyaltyHoldStatus.Released, UpdatedAt = now });
    }

    public async Task<LoyaltyRefundResult> RefundAsync(string tenantId, Guid holdId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var hold = await LockHoldAsync(conn, tx, tenantId, holdId, ct);
        if (hold is null)
            return new LoyaltyRefundResult(false, "not_found", "Hold not found.", null);

        if (hold.Status == LoyaltyHoldStatus.Refunded)
            return new LoyaltyRefundResult(true, null, null, hold);

        if (hold.Status != LoyaltyHoldStatus.Captured)
            return new LoyaltyRefundResult(false, "invalid_state", $"Hold is {hold.Status}, only Captured holds can be refunded.", null);

        var now = DateTimeOffset.UtcNow;
        await conn.ExecuteAsync(new CommandDefinition(
            """UPDATE "LoyaltyHolds" SET "Status"='Refunded', "UpdatedAt"=@Now WHERE "Id"=@Id;""",
            new { Id = holdId, Now = now }, tx, cancellationToken: ct));

        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO "LoyaltyLedger" ("TenantId","CustomerId","Delta","Reason","OrderId","HoldId","Notes","OccurredAt")
            VALUES (@TenantId,@CustomerId,@Delta,'REFUND',@OrderId,@HoldId,'Refund of captured hold',@Now);
            """,
            new { TenantId = tenantId, CustomerId = hold.CustomerId, Delta = hold.Amount,
                  OrderId = hold.OrderId, HoldId = holdId, Now = now }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
        return new LoyaltyRefundResult(true, null, null, hold with { Status = LoyaltyHoldStatus.Refunded, UpdatedAt = now });
    }

    public async Task<IReadOnlyList<ExpiredLoyaltyHoldRow>> ListExpiredHoldsAsync(DateTimeOffset asOf, int limit, CancellationToken ct)
    {
        if (limit <= 0) return Array.Empty<ExpiredLoyaltyHoldRow>();

        await using var conn = new NpgsqlConnection(connectionString);
        var rows = await conn.QueryAsync<ExpiredLoyaltyHoldRow>(new CommandDefinition(
            """
            SELECT "Id" AS HoldId, "TenantId" AS TenantId, "CustomerId" AS CustomerId,
                   "OrderId" AS OrderId, "Amount" AS Amount, "ExpiresAt" AS ExpiresAt
              FROM "LoyaltyHolds"
             WHERE "Status" = 'Held' AND "ExpiresAt" <= @AsOf
             ORDER BY "ExpiresAt" ASC
             LIMIT @Limit;
            """,
            new { AsOf = asOf, Limit = limit }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<long> RecordLedgerAsync(string tenantId, string customerId, decimal delta, string reason,
        Guid? orderId, string? notes, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO "LoyaltyLedger" ("TenantId","CustomerId","Delta","Reason","OrderId","Notes","OccurredAt")
            VALUES (@TenantId,@CustomerId,@Delta,@Reason,@OrderId,@Notes,@Now)
            RETURNING "Id";
            """,
            new { TenantId = tenantId, CustomerId = customerId, Delta = delta, Reason = reason,
                  OrderId = orderId, Notes = notes, Now = DateTimeOffset.UtcNow }, cancellationToken: ct));
    }

    private static async Task<(decimal Balance, decimal Held)> ReadBalanceAndHoldsAsync(
        NpgsqlConnection conn, System.Data.Common.DbTransaction? tx, string tenantId, string customerId, CancellationToken ct)
    {
        var row = await conn.QuerySingleAsync<(decimal Balance, decimal Held)>(new CommandDefinition(
            """
            SELECT
              COALESCE((SELECT SUM("Delta") FROM "LoyaltyLedger"
                         WHERE "TenantId" = @TenantId AND "CustomerId" = @CustomerId), 0) AS Balance,
              COALESCE((SELECT SUM("Amount") FROM "LoyaltyHolds"
                         WHERE "TenantId" = @TenantId AND "CustomerId" = @CustomerId AND "Status" = 'Held'), 0) AS Held;
            """,
            new { TenantId = tenantId, CustomerId = customerId }, tx, cancellationToken: ct));
        return (row.Balance, row.Held);
    }

    private static async Task<LoyaltyHoldRow?> LockHoldAsync(NpgsqlConnection conn, System.Data.Common.DbTransaction tx,
        string tenantId, Guid holdId, CancellationToken ct)
    {
        return await conn.QuerySingleOrDefaultAsync<LoyaltyHoldRow>(new CommandDefinition(
            """
            SELECT "Id" AS Id, "TenantId" AS TenantId, "CustomerId" AS CustomerId, "OrderId" AS OrderId,
                   "Amount" AS Amount, "Status" AS Status, "ExpiresAt" AS ExpiresAt,
                   "CreatedAt" AS CreatedAt, "UpdatedAt" AS UpdatedAt
              FROM "LoyaltyHolds"
             WHERE "TenantId" = @TenantId AND "Id" = @Id
             FOR UPDATE;
            """,
            new { TenantId = tenantId, Id = holdId }, tx, cancellationToken: ct));
    }
}
