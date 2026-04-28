using dCMS.Order.Infrastructure.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace dCMS.Order.Tests;

/// <summary>DAI-311 — DbUp applies embedded migrations to a real PostgreSQL instance.</summary>
public sealed class OrderMigrationsIntegrationTests : IAsyncLifetime
{
    private static void AssertTableExists(NpgsqlConnection conn, string quotedTableName)
    {
        using var cmd = new NpgsqlCommand(
            $"SELECT to_regclass('public.\"{quotedTableName}\"') IS NOT NULL",
            conn);
        var exists = (bool)(cmd.ExecuteScalar() ?? false);
        Assert.True(exists, $"Expected table {quotedTableName} to exist.");
    }

    private PostgreSqlContainer? _postgres;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    [Fact]
    public void Order_DbUp_creates_Orders_OrderItems_OrderSagaState()
    {
        var cs = _postgres!.GetConnectionString();
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Order"] = cs }).Build();

        OrderDatabaseUpgrader.Run(config, NullLogger.Instance);

        using var conn = new NpgsqlConnection(cs);
        conn.Open();
        AssertTableExists(conn, "Orders");
        AssertTableExists(conn, "OrderItems");
        AssertTableExists(conn, "OrderSagaState");
        AssertTableExists(conn, "ProcessedMessages");
        AssertTableExists(conn, "OrderFailures");
        AssertTableExists(conn, "OrderPayments");
        AssertTableExists(conn, "PaymentComponents");
    }
}
