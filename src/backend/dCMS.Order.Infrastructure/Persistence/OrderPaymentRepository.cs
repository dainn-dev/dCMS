using Dapper;
using dCMS.Order.Core.Domain.Payments;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Order.Infrastructure.Persistence;

/// <summary>DAI-722: Write side for the multi-tender payment aggregate.</summary>
public class OrderPaymentRepository
{
    private readonly string _connectionString;

    public OrderPaymentRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
    }

    public virtual async Task UpsertAsync(OrderPayment payment, CancellationToken ct = default)
    {
        payment.RecomputeStatus();
        payment.EnsureSumInvariant();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO "OrderPayments" ("Id","OrderId","Total","Status")
            VALUES (@Id, @OrderId, @Total, @Status)
            ON CONFLICT ("OrderId") DO UPDATE SET
              "Total"=EXCLUDED."Total",
              "Status"=EXCLUDED."Status";
            """,
            new { payment.Id, payment.OrderId, payment.Total, payment.Status },
            transaction: tx, cancellationToken: ct));

        // Resolve the surrogate Id for this OrderId (may differ from payment.Id if pre-existing).
        var paymentId = await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
            """SELECT "Id" FROM "OrderPayments" WHERE "OrderId"=@OrderId;""",
            new { payment.OrderId }, transaction: tx, cancellationToken: ct));

        foreach (var c in payment.Components)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO "PaymentComponents"
                  ("Id","OrderPaymentId","Type","Amount","ExternalRef","State","LastError","Ordering","CreatedAt","UpdatedAt")
                VALUES
                  (@Id, @OrderPaymentId, @Type, @Amount, @ExternalRef, @State, @LastError, @Ordering, @CreatedAt, @UpdatedAt)
                ON CONFLICT ("Id") DO UPDATE SET
                  "Amount"=EXCLUDED."Amount",
                  "ExternalRef"=EXCLUDED."ExternalRef",
                  "State"=EXCLUDED."State",
                  "LastError"=EXCLUDED."LastError",
                  "Ordering"=EXCLUDED."Ordering",
                  "UpdatedAt"=EXCLUDED."UpdatedAt";
                """,
                new
                {
                    c.Id,
                    OrderPaymentId = paymentId,
                    Type = c.Type.ToString(),
                    c.Amount,
                    c.ExternalRef,
                    State = c.State.ToString(),
                    c.LastError,
                    c.Ordering,
                    c.CreatedAt,
                    c.UpdatedAt,
                },
                transaction: tx, cancellationToken: ct));
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public virtual async Task<OrderPayment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var head = await conn.QuerySingleOrDefaultAsync<(Guid Id, Guid OrderId, decimal Total, string Status)?>(
            new CommandDefinition(
                """SELECT "Id","OrderId","Total","Status" FROM "OrderPayments" WHERE "OrderId"=@OrderId LIMIT 1;""",
                new { OrderId = orderId }, cancellationToken: ct));
        if (head is null) return null;

        var rows = await conn.QueryAsync<(Guid Id, string Type, decimal Amount, string? ExternalRef, string State, string? LastError, int Ordering, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt)>(
            new CommandDefinition(
                """
                SELECT "Id","Type","Amount","ExternalRef","State","LastError","Ordering","CreatedAt","UpdatedAt"
                FROM "PaymentComponents" WHERE "OrderPaymentId"=@PaymentId
                ORDER BY "Ordering" ASC, "CreatedAt" ASC;
                """,
                new { PaymentId = head.Value.Id }, cancellationToken: ct));

        var components = rows.Select(r => new PaymentComponent(
            r.Id,
            Enum.Parse<PaymentComponentType>(r.Type, ignoreCase: true),
            r.Amount,
            r.Ordering,
            Enum.Parse<PaymentComponentState>(r.State, ignoreCase: true),
            r.ExternalRef,
            r.LastError,
            r.CreatedAt,
            r.UpdatedAt));

        return new OrderPayment(head.Value.Id, head.Value.OrderId, head.Value.Total, head.Value.Status, components);
    }
}
