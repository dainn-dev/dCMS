using dCMS.Order.Infrastructure.Migrations;
using dCMS.Order.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Order.Tests.Integration;

public sealed class OrderFailureRepositoryTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private string? _cs;
    private IOrderFailureRepository? _repo;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await _postgres.StartAsync();
        _cs = _postgres.GetConnectionString();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Order"] = _cs })
            .Build();
        OrderDatabaseUpgrader.Run(configuration, NullLogger.Instance);
        _repo = new PgOrderFailureRepository(configuration);
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Upsert_then_get_roundtrips()
    {
        var id = Guid.NewGuid();
        var row = new OrderFailureRow
        {
            OrderId = id,
            TenantId = "TEN",
            StoreId = "STORE",
            FailureStatus = "PaymentFailed",
            FailureReason = "boom",
            FailureErrorCode = "E_PAY",
            SourceEventId = "evt-1",
            FailedAt = DateTimeOffset.UtcNow,
            RetryCount = 0,
            LastRetryAt = null,
            ResolvedAt = null,
            ResolvedBy = null,
            LogJson = "[]",
        };

        await _repo!.UpsertFailureAsync(row, """{"msg":"first"}""", CancellationToken.None);
        var got = await _repo.GetAsync("TEN", "STORE", id, CancellationToken.None);
        Assert.NotNull(got);
        Assert.Equal("PaymentFailed", got!.FailureStatus);
        Assert.Contains("first", got.LogJson);
    }

    [Fact]
    public async Task List_uses_index_shape()
    {
        // seed 3 rows
        for (var i = 0; i < 3; i++)
        {
            var id = Guid.NewGuid();
            var row = new OrderFailureRow
            {
                OrderId = id,
                TenantId = "TEN",
                StoreId = "STORE",
                FailureStatus = "SystemError",
                FailureReason = "boom",
                FailureErrorCode = null,
                SourceEventId = $"evt-{i}",
                FailedAt = DateTimeOffset.UtcNow.AddMinutes(-i),
                RetryCount = 0,
                LastRetryAt = null,
                ResolvedAt = null,
                ResolvedBy = null,
                LogJson = "[]",
            };
            await _repo!.UpsertFailureAsync(row, """{"msg":"x"}""", CancellationToken.None);
        }

        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            EXPLAIN (FORMAT TEXT)
            SELECT "OrderId" FROM "OrderFailures"
            WHERE "TenantId" = 'TEN' AND "StoreId" = 'STORE' AND "ResolvedAt" IS NULL
            ORDER BY "FailedAt" DESC
            LIMIT 10
            """,
            conn);
        var plan = "";
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                plan += reader.GetString(0) + "\n";
        }

        Assert.Contains("OrderFailures", plan);
        // best-effort: index name should appear in plan in most cases
        Assert.Contains("IX_OrderFailures_Tenant_Store_Status_FailedAt", plan);
    }
}

