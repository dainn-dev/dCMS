using System.Net;
using System.Text;
using dCMS.Payment.Infrastructure.Persistence;
using dCMS.Payment.Infrastructure.Webhooks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace dCMS.Payment.Tests.Webhooks;

/// <summary>DAI-31 — webhook route tests: signature, stale, duplicate, and idempotent delivery.</summary>
[Collection("PaymentWebhookRoute")]
public sealed class PaymentWebhookRouteTests(PaymentWebhookFactory factory)
{
    private void ResetMocks()
    {
        factory.RepoMock.Reset();
        factory.ProcessorMock.Reset();
    }

    [Fact]
    public async Task Webhook_invalid_signature_returns_401()
    {
        ResetMocks();
        var body = PaymentWebhookTestHelpers.ValidBody("pi_test", Guid.NewGuid().ToString("D"));
        var req = PaymentWebhookTestHelpers.BuildRequest("stub", body, "sha256=badhex");

        var response = await factory.CreateClient().SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_stale_occurredAt_returns_400_REPLAY_REJECTED()
    {
        ResetMocks();
        var tenantId = Guid.NewGuid().ToString("D");
        var staleTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        var body = PaymentWebhookTestHelpers.ValidBody("pi_stale", tenantId, occurredAt: staleTime);
        var sig = PaymentWebhookTestHelpers.Sign(body, PaymentWebhookFactory.Secret);

        var response = await factory.CreateClient().SendAsync(PaymentWebhookTestHelpers.BuildRequest("stub", body, sig));
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("REPLAY_REJECTED", json);
    }

    [Fact]
    public async Task Webhook_duplicate_eventId_returns_200_without_processing()
    {
        ResetMocks();
        var tenantId = Guid.NewGuid().ToString("D");
        var body = PaymentWebhookTestHelpers.ValidBody("pi_dup", tenantId);
        var sig = PaymentWebhookTestHelpers.Sign(body, PaymentWebhookFactory.Secret);

        factory.RepoMock.Setup(r =>
                r.TryRecordWebhookDeliveryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var response = await factory.CreateClient().SendAsync(PaymentWebhookTestHelpers.BuildRequest("stub", body, sig));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        factory.ProcessorMock.Verify(
            p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Webhook_valid_fresh_delivery_processes_and_returns_200()
    {
        ResetMocks();
        var tenantId = Guid.NewGuid().ToString("D");
        var body = PaymentWebhookTestHelpers.ValidBody("pi_fresh", tenantId);
        var sig = PaymentWebhookTestHelpers.Sign(body, PaymentWebhookFactory.Secret);

        factory.RepoMock.Setup(r =>
                r.TryRecordWebhookDeliveryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        factory.ProcessorMock.Setup(p =>
                p.ProcessAsync("pi_fresh", It.IsAny<Guid>(), It.IsAny<string>(), "stub",
                    true, It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentWebhookProcessResult.Ok);

        var response = await factory.CreateClient().SendAsync(PaymentWebhookTestHelpers.BuildRequest("stub", body, sig));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_missing_signature_returns_401()
    {
        ResetMocks();
        var body = PaymentWebhookTestHelpers.ValidBody("pi_nosig", Guid.NewGuid().ToString("D"));
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/payment/stub")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        var response = await factory.CreateClient().SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_invalid_payload_returns_400()
    {
        ResetMocks();
        var body = """{"tenantId":"not-a-guid","status":"succeeded"}""";
        var sig = PaymentWebhookTestHelpers.Sign(body, PaymentWebhookFactory.Secret);

        var response = await factory.CreateClient().SendAsync(PaymentWebhookTestHelpers.BuildRequest("stub", body, sig));
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("INVALID_PAYLOAD", json);
    }

    [Fact]
    public async Task Webhook_unknown_intent_returns_202_Accepted()
    {
        ResetMocks();
        var tenantId = Guid.NewGuid().ToString("D");
        var body = PaymentWebhookTestHelpers.ValidBody("pi_unknown", tenantId);
        var sig = PaymentWebhookTestHelpers.Sign(body, PaymentWebhookFactory.Secret);

        factory.RepoMock.Setup(r =>
                r.TryRecordWebhookDeliveryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        factory.ProcessorMock.Setup(p =>
                p.ProcessAsync("pi_unknown", It.IsAny<Guid>(), It.IsAny<string>(), "stub",
                    true, It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentWebhookProcessResult.UnknownIntent);

        var response = await factory.CreateClient().SendAsync(PaymentWebhookTestHelpers.BuildRequest("stub", body, sig));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_state_conflict_returns_409()
    {
        ResetMocks();
        var tenantId = Guid.NewGuid().ToString("D");
        var body = PaymentWebhookTestHelpers.ValidBody("pi_conflict", tenantId, success: false);
        var sig = PaymentWebhookTestHelpers.Sign(body, PaymentWebhookFactory.Secret);

        factory.RepoMock.Setup(r =>
                r.TryRecordWebhookDeliveryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        factory.ProcessorMock.Setup(p =>
                p.ProcessAsync("pi_conflict", It.IsAny<Guid>(), It.IsAny<string>(), "stub",
                    false, It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentWebhookProcessResult.Conflict);

        var response = await factory.CreateClient().SendAsync(PaymentWebhookTestHelpers.BuildRequest("stub", body, sig));
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("CONFLICT", json);
    }

    [Fact]
    public async Task Webhook_failure_status_processes_via_http_stack()
    {
        ResetMocks();
        var tenantId = Guid.NewGuid().ToString("D");
        var body = PaymentWebhookTestHelpers.ValidBody("pi_fail_http", tenantId, success: false);
        var sig = PaymentWebhookTestHelpers.Sign(body, PaymentWebhookFactory.Secret);

        factory.RepoMock.Setup(r =>
                r.TryRecordWebhookDeliveryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        factory.ProcessorMock.Setup(p =>
                p.ProcessAsync("pi_fail_http", It.IsAny<Guid>(), It.IsAny<string>(), "stub",
                    false, It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentWebhookProcessResult.Ok);

        var response = await factory.CreateClient().SendAsync(PaymentWebhookTestHelpers.BuildRequest("stub", body, sig));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        factory.ProcessorMock.Verify(
            p => p.ProcessAsync("pi_fail_http", It.IsAny<Guid>(), It.IsAny<string>(), "stub",
                false, It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Webhook_unknown_provider_returns_404()
    {
        ResetMocks();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/payment/unknown_provider_xyz")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };

        var response = await factory.CreateClient().SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

[CollectionDefinition("PaymentWebhookRoute")]
public sealed class PaymentWebhookRouteCollection : ICollectionFixture<PaymentWebhookFactory>;

public sealed class PaymentWebhookFactory : WebApplicationFactory<PaymentApiProgram>
{
    public const string Secret = PaymentTestSeeds.WebhookSecret;
    public readonly Mock<IPaymentTransactionRepository> RepoMock = new();
    public readonly Mock<PaymentGatewayWebhookProcessor> ProcessorMock;

    public PaymentWebhookFactory()
    {
        ProcessorMock = new Mock<PaymentGatewayWebhookProcessor>(
            RepoMock.Object,
            Mock.Of<MassTransit.IBus>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentGatewayWebhookProcessor>.Instance);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("Payment:Providers:stub:WebhookSecret", Secret);
        builder.UseSetting("ConnectionStrings:Payment", "Host=localhost;Database=test;Username=test;Password=test");
        builder.UseSetting("RabbitMq:Host", "localhost");
        builder.UseSetting("Dcms:Client:Id", "aeon");
        builder.ConfigureServices(services =>
        {
            // Remove services that require live infrastructure
            RemoveAll<Microsoft.Extensions.Hosting.IHostedService>(services);
            RemoveMassTransit(services);

            // Replace with mocks
            Replace(services, RepoMock.Object);
            Replace(services, ProcessorMock.Object);
        });
    }

    private static void RemoveAll<T>(IServiceCollection services)
    {
        var toRemove = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var d in toRemove) services.Remove(d);
    }

    private static void RemoveMassTransit(IServiceCollection services)
    {
        var toRemove = services
            .Where(d => d.ServiceType.FullName?.StartsWith("MassTransit", StringComparison.Ordinal) == true)
            .ToList();
        foreach (var d in toRemove) services.Remove(d);
    }

    private static void Replace<T>(IServiceCollection services, T replacement) where T : class
    {
        var desc = services.FirstOrDefault(d => d.ServiceType == typeof(T));
        if (desc is not null) services.Remove(desc);
        services.AddSingleton(replacement);
    }
}
