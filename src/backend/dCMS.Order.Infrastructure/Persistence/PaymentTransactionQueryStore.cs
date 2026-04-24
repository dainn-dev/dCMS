using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Order.Infrastructure.Persistence;

/// <summary>Read-side batch lookup of latest payment rows per order (Payment DB; DAI-651 refund cases).</summary>
public sealed class PaymentTransactionQueryStore
{
    private readonly string _connectionString;

    public PaymentTransactionQueryStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Payment")
            ?? throw new InvalidOperationException("ConnectionStrings:Payment is required.");
    }

    /// <summary>Latest transaction per order id (by <c>CreatedAt</c> desc). Empty input → empty map.</summary>
    public async Task<IReadOnlyDictionary<Guid, RefundCasePaymentRow>> GetLatestByOrderIdsAsync(
        IReadOnlyList<Guid> orderIds,
        CancellationToken cancellationToken = default)
    {
        if (orderIds.Count == 0)
            return new Dictionary<Guid, RefundCasePaymentRow>();

        // DAI-653: latest row per order among qualifying payment statuses (not "latest row overall").
        const string sql = """
            SELECT DISTINCT ON ("OrderId")
                "OrderId"::uuid AS OrderId,
                "Amount" AS Amount,
                "Currency" AS Currency,
                "PaymentMethod" AS PaymentMethod,
                "PaymentIntentId" AS PaymentIntentId,
                "Status" AS Status,
                "Provider" AS Provider
            FROM "PaymentTransactions"
            WHERE "OrderId" = ANY(@OrderIds)
              AND LOWER(TRIM("Status")) = ANY(@Qualifying)
            ORDER BY "OrderId", "CreatedAt" DESC
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = (await connection
                .QueryAsync<RefundCasePaymentRow>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            OrderIds = orderIds.ToArray(),
                            Qualifying = new[] { "succeeded", "refunded", "initiated", "completed" },
                        },
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false))
            .ToList();

        return rows.ToDictionary(r => r.OrderId, r => r);
    }
}

public sealed record RefundCasePaymentRow(
    Guid OrderId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string PaymentIntentId,
    string Status,
    string Provider);
