using Dapper;
using dCMS.Provisioning.Domain;
using Npgsql;

namespace dCMS.Infrastructure.Platform;

public sealed class SqlTenantUsageRepository(string connectionString) : ITenantUsageRepository
{
    private readonly string _connectionString = connectionString;

    public async Task IncrementAsync(
        string tenantId,
        Action<TenantUsageCounters> mutate,
        CancellationToken cancellationToken = default)
    {
        var counters = new TenantUsageCounters();
        mutate(counters);
        if (counters.OrdersDelta == 0 && counters.ApiCallsDelta == 0 &&
            counters.WebhookDeliveriesDelta == 0 && counters.ActiveProductsDelta == 0)
            return;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO "TenantUsageDaily" (
                "TenantId", "UsageDate", "OrdersCount", "ApiCallsCount", "WebhookDeliveriesCount", "ActiveProductsCount", "UpdatedAt")
            VALUES (@TenantId, CURRENT_DATE, @Orders, @ApiCalls, @Webhooks, @Products, NOW())
            ON CONFLICT ("TenantId", "UsageDate") DO UPDATE SET
                "OrdersCount" = "TenantUsageDaily"."OrdersCount" + @Orders,
                "ApiCallsCount" = "TenantUsageDaily"."ApiCallsCount" + @ApiCalls,
                "WebhookDeliveriesCount" = "TenantUsageDaily"."WebhookDeliveriesCount" + @Webhooks,
                "ActiveProductsCount" = GREATEST(0, "TenantUsageDaily"."ActiveProductsCount" + @Products),
                "UpdatedAt" = NOW()
            """, new
        {
            TenantId = tenantId,
            Orders = counters.OrdersDelta,
            ApiCalls = counters.ApiCallsDelta,
            Webhooks = counters.WebhookDeliveriesDelta,
            Products = counters.ActiveProductsDelta,
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<TenantUsageSnapshot> GetTodayAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var list = await GetRangeAsync(tenantId, today, today, cancellationToken).ConfigureAwait(false);
        return list.FirstOrDefault() ?? new TenantUsageSnapshot(tenantId, today, 0, 0, 0, 0, DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<TenantUsageSnapshot>> GetRangeAsync(
        string tenantId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition("""
            SELECT "TenantId", "UsageDate", "OrdersCount", "ApiCallsCount", "WebhookDeliveriesCount", "ActiveProductsCount", "UpdatedAt"
            FROM "TenantUsageDaily"
            WHERE "TenantId" = @TenantId AND "UsageDate" >= @From AND "UsageDate" <= @To
            ORDER BY "UsageDate"
            """, new { TenantId = tenantId, From = from, To = to }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new TenantUsageSnapshot(
            r.TenantId,
            DateOnly.FromDateTime(r.UsageDate),
            r.OrdersCount,
            r.ApiCallsCount,
            r.WebhookDeliveriesCount,
            r.ActiveProductsCount,
            new DateTimeOffset(r.UpdatedAt, TimeSpan.Zero))).ToList();
    }

    private sealed class Row
    {
        public string TenantId { get; init; } = "";
        public DateTime UsageDate { get; init; }
        public long OrdersCount { get; init; }
        public long ApiCallsCount { get; init; }
        public long WebhookDeliveriesCount { get; init; }
        public long ActiveProductsCount { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
