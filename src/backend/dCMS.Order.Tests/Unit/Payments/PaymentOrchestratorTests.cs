using dCMS.Core.Messaging;
using dCMS.Order.Core.Domain.Payments;
using dCMS.Order.Infrastructure.Payments;
using dCMS.Order.Infrastructure.Persistence;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace dCMS.Order.Tests.Unit.Payments;

/// <summary>
/// DAI-724: focused tests for the multi-tender orchestrator. The repository is faked in-memory
/// (no Postgres) and tender clients are stubbed so we exercise: happy path, inline compensation
/// when a later component fails after an earlier one captured, and replay safety via the dispatch log.
/// </summary>
public sealed class PaymentOrchestratorTests
{
    [Fact]
    public async Task Happy_path_captures_voucher_then_loyalty_then_publishes_completed()
    {
        var (orchestrator, repo, voucher, loyalty, log, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 100m, new[]
        {
            (PaymentComponentType.Voucher, 40m, (string?)"PROMO10"),
            (PaymentComponentType.LoyaltyPoints, 60m, (string?)"cust-1"),
        });
        repo.Seed(plan);
        voucher.NextHoldId = Guid.NewGuid();
        loyalty.NextHoldId = Guid.NewGuid();

        await harness.Bus.Publish(new ProcessPaymentV1(
            Guid.NewGuid(), orderId.ToString(), "t1", "cust-1", 100m, "USD", "card",
            DateTimeOffset.UtcNow.AddMinutes(15)));
        await harness.InactivityTask;

        Assert.Equal(1, voucher.ReserveCalls);
        Assert.Equal(1, loyalty.ReserveCalls);
        Assert.Equal(1, voucher.CaptureCalls);
        Assert.True(await harness.Published.Any<PaymentCompletedV1>());
        var saved = repo.LastSaved!;
        Assert.All(saved.Components, c => Assert.Equal(PaymentComponentState.Captured, c.State));
        Assert.Equal("Captured", saved.Status);
    }

    [Fact]
    public async Task Loyalty_reserve_failure_triggers_inline_voucher_refund_and_payment_failed()
    {
        var (_, repo, voucher, loyalty, log, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 100m, new[]
        {
            (PaymentComponentType.Voucher, 40m, (string?)"PROMO10"),
            (PaymentComponentType.LoyaltyPoints, 60m, (string?)"cust-1"),
        });
        repo.Seed(plan);
        voucher.NextHoldId = Guid.NewGuid();
        loyalty.ReserveResult = TenderCallResult.Fail("insufficient_balance", "no points");

        await harness.Bus.Publish(new ProcessPaymentV1(
            Guid.NewGuid(), orderId.ToString(), "t1", "cust-1", 100m, "USD", "card",
            DateTimeOffset.UtcNow.AddMinutes(15)));
        await harness.InactivityTask;

        Assert.Equal(1, voucher.RefundCalls);
        Assert.True(await harness.Published.Any<PaymentFailedV1>());
        Assert.False(await harness.Published.Any<PaymentCompletedV1>());
        var saved = repo.LastSaved!;
        Assert.Equal(PaymentComponentState.Refunded, saved.Components[0].State);
        Assert.Equal(PaymentComponentState.Failed, saved.Components[1].State);
    }

    [Fact]
    public async Task Replay_short_circuits_via_dispatch_log()
    {
        var (_, repo, voucher, loyalty, log, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 50m, new[]
        {
            (PaymentComponentType.Voucher, 50m, (string?)"PROMO10"),
        });
        repo.Seed(plan);
        var voucherComponent = plan.Components[0];
        var prevHold = Guid.NewGuid();
        log.Seed(orderId, voucherComponent.Id, "RESERVE", "Success", prevHold.ToString());
        log.Seed(orderId, voucherComponent.Id, "CAPTURE", "Success", null);

        // Pre-set ExternalRef so the cached RESERVE result lands the holdId on the component.
        plan.Components[0].Authorize(prevHold.ToString());

        await harness.Bus.Publish(new ProcessPaymentV1(
            Guid.NewGuid(), orderId.ToString(), "t1", "cust-1", 50m, "USD", "card",
            DateTimeOffset.UtcNow.AddMinutes(15)));
        await harness.InactivityTask;

        Assert.Equal(0, voucher.ReserveCalls);
        Assert.Equal(0, voucher.CaptureCalls);
        Assert.True(await harness.Published.Any<PaymentCompletedV1>());
    }

    [Fact]
    public async Task Voucher_reserve_uses_component_reference_not_externalref()
    {
        var (_, repo, voucher, loyalty, _, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 25m, new[]
        {
            (PaymentComponentType.Voucher, 25m, (string?)"PROMO10"),
        });
        repo.Seed(plan);

        await harness.Bus.Publish(new ProcessPaymentV1(
            Guid.NewGuid(), orderId.ToString(), "t1", "cust-1", 25m, "USD", "card",
            DateTimeOffset.UtcNow.AddMinutes(15)));
        await harness.InactivityTask;

        Assert.Equal("PROMO10", voucher.LastReserveCode);
    }

    private static async Task<(PaymentOrchestrator orch, FakeRepo repo, FakeVoucherClient voucher, FakeLoyaltyClient loyalty, FakeDispatchLog log, ITestHarness harness)> BuildAsync()
    {
        var repo = new FakeRepo();
        var voucher = new FakeVoucherClient();
        var loyalty = new FakeLoyaltyClient();
        var gateway = new StubGatewayTenderClient();
        var log = new FakeDispatchLog();

        var services = new ServiceCollection();
        services.AddSingleton<OrderPaymentRepository>(_ => repo);
        services.AddSingleton<IPaymentComponentDispatchLog>(log);
        services.AddSingleton<IVoucherTenderClient>(voucher);
        services.AddSingleton<ILoyaltyTenderClient>(loyalty);
        services.AddSingleton<IGatewayTenderClient>(gateway);
        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<PaymentOrchestrator>();
        });
        var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orch = new PaymentOrchestrator(repo, log, voucher, loyalty, gateway, NullLogger<PaymentOrchestrator>.Instance);
        return (orch, repo, voucher, loyalty, log, harness);
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
        {
            payment.RecomputeStatus();
            LastSaved = payment;
            _seed = payment;
            return Task.CompletedTask;
        }
        public override Task<OrderPayment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
            => Task.FromResult(_seed);
    }

    private sealed class FakeVoucherClient : IVoucherTenderClient
    {
        public int ReserveCalls;
        public int CaptureCalls;
        public int ReleaseCalls;
        public int RefundCalls;
        public Guid? NextHoldId;
        public TenderCallResult? ReserveResult;
        public string? LastReserveCode;
        public Task<TenderCallResult> ReserveAsync(string tenantId, string code, Guid orderId, decimal amount, CancellationToken ct)
        {
            ReserveCalls++;
            LastReserveCode = code;
            if (ReserveResult is { } r) return Task.FromResult(r);
            return Task.FromResult(TenderCallResult.Ok((NextHoldId ?? Guid.NewGuid()).ToString()));
        }
        public Task<TenderCallResult> CaptureAsync(string tenantId, Guid holdId, CancellationToken ct) { CaptureCalls++; return Task.FromResult(TenderCallResult.Ok()); }
        public Task<TenderCallResult> ReleaseAsync(string tenantId, Guid holdId, string reason, CancellationToken ct) { ReleaseCalls++; return Task.FromResult(TenderCallResult.Ok()); }
        public Task<TenderCallResult> RefundAsync(string tenantId, Guid holdId, CancellationToken ct) { RefundCalls++; return Task.FromResult(TenderCallResult.Ok()); }
    }

    private sealed class FakeLoyaltyClient : ILoyaltyTenderClient
    {
        public int ReserveCalls;
        public Guid? NextHoldId;
        public TenderCallResult? ReserveResult;
        public Task<TenderCallResult> ReserveAsync(string tenantId, string customerId, Guid orderId, decimal amount, CancellationToken ct)
        {
            ReserveCalls++;
            if (ReserveResult is { } r) return Task.FromResult(r);
            return Task.FromResult(TenderCallResult.Ok((NextHoldId ?? Guid.NewGuid()).ToString()));
        }
        public Task<TenderCallResult> CaptureAsync(string tenantId, Guid holdId, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> ReleaseAsync(string tenantId, Guid holdId, string reason, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> RefundAsync(string tenantId, Guid holdId, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
    }

    private sealed class FakeDispatchLog : IPaymentComponentDispatchLog
    {
        private readonly Dictionary<(Guid, Guid, string), DispatchOutcome> _store = new();
        public void Seed(Guid o, Guid c, string a, string status, string? extRef)
            => _store[(o, c, a)] = new DispatchOutcome(status, extRef, null, null);
        public Task<DispatchOutcome?> TryGetAsync(Guid orderId, Guid componentId, string action, CancellationToken ct)
            => Task.FromResult(_store.TryGetValue((orderId, componentId, action), out var o) ? o : null);
        public Task RecordSuccessAsync(Guid orderId, Guid componentId, string action, string? externalRef, CancellationToken ct)
        { _store[(orderId, componentId, action)] = new DispatchOutcome("Success", externalRef, null, null); return Task.CompletedTask; }
        public Task RecordFailureAsync(Guid orderId, Guid componentId, string action, string? errorCode, string? errorMessage, CancellationToken ct)
        { _store[(orderId, componentId, action)] = new DispatchOutcome("Failed", null, errorCode, errorMessage); return Task.CompletedTask; }
    }
}
