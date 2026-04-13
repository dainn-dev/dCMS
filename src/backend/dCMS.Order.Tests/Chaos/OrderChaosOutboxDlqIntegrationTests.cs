using System.Threading;
using dCMS.Infrastructure.Outbox;
using dCMS.Order.Infrastructure.Migrations;
using dCMS.Order.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace dCMS.Order.Tests.Chaos;

/// <summary>DAI-368 — Order outbox → SQL dead-letter + admin retry (Rabbit queue dlq.inventory.* not in scope).</summary>
public sealed class OrderChaosOutboxDlqIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Outbox_publish_fails_five_times_dead_letters_then_admin_retry_inserts_fresh_outbox_row()
    {
        var cs = _postgres!.GetConnectionString();
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Order"] = cs }).Build();
        OrderDatabaseUpgrader.Run(config, NullLogger.Instance);

        const string payload = """{"orderId":"00000000-0000-0000-0000-000000000001","tenantId":"t","storeId":"s","customerId":"c","totalAmount":1,"currency":"USD","lines":[],"occurredAt":"2026-01-01T00:00:00.0000000+00:00"}""";

        await using (var conn = new NpgsqlConnection(cs))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                """
                INSERT INTO "OutboxEvents" ("EventType", "Payload", "CreatedAt", "RetryCount")
                VALUES ('OrderPlaced', @p, NOW(), 0)
                """,
                conn);
            cmd.Parameters.AddWithValue("p", payload);
            await cmd.ExecuteNonQueryAsync();
        }

        var relay = new SqlOutboxRelay(cs);
        for (var i = 0; i < 5; i++)
        {
            await relay.ProcessPendingAsync(
                (_, _) => throw new InvalidOperationException($"forced failure {i}"),
                CancellationToken.None);
        }

        long dlqId;
        await using (var conn = new NpgsqlConnection(cs))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                """SELECT "Id" FROM "DeadLetterEvents" ORDER BY "Id" DESC LIMIT 1""",
                conn);
            var o = await cmd.ExecuteScalarAsync();
            Assert.NotNull(o);
            dlqId = (long)o!;
        }

        var repo = new OrderDlqAdminRepository(config);
        Assert.True(await repo.RetryAsync(dlqId, CancellationToken.None));

        var publishCalls = 0;
        await relay.ProcessPendingAsync(
            (_, _) =>
            {
                Interlocked.Increment(ref publishCalls);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(1, publishCalls);

        await using (var conn = new NpgsqlConnection(cs))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                """SELECT COUNT(*) FROM "OutboxEvents" WHERE "ProcessedAt" IS NULL""",
                conn);
            var pending = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(0, pending);
        }
    }
}
