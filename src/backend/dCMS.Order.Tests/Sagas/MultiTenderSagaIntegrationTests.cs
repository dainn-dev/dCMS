using dCMS.Core.Messaging;
using dCMS.Order.Core.Domain.Payments;
using dCMS.Order.Infrastructure.Payments;
using dCMS.Order.Infrastructure.Persistence;
using dCMS.Order.Infrastructure.Sagas;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace dCMS.Order.Tests.Sagas;

/// <summary>DAI-689: end-to-end saga + multi-tender orchestrator using MT in-memory test harness.</summary>
public sealed class MultiTenderSagaIntegrationTests
{
    [Fact]
    public async Task Place_pay_with_voucher_plus_gateway_reaches_Confirmed()
    {
        var (harness, repo) = await BuildAsync();
        var orderId = Guid.NewGuid();

        var plan = OrderPayment.Plan(orderId, 100m, new[]
        {
            (PaymentComponentType.Voucher, 40m, (string?)"PROMO10"),
            (PaymentComponentType.Gateway, 60m, (string?)null),
        });
        repo.Seed(plan);

        await harness.Bus.Publish(new OrderPlacedV1(
            orderId.ToString(), "t1", "s1", "cust-1", 100m, "USD",
            new List<OrderPlacedLineV1>(), DateTimeOffset.UtcNow));

        // Wait for saga to publish ReserveStockV1.
        Assert.True(await harness.Published.Any<ReserveStockV1>(x => x.Context.Message.OrderId == orderId.ToString()));

        await harness.Bus.Publish(new StockReservedV1(
            orderId.ToString(), "t1", "s1", DateTimeOffset.UtcNow));

        // Wait until ProcessPaymentV1 is published (saga → orchestrator),
        // then PaymentCompletedV1 is published (orchestrator → saga),
        // then OrderPaymentSettledV1 is published (saga at Confirmed).
        Assert.True(await harness.Published.Any<ProcessPaymentV1>(x => x.Context.Message.OrderId == orderId.ToString()));
        Assert.True(await harness.Published.Any<PaymentCompletedV1>(x => x.Context.Message.OrderId == orderId.ToString()));
        Assert.True(await harness.Published.Any<OrderPaymentSettledV1>(x => x.Context.Message.OrderId == orderId.ToString()));
        Assert.Equal("Captured", repo.LastSaved!.Status);
    }

    [Fact]
    public async Task Customer_cancel_after_confirmed_triggers_ReleasePaymentComponents()
    {
        var (harness, repo) = await BuildAsync();
        var orderId = Guid.NewGuid();

        var plan = OrderPayment.Plan(orderId, 100m, new[]
        {
            (PaymentComponentType.Voucher, 40m, (string?)"PROMO10"),
            (PaymentComponentType.Gateway, 60m, (string?)null),
        });
        repo.Seed(plan);

        await harness.Bus.Publish(new OrderPlacedV1(
            orderId.ToString(), "t1", "s1", "cust-1", 100m, "USD",
            new List<OrderPlacedLineV1>(), DateTimeOffset.UtcNow));
        Assert.True(await harness.Published.Any<ReserveStockV1>(x => x.Context.Message.OrderId == orderId.ToString()));

        await harness.Bus.Publish(new StockReservedV1(orderId.ToString(), "t1", "s1", DateTimeOffset.UtcNow));
        Assert.True(await harness.Published.Any<OrderPaymentSettledV1>(x => x.Context.Message.OrderId == orderId.ToString()));

        await harness.Bus.Publish(new OrderCustomerCancellationV1(
            orderId.ToString(), "t1", "s1", "user_request", DateTimeOffset.UtcNow));
        Assert.True(await harness.Published.Any<ReleasePaymentComponentsV1>(x => x.Context.Message.OrderId == orderId));

        // ReleaseConsumer needs the inactivity to complete before we read repo.LastSaved.
        await harness.InactivityTask;
        Assert.All(repo.LastSaved!.Components, c =>
            Assert.Contains(c.State, new[] { PaymentComponentState.Refunded, PaymentComponentState.Cancelled }));
    }

    private static async Task<(ITestHarness harness, FakeRepo repo)> BuildAsync()
    {
        var repo = new FakeRepo();
        var voucher = new AutoVoucher();
        var loyalty = new AutoLoyalty();
        var gateway = new StubGatewayTenderClient();
        var log = new InMemoryLog();

        var services = new ServiceCollection();
        services.AddSingleton<OrderPaymentRepository>(_ => repo);
        services.AddSingleton<IPaymentComponentDispatchLog>(log);
        services.AddSingleton<IVoucherTenderClient>(voucher);
        services.AddSingleton<ILoyaltyTenderClient>(loyalty);
        services.AddSingleton<IGatewayTenderClient>(gateway);
        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<PaymentOrchestrator>();
            cfg.AddConsumer<ReleasePaymentComponentsConsumer>();
            cfg.AddSagaStateMachine<OrderSaga, OrderSagaState>().InMemoryRepository();
        });
        var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, repo);
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

    private sealed class AutoVoucher : IVoucherTenderClient
    {
        public Task<TenderCallResult> ReserveAsync(string t, string c, Guid o, decimal a, CancellationToken ct)
            => Task.FromResult(TenderCallResult.Ok(Guid.NewGuid().ToString()));
        public Task<TenderCallResult> CaptureAsync(string t, Guid h, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> ReleaseAsync(string t, Guid h, string r, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> RefundAsync(string t, Guid h, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
    }

    private sealed class AutoLoyalty : ILoyaltyTenderClient
    {
        public Task<TenderCallResult> ReserveAsync(string t, string c, Guid o, decimal a, CancellationToken ct)
            => Task.FromResult(TenderCallResult.Ok(Guid.NewGuid().ToString()));
        public Task<TenderCallResult> CaptureAsync(string t, Guid h, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> ReleaseAsync(string t, Guid h, string r, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> RefundAsync(string t, Guid h, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
    }

    private sealed class InMemoryLog : IPaymentComponentDispatchLog
    {
        private readonly Dictionary<(Guid, Guid, string), DispatchOutcome> _store = new();
        public Task<DispatchOutcome?> TryGetAsync(Guid o, Guid c, string a, CancellationToken ct)
            => Task.FromResult(_store.TryGetValue((o, c, a), out var v) ? v : null);
        public Task RecordSuccessAsync(Guid o, Guid c, string a, string? r, CancellationToken ct)
        { _store[(o, c, a)] = new DispatchOutcome("Success", r, null, null); return Task.CompletedTask; }
        public Task RecordFailureAsync(Guid o, Guid c, string a, string? code, string? msg, CancellationToken ct)
        { _store[(o, c, a)] = new DispatchOutcome("Failed", null, code, msg); return Task.CompletedTask; }
    }
}
