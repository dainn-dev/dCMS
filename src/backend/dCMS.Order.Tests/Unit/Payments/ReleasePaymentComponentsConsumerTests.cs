using dCMS.Core.Messaging;
using dCMS.Order.Core.Domain.Payments;
using dCMS.Order.Infrastructure.Payments;
using dCMS.Order.Infrastructure.Persistence;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace dCMS.Order.Tests.Unit.Payments;

public sealed class ReleasePaymentComponentsConsumerTests
{
    [Fact]
    public async Task Captured_voucher_gets_refunded()
    {
        var (repo, voucher, _, _, _, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 40m, new[] { (PaymentComponentType.Voucher, 40m, (string?)"PROMO10") });
        var holdId = Guid.NewGuid();
        plan.Components[0].Authorize(holdId.ToString());
        plan.Components[0].Capture();
        repo.Seed(plan);

        await harness.Bus.Publish(new ReleasePaymentComponentsV1(
            orderId, "t1", "s1", "customer_cancel", DateTimeOffset.UtcNow));
        await harness.InactivityTask;

        Assert.Equal(1, voucher.RefundCalls);
        Assert.Equal(PaymentComponentState.Refunded, repo.LastSaved!.Components[0].State);
    }

    [Fact]
    public async Task Authorized_gateway_gets_voided()
    {
        var (repo, _, _, gateway, _, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 60m, new[] { (PaymentComponentType.Gateway, 60m, (string?)null) });
        plan.Components[0].Authorize("ch_stub_abc");
        repo.Seed(plan);

        await harness.Bus.Publish(new ReleasePaymentComponentsV1(
            orderId, "t1", "s1", "payment_timeout", DateTimeOffset.UtcNow));
        await harness.InactivityTask;

        Assert.Equal(1, gateway.VoidCalls);
        Assert.Equal(PaymentComponentState.Cancelled, repo.LastSaved!.Components[0].State);
    }

    [Fact]
    public async Task Pending_components_are_skipped()
    {
        var (repo, voucher, _, _, _, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 40m, new[] { (PaymentComponentType.Voucher, 40m, (string?)"PROMO10") });
        repo.Seed(plan);

        await harness.Bus.Publish(new ReleasePaymentComponentsV1(
            orderId, "t1", "s1", "customer_cancel", DateTimeOffset.UtcNow));
        await harness.InactivityTask;

        Assert.Equal(0, voucher.RefundCalls);
        Assert.Equal(0, voucher.ReleaseCalls);
    }

    [Fact]
    public async Task Replay_short_circuits_via_dispatch_log()
    {
        var (repo, voucher, _, _, log, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 40m, new[] { (PaymentComponentType.Voucher, 40m, (string?)"PROMO10") });
        var holdId = Guid.NewGuid();
        plan.Components[0].Authorize(holdId.ToString());
        plan.Components[0].Capture();
        repo.Seed(plan);
        log.Seed(orderId, plan.Components[0].Id, "REFUND", "Success", holdId.ToString());

        await harness.Bus.Publish(new ReleasePaymentComponentsV1(
            orderId, "t1", "s1", "customer_cancel", DateTimeOffset.UtcNow));
        await harness.InactivityTask;

        Assert.Equal(0, voucher.RefundCalls);
        Assert.Equal(PaymentComponentState.Refunded, repo.LastSaved!.Components[0].State);
    }

    private static async Task<(FakeRepo repo, FakeVoucher voucher, FakeLoyalty loyalty, FakeGateway gateway, FakeLog log, ITestHarness harness)> BuildAsync()
    {
        var repo = new FakeRepo();
        var voucher = new FakeVoucher();
        var loyalty = new FakeLoyalty();
        var gateway = new FakeGateway();
        var log = new FakeLog();

        var services = new ServiceCollection();
        services.AddSingleton<OrderPaymentRepository>(_ => repo);
        services.AddSingleton<IPaymentComponentDispatchLog>(log);
        services.AddSingleton<IVoucherTenderClient>(voucher);
        services.AddSingleton<ILoyaltyTenderClient>(loyalty);
        services.AddSingleton<IGatewayTenderClient>(gateway);
        services.AddMassTransitTestHarness(cfg => cfg.AddConsumer<ReleasePaymentComponentsConsumer>());
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
        public int RefundCalls; public int ReleaseCalls;
        public Task<TenderCallResult> ReserveAsync(string t, string c, Guid o, decimal a, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> CaptureAsync(string t, Guid h, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> ReleaseAsync(string t, Guid h, string r, CancellationToken ct) { ReleaseCalls++; return Task.FromResult(TenderCallResult.Ok()); }
        public Task<TenderCallResult> RefundAsync(string t, Guid h, CancellationToken ct) { RefundCalls++; return Task.FromResult(TenderCallResult.Ok()); }
    }
    private sealed class FakeLoyalty : ILoyaltyTenderClient
    {
        public Task<TenderCallResult> ReserveAsync(string t, string c, Guid o, decimal a, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> CaptureAsync(string t, Guid h, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> ReleaseAsync(string t, Guid h, string r, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> RefundAsync(string t, Guid h, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
    }
    private sealed class FakeGateway : IGatewayTenderClient
    {
        public int VoidCalls; public int RefundCalls;
        public Task<TenderCallResult> AuthorizeAsync(string t, string c, Guid o, decimal a, string cur, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok("ch_stub"));
        public Task<TenderCallResult> CaptureAsync(string t, string r, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> VoidAsync(string t, string r, string reason, CancellationToken ct) { VoidCalls++; return Task.FromResult(TenderCallResult.Ok()); }
        public Task<TenderCallResult> RefundAsync(string t, string r, CancellationToken ct) { RefundCalls++; return Task.FromResult(TenderCallResult.Ok()); }
    }
    public sealed class FakeLog : IPaymentComponentDispatchLog
    {
        private readonly Dictionary<(Guid, Guid, string), DispatchOutcome> _store = new();
        public void Seed(Guid o, Guid c, string a, string status, string? extRef)
            => _store[(o, c, a)] = new DispatchOutcome(status, extRef, null, null);
        public Task<DispatchOutcome?> TryGetAsync(Guid o, Guid c, string a, CancellationToken ct)
            => Task.FromResult(_store.TryGetValue((o, c, a), out var v) ? v : null);
        public Task RecordSuccessAsync(Guid o, Guid c, string a, string? r, CancellationToken ct)
        { _store[(o, c, a)] = new DispatchOutcome("Success", r, null, null); return Task.CompletedTask; }
        public Task RecordFailureAsync(Guid o, Guid c, string a, string? code, string? msg, CancellationToken ct)
        { _store[(o, c, a)] = new DispatchOutcome("Failed", null, code, msg); return Task.CompletedTask; }
    }
}
