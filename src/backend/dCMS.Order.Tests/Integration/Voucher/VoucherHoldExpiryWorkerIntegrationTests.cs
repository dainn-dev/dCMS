using dCMS.Voucher.Api.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Order.Tests.Integration.Voucher;

/// <summary>DAI-689: HoldExpiryWorker scans expired holds and releases them via SqlVoucherStore.</summary>
public sealed class VoucherHoldExpiryWorkerIntegrationTests : IAsyncLifetime
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
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "dCMS.Voucher.Api", "Migrations", "001_CreateVouchers.sql"));
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();

    [Fact]
    public async Task ListExpiredHolds_returns_only_held_rows_past_ExpiresAt()
    {
        var store = new SqlVoucherStore(_pg.GetConnectionString());
        await using var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();

        var voucherId = Guid.NewGuid();
        var orderActive = Guid.NewGuid();
        var orderExpired = Guid.NewGuid();
        var holdActive = Guid.NewGuid();
        var holdExpired = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var seed = new NpgsqlCommand(
            """
            INSERT INTO "Vouchers"("Id","TenantId","Code","FaceValue","RemainingValue","Currency","Status")
              VALUES (@vid,'t1','PROMO',100,100,'VND','Active');
            INSERT INTO "VoucherHolds"("Id","TenantId","VoucherId","OrderId","Amount","Status","ExpiresAt","CreatedAt","UpdatedAt")
              VALUES
                (@hActive,'t1',@vid,@oActive,10,'Held',@exp1,@now,@now),
                (@hExpired,'t1',@vid,@oExpired,10,'Held',@exp2,@now,@now);
            """, conn))
        {
            seed.Parameters.AddWithValue("vid", voucherId);
            seed.Parameters.AddWithValue("hActive", holdActive);
            seed.Parameters.AddWithValue("hExpired", holdExpired);
            seed.Parameters.AddWithValue("oActive", orderActive);
            seed.Parameters.AddWithValue("oExpired", orderExpired);
            seed.Parameters.AddWithValue("exp1", now.AddMinutes(15));
            seed.Parameters.AddWithValue("exp2", now.AddMinutes(-1));
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
        var store = new SqlVoucherStore(_pg.GetConnectionString());
        await using var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();

        var voucherId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var oldest = Guid.NewGuid();
        var middle = Guid.NewGuid();
        var newest = Guid.NewGuid();

        await using (var seed = new NpgsqlCommand(
            """
            INSERT INTO "Vouchers"("Id","TenantId","Code","FaceValue","RemainingValue","Currency","Status")
              VALUES (@vid,'t1','PROMO',100,100,'VND','Active');
            INSERT INTO "VoucherHolds"("Id","TenantId","VoucherId","OrderId","Amount","Status","ExpiresAt","CreatedAt","UpdatedAt")
              VALUES
                (@h1,'t1',@vid,@o1,5,'Held',@exp1,@now,@now),
                (@h2,'t1',@vid,@o2,5,'Held',@exp2,@now,@now),
                (@h3,'t1',@vid,@o3,5,'Held',@exp3,@now,@now);
            """, conn))
        {
            seed.Parameters.AddWithValue("vid", voucherId);
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
