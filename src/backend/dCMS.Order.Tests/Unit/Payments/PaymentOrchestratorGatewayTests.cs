using dCMS.Core.Messaging;
using dCMS.Order.Core.Domain.Payments;
using dCMS.Order.Infrastructure.Payments;
using dCMS.Order.Infrastructure.Persistence;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace dCMS.Order.Tests.Unit.Payments;

/// <summary>DAI-689: orchestrator behavior with the Gateway component arm wired.</summary>
public sealed class PaymentOrchestratorGatewayTests
{
    [Fact]
    public async Task Voucher_then_Gateway_happy_path_reaches_Captured()
    {
        var (repo, voucher, _, gateway, _, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 100m, new[]
        {
            (PaymentComponentType.Voucher, 40m, (string?)"PROMO10"),
            (PaymentComponentType.Gateway, 60m, (string?)null),
        });
        repo.Seed(plan);

        await harness.Bus.Publish(new ProcessPaymentV1(
            Guid.NewGuid(), orderId.ToString(), "t1", "cust-ok", 100m, "USD", "card",
            DateTimeOffset.UtcNow.AddMinutes(15)));
        await harness.InactivityTask;

        Assert.True(await harness.Published.Any<PaymentCompletedV1>());
        Assert.Equal(1, voucher.CaptureCalls);
        var saved = repo.LastSaved!;
        Assert.Equal("Captured", saved.Status);
        Assert.All(saved.Components, c => Assert.Equal(PaymentComponentState.Captured, c.State));
        Assert.StartsWith("ch_stub_", saved.Components.Single(c => c.Type == PaymentComponentType.Gateway).ExternalRef);
    }

    [Fact]
    public async Task Gateway_decline_after_voucher_captured_refunds_voucher_and_publishes_failed()
    {
        var (repo, voucher, _, _, _, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 100m, new[]
        {
            (PaymentComponentType.Voucher, 40m, (string?)"PROMO10"),
            (PaymentComponentType.Gateway, 60m, (string?)null),
        });
        repo.Seed(plan);

        await harness.Bus.Publish(new ProcessPaymentV1(
            Guid.NewGuid(), orderId.ToString(), "t1", "cust-decline", 100m, "USD", "card",
            DateTimeOffset.UtcNow.AddMinutes(15)));
        await harness.InactivityTask;

        Assert.True(await harness.Published.Any<PaymentFailedV1>());
        Assert.False(await harness.Published.Any<PaymentCompletedV1>());
        Assert.Equal(1, voucher.RefundCalls);
        var saved = repo.LastSaved!;
        Assert.Equal(PaymentComponentState.Refunded, saved.Components[0].State); // voucher rolled back
        Assert.Equal(PaymentComponentState.Failed,   saved.Components[1].State); // gateway failed
    }

    [Fact]
    public async Task Replay_does_not_recall_gateway_authorize()
    {
        var (repo, _, _, gateway, _, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 60m, new[]
        {
            (PaymentComponentType.Gateway, 60m, (string?)null),
        });
        repo.Seed(plan);

        await harness.Bus.Publish(new ProcessPaymentV1(
            Guid.NewGuid(), orderId.ToString(), "t1", "cust-1", 60m, "USD", "card",
            DateTimeOffset.UtcNow.AddMinutes(15)));
        await harness.InactivityTask;
        var firstAuthCount = gateway.AuthorizeCalls;

        await harness.Bus.Publish(new ProcessPaymentV1(
            Guid.NewGuid(), orderId.ToString(), "t1", "cust-1", 60m, "USD", "card",
            DateTimeOffset.UtcNow.AddMinutes(15)));
        await harness.InactivityTask;

        Assert.Equal(firstAuthCount, gateway.AuthorizeCalls);
    }

    private static async Task<(FakeRepo repo, FakeVoucher voucher, FakeLoyalty loyalty, CountingGateway gateway, InMemoryDispatchLog log, ITestHarness harness)> BuildAsync()
    {
        var repo = new FakeRepo();
        var voucher = new FakeVoucher();
        var loyalty = new FakeLoyalty();
        var gateway = new CountingGateway();
        var log = new InMemoryDispatchLog();

        var services = new ServiceCollection();
        services.AddSingleton<OrderPaymentRepository>(_ => repo);
        services.AddSingleton<IPaymentComponentDispatchLog>(log);
        services.AddSingleton<IVoucherTenderClient>(voucher);
        services.AddSingleton<ILoyaltyTenderClient>(loyalty);
        services.AddSingleton<IGatewayTenderClient>(gateway);
        services.AddMassTransitTestHarness(cfg => cfg.AddConsumer<PaymentOrchestrator>());
        var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (repo, voucher, loyalty, gateway, log, harness);
    }

    private sealed class FakeRepo : OrderPaymentRepository
    {
        private OrderPayment? _seed;
        public OrderPayment? LastSaved { get; private set; }
        public FakeRepo() : base(BuildStubConfig()) { }
        private static Microsoft.Extensions.Configuration.IConfiguration BuildStubConfig()
        {
            var cfg = new Microsoft.Extensions.Configuration.ConfigurationManager();
            cfg["ConnectionStrings:Order"] = "Host=stub";
            return cfg;
        }
        public void Seed(OrderPayment p) => _seed = p;
        public override Task UpsertAsync(OrderPayment payment, CancellationToken ct = default)
        { payment.RecomputeStatus(); LastSaved = payment; _seed = payment; return Task.CompletedTask; }
        public override Task<OrderPayment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
            => Task.FromResult(_seed);
    }

    private sealed class FakeVoucher : IVoucherTenderClient
    {
        public int CaptureCalls;
        public int RefundCalls;
        public Task<TenderCallResult> ReserveAsync(string t, string c, Guid o, decimal a, CancellationToken ct)
            => Task.FromResult(TenderCallResult.Ok(Guid.NewGuid().ToString()));
        public Task<TenderCallResult> CaptureAsync(string t, Guid h, CancellationToken ct) { CaptureCalls++; return Task.FromResult(TenderCallResult.Ok()); }
        public Task<TenderCallResult> ReleaseAsync(string t, Guid h, string r, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> RefundAsync(string t, Guid h, CancellationToken ct) { RefundCalls++; return Task.FromResult(TenderCallResult.Ok()); }
    }

    private sealed class FakeLoyalty : ILoyaltyTenderClient
    {
        public Task<TenderCallResult> ReserveAsync(string t, string c, Guid o, decimal a, CancellationToken ct)
            => Task.FromResult(TenderCallResult.Ok(Guid.NewGuid().ToString()));
        public Task<TenderCallResult> CaptureAsync(string t, Guid h, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> ReleaseAsync(string t, Guid h, string r, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> RefundAsync(string t, Guid h, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
    }

    private sealed class CountingGateway : IGatewayTenderClient
    {
        private readonly StubGatewayTenderClient _inner = new();
        public int AuthorizeCalls;
        public Task<TenderCallResult> AuthorizeAsync(string tenantId, string customerId, Guid orderId, decimal amount, string currency, CancellationToken ct)
        { AuthorizeCalls++; return _inner.AuthorizeAsync(tenantId, customerId, orderId, amount, currency, ct); }
        public Task<TenderCallResult> CaptureAsync(string tenantId, string chargeRef, CancellationToken ct) => _inner.CaptureAsync(tenantId, chargeRef, ct);
        public Task<TenderCallResult> VoidAsync(string tenantId, string chargeRef, string reason, CancellationToken ct) => _inner.VoidAsync(tenantId, chargeRef, reason, ct);
        public Task<TenderCallResult> RefundAsync(string tenantId, string chargeRef, CancellationToken ct) => _inner.RefundAsync(tenantId, chargeRef, ct);
    }

    private sealed class InMemoryDispatchLog : IPaymentComponentDispatchLog
    {
        private readonly Dictionary<(Guid, Guid, string), DispatchOutcome> _store = new();
        public Task<DispatchOutcome?> TryGetAsync(Guid orderId, Guid componentId, string action, CancellationToken ct)
            => Task.FromResult(_store.TryGetValue((orderId, componentId, action), out var o) ? o : null);
        public Task RecordSuccessAsync(Guid orderId, Guid componentId, string action, string? externalRef, CancellationToken ct)
        { _store[(orderId, componentId, action)] = new DispatchOutcome("Success", externalRef, null, null); return Task.CompletedTask; }
        public Task RecordFailureAsync(Guid orderId, Guid componentId, string action, string? errorCode, string? errorMessage, CancellationToken ct)
        { _store[(orderId, componentId, action)] = new DispatchOutcome("Failed", null, errorCode, errorMessage); return Task.CompletedTask; }
    }
}
