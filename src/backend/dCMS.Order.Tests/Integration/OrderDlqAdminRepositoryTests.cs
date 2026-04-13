using dCMS.Order.Infrastructure.Migrations;
using dCMS.Order.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Order.Tests.Integration;

public sealed class OrderDlqAdminRepositoryTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private string? _cs;
    private IOrderDlqAdminRepository? _repo;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _postgres.StartAsync();
        _cs = _postgres.GetConnectionString();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Order"] = _cs })
            .Build();
        OrderDatabaseUpgrader.Run(configuration, NullLogger.Instance);
        _repo = new OrderDlqAdminRepository(configuration);
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Discard_then_list_excludes_when_not_include_discarded()
    {
        var id = await InsertDlqRowAsync(
            """{"orderId":"11111111-1111-1111-1111-111111111111"}""",
            "OrderPlaced",
            "boom");

        var discarded = await _repo!.DiscardAsync(id, "test reason", CancellationToken.None);
        Assert.True(discarded);

        var all = await _repo.ListAsync(null, null, null, CancellationToken.None);
        Assert.Contains(all, r => r.Id == id && r.DiscardedAt is not null);

        var active = all.Where(r => r.DiscardedAt is null).ToList();
        Assert.DoesNotContain(active, r => r.Id == id);
    }

    [Fact]
    public async Task Retry_inserts_outbox_and_marks_reprocessed()
    {
        var payload = """{"orderId":"22222222-2222-2222-2222-222222222222"}""";
        var id = await InsertDlqRowAsync(payload, "OrderPlaced", "fail");

        var ok = await _repo!.RetryAsync(id, CancellationToken.None);
        Assert.True(ok);

        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """SELECT COUNT(*)::bigint FROM "OutboxEvents" WHERE "Payload" = @p AND "ProcessedAt" IS NULL""",
            conn);
        cmd.Parameters.AddWithValue("p", payload);
        var pending = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        Assert.True(pending >= 1);

        var row = await _repo.GetAsync(id, CancellationToken.None);
        Assert.NotNull(row?.ReprocessedAt);
    }

    private async Task<long> InsertDlqRowAsync(string payload, string eventType, string reason)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO "DeadLetterEvents" ("SourceOutboxId", "EventType", "Payload", "FailureReason", "FailedAt")
            VALUES (NULL, @et, @pl, @rs, NOW())
            RETURNING "Id"
            """,
            conn);
        cmd.Parameters.AddWithValue("et", eventType);
        cmd.Parameters.AddWithValue("pl", payload);
        cmd.Parameters.AddWithValue("rs", reason);
        var id = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        Assert.True(id > 0);
        return id;
    }
}
