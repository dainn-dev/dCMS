using dCMS.Loyalty.Api.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Order.Tests.Integration.Loyalty;

public sealed class LoyaltyHoldExpiryWorkerIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        await using var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();
        var sql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "dCMS.Loyalty.Api", "Migrations", "001_CreateLoyaltyLedger.sql"));
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();

    [Fact]
    public async Task ListExpiredHolds_returns_only_held_past_expiry()
    {
        var store = new SqlLoyaltyStore(_pg.GetConnectionString());
        await using var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();

        var now = DateTimeOffset.UtcNow;
        var holdActive = Guid.NewGuid();
        var holdExpired = Guid.NewGuid();

        await using (var seed = new NpgsqlCommand(
            """
            INSERT INTO "LoyaltyLedger"("TenantId","CustomerId","Delta","Reason","OccurredAt")
              VALUES ('t1','cust-1',1000,'EARN',@now);
            INSERT INTO "LoyaltyHolds"("Id","TenantId","CustomerId","OrderId","Amount","Status","ExpiresAt","CreatedAt","UpdatedAt")
              VALUES
                (@hA,'t1','cust-1',@oA,100,'Held',@expA,@now,@now),
                (@hE,'t1','cust-2',@oE,100,'Held',@expE,@now,@now);
            """, conn))
        {
            seed.Parameters.AddWithValue("hA", holdActive);
            seed.Parameters.AddWithValue("hE", holdExpired);
            seed.Parameters.AddWithValue("oA", Guid.NewGuid());
            seed.Parameters.AddWithValue("oE", Guid.NewGuid());
            seed.Parameters.AddWithValue("expA", now.AddMinutes(15));
            seed.Parameters.AddWithValue("expE", now.AddMinutes(-1));
            seed.Parameters.AddWithValue("now", now);
            await seed.ExecuteNonQueryAsync();
        }

        var expired = await store.ListExpiredHoldsAsync(now, 100, default);
        Assert.Single(expired);
        Assert.Equal(holdExpired, expired[0].HoldId);
    }

    [Fact]
    public async Task ListExpiredHolds_respects_limit_and_orders_by_ExpiresAt_ASC()
    {
        var store = new SqlLoyaltyStore(_pg.GetConnectionString());
        await using var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();

        var now = DateTimeOffset.UtcNow;
        var oldest = Guid.NewGuid();
        var middle = Guid.NewGuid();
        var newest = Guid.NewGuid();

        await using (var seed = new NpgsqlCommand(
            """
            INSERT INTO "LoyaltyLedger"("TenantId","CustomerId","Delta","Reason","OccurredAt")
              VALUES ('t1','cust-1',1000,'EARN',@now);
            INSERT INTO "LoyaltyHolds"("Id","TenantId","CustomerId","OrderId","Amount","Status","ExpiresAt","CreatedAt","UpdatedAt")
              VALUES
                (@h1,'t1','c1',@o1,5,'Held',@exp1,@now,@now),
                (@h2,'t1','c2',@o2,5,'Held',@exp2,@now,@now),
                (@h3,'t1','c3',@o3,5,'Held',@exp3,@now,@now);
            """, conn))
        {
            seed.Parameters.AddWithValue("h1", oldest);
            seed.Parameters.AddWithValue("h2", middle);
            seed.Parameters.AddWithValue("h3", newest);
            seed.Parameters.AddWithValue("o1", Guid.NewGuid());
            seed.Parameters.AddWithValue("o2", Guid.NewGuid());
            seed.Parameters.AddWithValue("o3", Guid.NewGuid());
            seed.Parameters.AddWithValue("exp1", now.AddMinutes(-30));
            seed.Parameters.AddWithValue("exp2", now.AddMinutes(-20));
            seed.Parameters.AddWithValue("exp3", now.AddMinutes(-10));
            seed.Parameters.AddWithValue("now", now);
            await seed.ExecuteNonQueryAsync();
        }

        var firstBatch = await store.ListExpiredHoldsAsync(now, limit: 2, default);
        Assert.Equal(2, firstBatch.Count);
        Assert.Equal(oldest, firstBatch[0].HoldId);
        Assert.Equal(middle, firstBatch[1].HoldId);
    }
}
