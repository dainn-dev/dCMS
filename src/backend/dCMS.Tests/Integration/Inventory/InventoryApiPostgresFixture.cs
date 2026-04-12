using Dapper;
using dCMS.Inventory.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Tests.Integration.Inventory;

/// <summary>PostgreSQL + schema 007 + seed row; <see cref="WebApplicationFactory{TEntryPoint}"/> with <c>Auth:Enabled=false</c>.</summary>
public sealed class InventoryApiPostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private WebApplicationFactory<InventoryApiAssemblyMarker>? _factory;

    public WebApplicationFactory<InventoryApiAssemblyMarker>? Factory => _factory;
    public bool IsReady { get; private set; }
    public string? TestConnectionString => _container?.GetConnectionString();

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("dcms_inventory")
                .WithUsername("dcms")
                .WithPassword("test")
                .Build();
            await _container.StartAsync();

            var cs = _container.GetConnectionString();
            await using (var conn = new NpgsqlConnection(cs))
            {
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
                    INSERT INTO "Warehouses" ("Id", "TenantId", "StoreId", "Name", "IsActive")
                    VALUES (@Id2, @TenantId, @StoreId, @Name2, TRUE);
                    INSERT INTO "VariantStock" ("VariantId", "WarehouseId", "Quantity", "ReservedQuantity")
                    VALUES (@VariantId2, @Id2, 3, 0);
                    """,
                    new
                    {
                        Id = "wh_1",
                        Id2 = "wh_2",
                        TenantId = "t1",
                        StoreId = "s1",
                        Name = "Main",
                        Name2 = "Secondary",
                        VariantId = "var_1",
                        VariantId2 = "var_missing_bulk"
                    });
            }

            _factory = new WebApplicationFactory<InventoryApiAssemblyMarker>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Inventory", cs);
                builder.UseSetting("ConnectionStrings:Audit", cs);
                builder.UseSetting("Auth:Enabled", "false");
                builder.UseSetting("InternalInventory:ApiKey", "integration-test-internal-inventory-key-min-32!");
            });

            IsReady = true;
        }
        catch
        {
            IsReady = false;
            if (_factory is not null)
            {
                await _factory.DisposeAsync();
                _factory = null;
            }

            if (_container is not null)
            {
                await _container.DisposeAsync();
                _container = null;
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
            await _factory.DisposeAsync();
        _factory = null;
        if (_container is not null)
            await _container.DisposeAsync();
        _container = null;
    }
}
