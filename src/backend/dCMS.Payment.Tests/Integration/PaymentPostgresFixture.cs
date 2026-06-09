using dCMS.Payment.Infrastructure.Migrations;
using dCMS.Payment.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace dCMS.Payment.Tests.Integration;

[CollectionDefinition("PaymentPostgres", DisableParallelization = true)]
public sealed class PaymentPostgresCollection : ICollectionFixture<PaymentPostgresFixture>
{
}

/// <summary>PostgreSQL + DbUp migrations for Payment repository and webhook replay tests.</summary>
public sealed class PaymentPostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;

    public bool IsReady { get; private set; }
    public string ConnectionString { get; private set; } = "";
    public IPaymentTransactionRepository Repository { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("dcms_payment_it")
                .WithUsername("dcms")
                .WithPassword("test")
                .Build();

            await _postgres.StartAsync().ConfigureAwait(false);
            ConnectionString = _postgres.GetConnectionString();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Payment"] = ConnectionString,
                })
                .Build();

            PaymentDatabaseUpgrader.Run(configuration, NullLogger.Instance);

            Repository = new PostgresPaymentTransactionRepository(configuration);
            IsReady = true;
        }
        catch
        {
            IsReady = false;
            if (_postgres is not null)
            {
                await _postgres.DisposeAsync().ConfigureAwait(false);
                _postgres = null;
            }
        }
    }

    public async Task SeedTransactionAsync(PaymentTransactionInsert row, CancellationToken ct = default) =>
        await Repository.InsertInitiatedAsync(row, ct).ConfigureAwait(false);

    public static PaymentTransactionInsert SampleInsert(
        string paymentIntentId,
        Guid? tenantId = null,
        Guid? storeId = null,
        Guid? orderId = null,
        string? clientId = null,
        decimal amount = 42.5m) =>
        new(
            Guid.NewGuid(),
            orderId ?? Guid.NewGuid(),
            tenantId ?? PaymentTestSeeds.TenantA,
            storeId ?? PaymentTestSeeds.StoreA1,
            clientId ?? PaymentTestSeeds.ClientId,
            "cust-pay-it",
            "card",
            paymentIntentId,
            amount,
            "USD",
            PaymentTestSeeds.Provider);

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync().ConfigureAwait(false);
        _postgres = null;
    }
}
