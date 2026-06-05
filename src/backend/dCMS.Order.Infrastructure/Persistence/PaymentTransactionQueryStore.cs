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

    /// <summary>
    /// Ecommerce Payments BRD: every payment transaction (all statuses, BR01/BR07) for the given orders,
    /// filtered by gateway timestamp + the optional BRD search criteria. Parameterised; no card data is
    /// exposed (BR03) — the table has no masked-card column.
    /// </summary>
    public async Task<IReadOnlyList<EcommercePaymentRow>> GetTransactionsForOrdersAsync(
        IReadOnlyList<Guid> orderIds,
        DateTime from,
        DateTime to,
        EcommercePaymentFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (orderIds.Count == 0)
            return Array.Empty<EcommercePaymentRow>();

        const string sql = """
            SELECT
                "OrderId"::uuid    AS OrderId,
                "PaymentIntentId"  AS ReferenceNumber,
                "Provider"         AS GatewayName,
                "PaymentMethod"    AS PaymentMethod,
                "Status"           AS PaymentStatus,
                NULL::text         AS GatewayMessage,
                "CreatedAt"        AS PaymentDatetime,
                NULL::text         AS CardNo,
                "Amount"           AS Amount,
                "Currency"         AS Currency
            FROM "PaymentTransactions"
            WHERE "OrderId" = ANY(@OrderIds)
              AND "CreatedAt" >= @From AND "CreatedAt" < @To
              AND (@OrderNumber = ''      OR "OrderId"::text     ILIKE '%' || @OrderNumber || '%')
              AND (@ReferenceNumber = ''  OR "PaymentIntentId"   ILIKE '%' || @ReferenceNumber || '%')
              AND (@GatewayName = ''      OR "Provider"          ILIKE '%' || @GatewayName || '%')
              AND (@PaymentMethod = ''    OR "PaymentMethod"     ILIKE '%' || @PaymentMethod || '%')
              AND (@PaymentStatus = ''    OR LOWER(TRIM("Status")) = LOWER(TRIM(@PaymentStatus)))
              AND (@AmountMin IS NULL     OR "Amount" >= @AmountMin)
              AND (@AmountMax IS NULL     OR "Amount" <= @AmountMax)
            ORDER BY "CreatedAt" DESC
            LIMIT 1000
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<EcommercePaymentRow>(
            new CommandDefinition(sql, new
            {
                OrderIds = orderIds.ToArray(),
                From = from,
                To = to,
                OrderNumber = (filter.OrderNumber ?? string.Empty).Trim(),
                ReferenceNumber = (filter.ReferenceNumber ?? string.Empty).Trim(),
                GatewayName = (filter.GatewayName ?? string.Empty).Trim(),
                PaymentMethod = (filter.PaymentMethod ?? string.Empty).Trim(),
                PaymentStatus = (filter.PaymentStatus ?? string.Empty).Trim(),
                filter.AmountMin,
                filter.AmountMax,
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
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
