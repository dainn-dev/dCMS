using Dapper;
using dCMS.Core.Exceptions;
using dCMS.Core.Messaging;
using dCMS.Inventory.Infrastructure;
using dCMS.Inventory.Models;
using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Tests.Integration.Inventory;

public sealed class StockConcurrencyIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private bool _dockerReady;

    public async Task InitializeAsync()
    {
        _dockerReady = false;
        try
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("dcms_inventory")
                .WithUsername("dcms")
                .WithPassword("test")
                .Build();
            await _container.StartAsync();

            await using var conn = new NpgsqlConnection(_container.GetConnectionString());
            await conn.OpenAsync();
            var path = Path.Combine(AppContext.BaseDirectory, "007_CreateInventory.sql");
            var script = await File.ReadAllTextAsync(path);
            await conn.ExecuteAsync(script);

            await conn.ExecuteAsync(
                """
                INSERT INTO "Warehouses" ("Id", "TenantId", "StoreId", "Name", "IsActive")
                VALUES (@Id, @TenantId, @StoreId, @Name, TRUE);
                INSERT INTO "VariantStock" ("VariantId", "WarehouseId", "Quantity", "ReservedQuantity")
                VALUES (@VariantId, @Id, 10, 0);
                """,
                new { Id = "wh_1", TenantId = "t1", StoreId = "s1", Name = "Main", VariantId = "var_1" });

            _dockerReady = true;
        }
        catch
        {
            _dockerReady = false;
            if (_container is not null)
            {
                await _container.DisposeAsync();
                _container = null;
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    [SkippableFact]
    public async Task Commit_with_stale_revision_throws_StockConcurrencyException()
    {
        Skip.IfNot(_dockerReady, "Docker / Testcontainers not available.");

        var cs = _container!.GetConnectionString();
        var repo = new SqlStockPersistence(cs);

        var stock = await repo.GetStockAsync("t1", "s1", "var_1", "wh_1");
        stock.Should().NotBeNull();
        stock!.Adjust(1);
        var movement = StockMovement.ForAppend(stock.VariantId, stock.WarehouseId, 1, StockMovementType.Adjustment, "itest",
            null, DateTimeOffset.UtcNow);
        var envelope = new StockUpdatedV1(stock.VariantId, stock.WarehouseId, "t1", "s1", stock.Quantity, stock.ReservedQuantity,
            movement.CreatedAt);

        stock.RowVersion = -1L;

        await Assert.ThrowsAsync<StockConcurrencyException>(() =>
            repo.CommitStockChangeAsync("t1", "s1", stock, movement, envelope));
    }
}
