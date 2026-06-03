using Dapper;
using dCMS.Core.Persistence;
using Npgsql;

namespace dCMS.Infrastructure.Catalog;

/// <summary>Per-user purchased quantity for quantity-limit enforcement (Order DB).</summary>
public sealed class SqlCustomerOrderQuantityQuery(string orderConnectionString) : ICustomerOrderQuantityQuery
{
    private readonly string _cs = orderConnectionString ?? throw new ArgumentNullException(nameof(orderConnectionString));

    private static readonly int[] ExcludedStatuses = [5, 6, 7, 8, 9, 10];

    public async Task<int> GetPurchasedQuantityAsync(
        string tenantId,
        string storeId,
        string customerId,
        string productId,
        DateOnly fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            SELECT COALESCE(SUM(oi."Quantity"), 0)
            FROM "OrderItems" oi
            INNER JOIN "Orders" o ON o."Id" = oi."OrderId"
            WHERE o."TenantId" = @TenantId
              AND o."StoreId" = @StoreId
              AND o."CustomerId" = @CustomerId
              AND oi."ProductId" = @ProductId
              AND NOT (o."Status" = ANY(@ExcludedStatuses))
              AND o."CreatedAt" >= @FromUtc
              AND (@ToUtc IS NULL OR o."CreatedAt" < @ToUtc)
            """;

        var fromUtc = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime? toUtc = toDate?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql,
            new
            {
                TenantId = tenantId,
                StoreId = storeId,
                CustomerId = customerId,
                ProductId = productId,
                ExcludedStatuses = ExcludedStatuses,
                FromUtc = fromUtc,
                ToUtc = toUtc
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
