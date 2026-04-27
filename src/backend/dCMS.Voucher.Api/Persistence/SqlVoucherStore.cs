using Dapper;
using dCMS.Voucher.Api.Domain;
using Npgsql;

namespace dCMS.Voucher.Api.Persistence;

public sealed class SqlVoucherStore(string connectionString) : IVoucherStore
{
    public async Task<VoucherRow?> GetByCodeAsync(string tenantId, string code, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        return await conn.QuerySingleOrDefaultAsync<VoucherRow>(new CommandDefinition(
            """
            SELECT "Id" AS Id, "TenantId" AS TenantId, "Code" AS Code, "FaceValue" AS FaceValue,
                   "RemainingValue" AS RemainingValue, "Currency" AS Currency, "Status" AS Status,
                   "ExpiresAt" AS ExpiresAt, "CreatedAt" AS CreatedAt, "UpdatedAt" AS UpdatedAt
              FROM "Vouchers"
             WHERE "TenantId" = @TenantId AND "Code" = @Code
             LIMIT 1;
            """,
            new { TenantId = tenantId, Code = code }, cancellationToken: ct));
    }

    public async Task<ReserveResult> ReserveAsync(string tenantId, string code, Guid orderId, decimal amount, TimeSpan ttl, CancellationToken ct)
    {
        if (amount <= 0m)
            return new ReserveResult(false, "invalid_amount", "Amount must be positive.", null);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var voucher = await conn.QuerySingleOrDefaultAsync<VoucherRow>(new CommandDefinition(
            """
            SELECT "Id" AS Id, "TenantId" AS TenantId, "Code" AS Code, "FaceValue" AS FaceValue,
                   "RemainingValue" AS RemainingValue, "Currency" AS Currency, "Status" AS Status,
                   "ExpiresAt" AS ExpiresAt, "CreatedAt" AS CreatedAt, "UpdatedAt" AS UpdatedAt
              FROM "Vouchers"
             WHERE "TenantId" = @TenantId AND "Code" = @Code
             FOR UPDATE;
            """,
            new { TenantId = tenantId, Code = code }, tx, cancellationToken: ct));

        if (voucher is null)
            return new ReserveResult(false, "not_found", "Voucher not found.", null);

        if (!string.Equals(voucher.Status, VoucherStatus.Active, StringComparison.OrdinalIgnoreCase))
            return new ReserveResult(false, "voucher_inactive", $"Voucher is {voucher.Status}.", null);

        if (voucher.ExpiresAt is { } exp && exp <= DateTimeOffset.UtcNow)
            return new ReserveResult(false, "voucher_expired", "Voucher has expired.", null);

        // Idempotency: existing active hold for (tenant, order, voucher) returns the same row.
        var existing = await conn.QuerySingleOrDefaultAsync<VoucherHoldRow>(new CommandDefinition(
            """
            SELECT "Id" AS Id, "TenantId" AS TenantId, "VoucherId" AS VoucherId, "OrderId" AS OrderId,
                   "Amount" AS Amount, "Status" AS Status, "ExpiresAt" AS ExpiresAt,
                   "CreatedAt" AS CreatedAt, "UpdatedAt" AS UpdatedAt
              FROM "VoucherHolds"
             WHERE "TenantId" = @TenantId AND "OrderId" = @OrderId AND "VoucherId" = @VoucherId
               AND "Status" = 'Held'
             LIMIT 1;
            """,
            new { TenantId = tenantId, OrderId = orderId, VoucherId = voucher.Id }, tx, cancellationToken: ct));

        if (existing is not null)
        {
            await tx.CommitAsync(ct);
            return new ReserveResult(true, null, null, existing);
        }

        if (voucher.RemainingValue < amount)
            return new ReserveResult(false, "insufficient_balance",
                $"Voucher has {voucher.RemainingValue} remaining, requested {amount}.", null);

        var holdId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(ttl);

        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE "Vouchers"
               SET "RemainingValue" = "RemainingValue" - @Amount, "UpdatedAt" = @Now
             WHERE "Id" = @VoucherId;
            """,
            new { Amount = amount, Now = now, VoucherId = voucher.Id }, tx, cancellationToken: ct));

        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO "VoucherHolds"
              ("Id","TenantId","VoucherId","OrderId","Amount","Status","ExpiresAt","CreatedAt","UpdatedAt")
            VALUES (@Id,@TenantId,@VoucherId,@OrderId,@Amount,'Held',@ExpiresAt,@Now,@Now);
            """,
            new { Id = holdId, TenantId = tenantId, VoucherId = voucher.Id, OrderId = orderId,
                  Amount = amount, ExpiresAt = expiresAt, Now = now }, tx, cancellationToken: ct));

        await WriteLedgerAsync(conn, tx, tenantId, voucher.Id, holdId, orderId, "RESERVE", -amount, "Reserve hold", now, ct);

        await tx.CommitAsync(ct);

        var hold = new VoucherHoldRow(holdId, tenantId, voucher.Id, orderId, amount,
            VoucherHoldStatus.Held, expiresAt, now, now);
        return new ReserveResult(true, null, null, hold);
    }

    public async Task<CaptureResult> CaptureAsync(string tenantId, Guid holdId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var hold = await LockHoldAsync(conn, tx, tenantId, holdId, ct);
        if (hold is null)
            return new CaptureResult(false, "not_found", "Hold not found.", null);

        if (hold.Status == VoucherHoldStatus.Captured)
            return new CaptureResult(true, null, null, hold);

        if (hold.Status != VoucherHoldStatus.Held)
            return new CaptureResult(false, "invalid_state", $"Hold is {hold.Status}, cannot capture.", null);

        if (hold.ExpiresAt <= DateTimeOffset.UtcNow)
            return new CaptureResult(false, "hold_expired", "Hold has expired.", null);

        var now = DateTimeOffset.UtcNow;
        await conn.ExecuteAsync(new CommandDefinition(
            """UPDATE "VoucherHolds" SET "Status"='Captured', "UpdatedAt"=@Now WHERE "Id"=@Id;""",
            new { Id = holdId, Now = now }, tx, cancellationToken: ct));

        await WriteLedgerAsync(conn, tx, tenantId, hold.VoucherId, holdId, hold.OrderId, "CAPTURE", 0m, "Hold captured", now, ct);

        // If voucher's RemainingValue is now zero, mark it Consumed.
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE "Vouchers" SET "Status"='Consumed', "UpdatedAt"=@Now
             WHERE "Id"=@VoucherId AND "Status"='Active' AND "RemainingValue"=0;
            """,
            new { VoucherId = hold.VoucherId, Now = now }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
        return new CaptureResult(true, null, null, hold with { Status = VoucherHoldStatus.Captured, UpdatedAt = now });
    }

    public async Task<ReleaseResult> ReleaseAsync(string tenantId, Guid holdId, string reason, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var hold = await LockHoldAsync(conn, tx, tenantId, holdId, ct);
        if (hold is null)
            return new ReleaseResult(false, "not_found", "Hold not found.", null);

        if (hold.Status == VoucherHoldStatus.Released)
            return new ReleaseResult(true, null, null, hold);

        if (hold.Status != VoucherHoldStatus.Held)
            return new ReleaseResult(false, "invalid_state", $"Hold is {hold.Status}, cannot release.", null);

        var now = DateTimeOffset.UtcNow;
        await conn.ExecuteAsync(new CommandDefinition(
            """UPDATE "VoucherHolds" SET "Status"='Released', "UpdatedAt"=@Now WHERE "Id"=@Id;""",
            new { Id = holdId, Now = now }, tx, cancellationToken: ct));

        await conn.ExecuteAsync(new CommandDefinition(
            """UPDATE "Vouchers" SET "RemainingValue" = "RemainingValue" + @Amount, "UpdatedAt"=@Now WHERE "Id"=@VoucherId;""",
            new { Amount = hold.Amount, Now = now, VoucherId = hold.VoucherId }, tx, cancellationToken: ct));

        await WriteLedgerAsync(conn, tx, tenantId, hold.VoucherId, holdId, hold.OrderId, "RELEASE", hold.Amount, reason, now, ct);

        await tx.CommitAsync(ct);
        return new ReleaseResult(true, null, null, hold with { Status = VoucherHoldStatus.Released, UpdatedAt = now });
    }

    public async Task<RefundResult> RefundAsync(string tenantId, Guid holdId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var hold = await LockHoldAsync(conn, tx, tenantId, holdId, ct);
        if (hold is null)
            return new RefundResult(false, "not_found", "Hold not found.", null);

        if (hold.Status == VoucherHoldStatus.Refunded)
            return new RefundResult(true, null, null, hold);

        if (hold.Status != VoucherHoldStatus.Captured)
            return new RefundResult(false, "invalid_state", $"Hold is {hold.Status}, only Captured holds can be refunded.", null);

        var now = DateTimeOffset.UtcNow;
        await conn.ExecuteAsync(new CommandDefinition(
            """UPDATE "VoucherHolds" SET "Status"='Refunded', "UpdatedAt"=@Now WHERE "Id"=@Id;""",
            new { Id = holdId, Now = now }, tx, cancellationToken: ct));

        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE "Vouchers"
               SET "RemainingValue" = "RemainingValue" + @Amount,
                   "Status" = CASE WHEN "Status" = 'Consumed' THEN 'Active' ELSE "Status" END,
                   "UpdatedAt" = @Now
             WHERE "Id" = @VoucherId;
            """,
            new { Amount = hold.Amount, Now = now, VoucherId = hold.VoucherId }, tx, cancellationToken: ct));

        await WriteLedgerAsync(conn, tx, tenantId, hold.VoucherId, holdId, hold.OrderId, "REFUND", hold.Amount, "Refund captured hold", now, ct);

        await tx.CommitAsync(ct);
        return new RefundResult(true, null, null, hold with { Status = VoucherHoldStatus.Refunded, UpdatedAt = now });
    }

    public async Task<BalanceView?> GetBalanceAsync(string tenantId, string code, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        var row = await conn.QuerySingleOrDefaultAsync<(decimal FaceValue, decimal RemainingValue, string Currency, string Status, decimal HeldValue)>(new CommandDefinition(
            """
            SELECT v."FaceValue" AS FaceValue, v."RemainingValue" AS RemainingValue, v."Currency" AS Currency, v."Status" AS Status,
                   COALESCE((SELECT SUM(h."Amount") FROM "VoucherHolds" h
                              WHERE h."TenantId" = v."TenantId" AND h."VoucherId" = v."Id" AND h."Status" = 'Held'), 0) AS HeldValue
              FROM "Vouchers" v
             WHERE v."TenantId" = @TenantId AND v."Code" = @Code
             LIMIT 1;
            """,
            new { TenantId = tenantId, Code = code }, cancellationToken: ct));

        if (row.Equals(default((decimal, decimal, string, string, decimal))))
            return null;

        return new BalanceView(code, row.FaceValue, row.RemainingValue, row.HeldValue, row.Currency, row.Status);
    }

    private static async Task<VoucherHoldRow?> LockHoldAsync(NpgsqlConnection conn, System.Data.Common.DbTransaction tx,
        string tenantId, Guid holdId, CancellationToken ct)
    {
        return await conn.QuerySingleOrDefaultAsync<VoucherHoldRow>(new CommandDefinition(
            """
            SELECT "Id" AS Id, "TenantId" AS TenantId, "VoucherId" AS VoucherId, "OrderId" AS OrderId,
                   "Amount" AS Amount, "Status" AS Status, "ExpiresAt" AS ExpiresAt,
                   "CreatedAt" AS CreatedAt, "UpdatedAt" AS UpdatedAt
              FROM "VoucherHolds"
             WHERE "TenantId" = @TenantId AND "Id" = @Id
             FOR UPDATE;
            """,
            new { TenantId = tenantId, Id = holdId }, tx, cancellationToken: ct));
    }

    private static Task WriteLedgerAsync(NpgsqlConnection conn, System.Data.Common.DbTransaction tx,
        string tenantId, Guid voucherId, Guid holdId, Guid orderId, string action, decimal delta, string? reason, DateTimeOffset now,
        CancellationToken ct)
    {
        return conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO "VoucherLedger"
              ("TenantId","VoucherId","HoldId","OrderId","Action","Delta","Reason","OccurredAt")
            VALUES (@TenantId,@VoucherId,@HoldId,@OrderId,@Action,@Delta,@Reason,@Now);
            """,
            new { TenantId = tenantId, VoucherId = voucherId, HoldId = holdId, OrderId = orderId,
                  Action = action, Delta = delta, Reason = reason, Now = now }, tx, cancellationToken: ct));
    }
}
