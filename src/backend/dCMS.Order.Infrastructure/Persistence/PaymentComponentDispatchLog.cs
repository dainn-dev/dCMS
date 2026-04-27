using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Order.Infrastructure.Persistence;

/// <summary>
/// DAI-724: idempotency cache for downstream tender API dispatch
/// (Voucher.Api / Loyalty.Api / Payment.Api). Keyed by (OrderId, ComponentId, Action).
/// On retry, callers should consult <see cref="TryGetAsync"/> first and skip the network call
/// if the prior outcome is recorded.
/// </summary>
public interface IPaymentComponentDispatchLog
{
    Task<DispatchOutcome?> TryGetAsync(Guid orderId, Guid componentId, string action, CancellationToken ct);

    Task RecordSuccessAsync(Guid orderId, Guid componentId, string action, string? externalRef, CancellationToken ct);

    Task RecordFailureAsync(Guid orderId, Guid componentId, string action, string? errorCode, string? errorMessage, CancellationToken ct);
}

public sealed record DispatchOutcome(string Status, string? ExternalRef, string? ErrorCode, string? ErrorMessage)
{
    public bool IsSuccess => Status == "Success";
}

public sealed class SqlPaymentComponentDispatchLog : IPaymentComponentDispatchLog
{
    private readonly string _connectionString;

    public SqlPaymentComponentDispatchLog(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
    }

    public async Task<DispatchOutcome?> TryGetAsync(Guid orderId, Guid componentId, string action, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var row = await conn.QuerySingleOrDefaultAsync<(string Status, string? ExternalRef, string? ErrorCode, string? ErrorMessage)?>(
            new CommandDefinition(
                """
                SELECT "Status","ExternalRef","ErrorCode","ErrorMessage"
                FROM "PaymentComponentDispatchLog"
                WHERE "OrderId"=@OrderId AND "ComponentId"=@ComponentId AND "Action"=@Action
                LIMIT 1;
                """,
                new { OrderId = orderId, ComponentId = componentId, Action = action },
                cancellationToken: ct));
        return row is null
            ? null
            : new DispatchOutcome(row.Value.Status, row.Value.ExternalRef, row.Value.ErrorCode, row.Value.ErrorMessage);
    }

    public Task RecordSuccessAsync(Guid orderId, Guid componentId, string action, string? externalRef, CancellationToken ct)
        => UpsertAsync(orderId, componentId, action, "Success", externalRef, null, null, ct);

    public Task RecordFailureAsync(Guid orderId, Guid componentId, string action, string? errorCode, string? errorMessage, CancellationToken ct)
        => UpsertAsync(orderId, componentId, action, "Failed", null, errorCode, errorMessage, ct);

    private async Task UpsertAsync(Guid orderId, Guid componentId, string action, string status,
        string? externalRef, string? errorCode, string? errorMessage, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO "PaymentComponentDispatchLog"
              ("OrderId","ComponentId","Action","ExternalRef","Status","ErrorCode","ErrorMessage")
            VALUES
              (@OrderId,@ComponentId,@Action,@ExternalRef,@Status,@ErrorCode,@ErrorMessage)
            ON CONFLICT ("OrderId","ComponentId","Action") DO UPDATE SET
              "ExternalRef"=COALESCE(EXCLUDED."ExternalRef","PaymentComponentDispatchLog"."ExternalRef"),
              "Status"=EXCLUDED."Status",
              "ErrorCode"=EXCLUDED."ErrorCode",
              "ErrorMessage"=EXCLUDED."ErrorMessage",
              "OccurredAt"=NOW();
            """,
            new
            {
                OrderId = orderId,
                ComponentId = componentId,
                Action = action,
                ExternalRef = externalRef,
                Status = status,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
            },
            cancellationToken: ct));
    }
}
