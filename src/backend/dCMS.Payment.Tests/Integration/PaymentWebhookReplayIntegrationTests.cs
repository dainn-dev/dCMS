using System.Net;
using Dapper;
using dCMS.Payment.Infrastructure.Persistence;
using dCMS.Payment.Infrastructure.Webhooks;
using dCMS.Payment.Tests.Webhooks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;

namespace dCMS.Payment.Tests.Integration;

[Collection("PaymentPostgres")]
public sealed class PaymentWebhookReplayIntegrationTests(PaymentPostgresFixture pg)
{
    private PaymentWebhookReplayFactory? _factory;

    private void Skip()
    {
        Xunit.Skip.IfNot(pg.IsReady, "Docker / Testcontainers not available.");
        _factory ??= new PaymentWebhookReplayFactory(pg.ConnectionString);
    }

    [SkippableFact]
    public async Task Webhook_valid_delivery_records_row_and_processes()
    {
        Skip();
        const string intentId = "pi_wh_valid";
        const string eventId = "evt_wh_valid_001";
        await pg.SeedTransactionAsync(PaymentPostgresFixture.SampleInsert(intentId));

        _factory!.ProcessorMock.Reset();
        _factory.ProcessorMock
            .Setup(p => p.ProcessAsync(intentId, PaymentTestSeeds.TenantA, PaymentTestSeeds.ClientId,
                PaymentTestSeeds.Provider, true, It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentWebhookProcessResult.Ok);

        var body = PaymentWebhookTestHelpers.ValidBody(intentId, PaymentTestSeeds.TenantAStr, eventId: eventId);
        var sig = PaymentWebhookTestHelpers.Sign(body, PaymentTestSeeds.WebhookSecret);

        using var client = _factory.CreateClient();
        var response = await client.SendAsync(PaymentWebhookTestHelpers.BuildRequest(PaymentTestSeeds.Provider, body, sig));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var conn = new NpgsqlConnection(pg.ConnectionString);
        var count = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM "PaymentWebhookDeliveries"
            WHERE "Provider" = @Provider AND "EventId" = @EventId
            """,
            new { Provider = PaymentTestSeeds.Provider, EventId = eventId });

        Assert.Equal(1, count);
        _factory.ProcessorMock.Verify(
            p => p.ProcessAsync(intentId, PaymentTestSeeds.TenantA, PaymentTestSeeds.ClientId,
                PaymentTestSeeds.Provider, true, It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [SkippableFact]
    public async Task Webhook_duplicate_eventId_returns_200_without_reprocessing()
    {
        Skip();
        const string intentId = "pi_wh_dup";
        const string eventId = "evt_wh_dup_001";
        await pg.SeedTransactionAsync(PaymentPostgresFixture.SampleInsert(intentId));

        _factory!.ProcessorMock.Reset();
        _factory.ProcessorMock
            .Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentWebhookProcessResult.Ok);

        var body = PaymentWebhookTestHelpers.ValidBody(intentId, PaymentTestSeeds.TenantAStr, eventId: eventId);
        var sig = PaymentWebhookTestHelpers.Sign(body, PaymentTestSeeds.WebhookSecret);
        using var client = _factory.CreateClient();

        var first = await client.SendAsync(PaymentWebhookTestHelpers.BuildRequest(PaymentTestSeeds.Provider, body, sig));
        var second = await client.SendAsync(PaymentWebhookTestHelpers.BuildRequest(PaymentTestSeeds.Provider, body, sig));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        _factory.ProcessorMock.Verify(
            p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        await using var conn = new NpgsqlConnection(pg.ConnectionString);
        var count = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM "PaymentWebhookDeliveries"
            WHERE "Provider" = @Provider AND "EventId" = @EventId
            """,
            new { Provider = PaymentTestSeeds.Provider, EventId = eventId });

        Assert.Equal(1, count);
    }

    [SkippableFact]
    public async Task Webhook_different_eventId_same_payload_may_process_twice()
    {
        Skip();
        const string intentId = "pi_wh_two_events";
        await pg.SeedTransactionAsync(PaymentPostgresFixture.SampleInsert(intentId));

        _factory!.ProcessorMock.Reset();
        _factory.ProcessorMock
            .Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentWebhookProcessResult.Ok);

        var body1 = PaymentWebhookTestHelpers.ValidBody(intentId, PaymentTestSeeds.TenantAStr, eventId: "evt_a");
        var body2 = PaymentWebhookTestHelpers.ValidBody(intentId, PaymentTestSeeds.TenantAStr, eventId: "evt_b");
        var sig1 = PaymentWebhookTestHelpers.Sign(body1, PaymentTestSeeds.WebhookSecret);
        var sig2 = PaymentWebhookTestHelpers.Sign(body2, PaymentTestSeeds.WebhookSecret);
        using var client = _factory.CreateClient();

        await client.SendAsync(PaymentWebhookTestHelpers.BuildRequest(PaymentTestSeeds.Provider, body1, sig1));
        await client.SendAsync(PaymentWebhookTestHelpers.BuildRequest(PaymentTestSeeds.Provider, body2, sig2));

        _factory.ProcessorMock.Verify(
            p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}

public sealed class PaymentWebhookReplayFactory : WebApplicationFactory<PaymentApiProgram>
{
    private readonly string _connectionString;

    public readonly Mock<PaymentGatewayWebhookProcessor> ProcessorMock;

    public PaymentWebhookReplayFactory(string connectionString)
    {
        _connectionString = connectionString;
        ProcessorMock = new Mock<PaymentGatewayWebhookProcessor>(
            Mock.Of<IPaymentTransactionRepository>(),
            Mock.Of<MassTransit.IBus>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentGatewayWebhookProcessor>.Instance);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("ConnectionStrings:Payment", _connectionString);
        builder.UseSetting("Payment:Providers:stub:WebhookSecret", PaymentTestSeeds.WebhookSecret);
        builder.UseSetting("Dcms:Client:Id", PaymentTestSeeds.ClientId);
        builder.UseSetting("RabbitMq:Host", "localhost");
        builder.ConfigureServices(services =>
        {
            PaymentTestHost.RemoveHostedServices(services);
            PaymentTestHost.RemoveMassTransit(services);

            var repoDesc = services.FirstOrDefault(d => d.ServiceType == typeof(IPaymentTransactionRepository));
            if (repoDesc is not null)
                services.Remove(repoDesc);
            services.AddSingleton<IPaymentTransactionRepository, PostgresPaymentTransactionRepository>();

            var procDesc = services.FirstOrDefault(d => d.ServiceType == typeof(PaymentGatewayWebhookProcessor));
            if (procDesc is not null)
                services.Remove(procDesc);
            services.AddSingleton(ProcessorMock.Object);
        });
    }
}
