using Dapper;
using Npgsql;

namespace dCMS.Payment.Tests.Integration;

[Collection("PaymentPostgres")]
public sealed class PostgresPaymentTransactionRepositoryIntegrationTests(PaymentPostgresFixture fx)
{
    private void Skip() =>
        Xunit.Skip.IfNot(fx.IsReady, "Docker / Testcontainers not available.");

    [SkippableFact]
    public async Task GetLatestByPaymentIntentId_different_tenant_returns_null()
    {
        Skip();
        const string intentId = "pi_tenant_iso";
        await fx.SeedTransactionAsync(PaymentPostgresFixture.SampleInsert(intentId));

        var row = await fx.Repository.GetLatestByPaymentIntentIdAsync(
            intentId, PaymentTestSeeds.TenantB, PaymentTestSeeds.ClientId, PaymentTestSeeds.Provider);

        Assert.Null(row);
    }

    [SkippableFact]
    public async Task GetLatestByPaymentIntentId_different_client_returns_null()
    {
        Skip();
        const string intentId = "pi_client_iso";
        await fx.SeedTransactionAsync(PaymentPostgresFixture.SampleInsert(intentId));

        var row = await fx.Repository.GetLatestByPaymentIntentIdAsync(
            intentId, PaymentTestSeeds.TenantA, PaymentTestSeeds.OtherClientId, PaymentTestSeeds.Provider);

        Assert.Null(row);
    }

    [SkippableFact]
    public async Task GetLatestByPaymentIntentId_different_store_returns_null()
    {
        Skip();
        const string intentId = "pi_store_iso";
        await fx.SeedTransactionAsync(PaymentPostgresFixture.SampleInsert(intentId));

        var row = await fx.Repository.GetLatestByPaymentIntentIdAsync(
            intentId, PaymentTestSeeds.TenantA, PaymentTestSeeds.ClientId, PaymentTestSeeds.Provider,
            PaymentTestSeeds.StoreB1);

        Assert.Null(row);
    }

    [SkippableFact]
    public async Task GetLatestByPaymentIntentId_matching_scope_returns_row()
    {
        Skip();
        const string intentId = "pi_match";
        var insert = PaymentPostgresFixture.SampleInsert(intentId);
        await fx.SeedTransactionAsync(insert);

        var row = await fx.Repository.GetLatestByPaymentIntentIdAsync(
            intentId, PaymentTestSeeds.TenantA, PaymentTestSeeds.ClientId, PaymentTestSeeds.Provider,
            PaymentTestSeeds.StoreA1);

        Assert.NotNull(row);
        Assert.Equal(insert.PaymentIntentId, row!.PaymentIntentId);
        Assert.Equal(insert.TenantId, row.TenantId);
        Assert.Equal(insert.StoreId, row.StoreId);
    }

    [SkippableFact]
    public async Task TryRecordWebhookDelivery_first_insert_returns_true()
    {
        Skip();
        var recorded = await fx.Repository.TryRecordWebhookDeliveryAsync(
            PaymentTestSeeds.Provider, "evt_first", "digest1", DateTimeOffset.UtcNow);

        Assert.True(recorded);
    }

    [SkippableFact]
    public async Task TryRecordWebhookDelivery_duplicate_event_returns_false()
    {
        Skip();
        const string eventId = "evt_dup_repo";
        var first = await fx.Repository.TryRecordWebhookDeliveryAsync(
            PaymentTestSeeds.Provider, eventId, "digest_a", DateTimeOffset.UtcNow);
        var second = await fx.Repository.TryRecordWebhookDeliveryAsync(
            PaymentTestSeeds.Provider, eventId, "digest_b", DateTimeOffset.UtcNow);

        Assert.True(first);
        Assert.False(second);
    }

    [SkippableFact]
    public async Task TryRecordWebhookDelivery_duplicate_leaves_single_row()
    {
        Skip();
        const string eventId = "evt_single_row";
        await fx.Repository.TryRecordWebhookDeliveryAsync(
            PaymentTestSeeds.Provider, eventId, "digest_x", DateTimeOffset.UtcNow);
        await fx.Repository.TryRecordWebhookDeliveryAsync(
            PaymentTestSeeds.Provider, eventId, "digest_y", DateTimeOffset.UtcNow);

        await using var conn = new NpgsqlConnection(fx.ConnectionString);
        var count = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM "PaymentWebhookDeliveries"
            WHERE "Provider" = @Provider AND "EventId" = @EventId
            """,
            new { Provider = PaymentTestSeeds.Provider, EventId = eventId });

        Assert.Equal(1, count);
    }
}
