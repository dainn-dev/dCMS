using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Order.Infrastructure.Persistence;

/// <summary>Read-side lookup of multi-tender payment components for an order (DAI-722).</summary>
public sealed class OrderPaymentQueryStore
{
    private readonly string _connectionString;

    public OrderPaymentQueryStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
    }

    public async Task<OrderPaymentView?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var head = await conn.QuerySingleOrDefaultAsync<OrderPaymentHead>(new CommandDefinition(
            """
            SELECT
              p."Id" AS Id,
              p."OrderId" AS OrderId,
              p."Total" AS Total,
              p."Status" AS Status
            FROM "OrderPayments" p
            WHERE p."OrderId"=@OrderId
            LIMIT 1;
            """,
            new { OrderId = orderId },
            cancellationToken: ct));

        if (head is null)
            return null;

        var comps = (await conn.QueryAsync<PaymentComponentView>(new CommandDefinition(
            """
            SELECT
              c."Id" AS Id,
              c."Type" AS Type,
              c."Amount" AS Amount,
              c."ExternalRef" AS ExternalRef,
              c."State" AS State,
              c."LastError" AS LastError,
              c."Ordering" AS Ordering,
              c."CreatedAt" AS CreatedAt,
              c."UpdatedAt" AS UpdatedAt
            FROM "PaymentComponents" c
            WHERE c."OrderPaymentId"=@OrderPaymentId
            ORDER BY c."Ordering" ASC, c."CreatedAt" ASC;
            """,
            new { OrderPaymentId = head.Id },
            cancellationToken: ct))).ToList();

        return new OrderPaymentView(head.OrderId, head.Total, head.Status, comps);
    }

    private sealed record OrderPaymentHead(Guid Id, Guid OrderId, decimal Total, string Status);
}

public sealed record OrderPaymentView(Guid OrderId, decimal Total, string Status, IReadOnlyList<PaymentComponentView> Components);

public sealed record PaymentComponentView(
    Guid Id,
    string Type,
    decimal Amount,
    string? ExternalRef,
    string State,
    string? LastError,
    int Ordering,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

