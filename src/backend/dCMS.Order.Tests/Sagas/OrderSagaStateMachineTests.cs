using dCMS.Core.Messaging;
using dCMS.Order.Infrastructure.Sagas;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace dCMS.Order.Tests.Sagas;

/// <summary>DAI-317 — Order saga transitions via MassTransit in-memory test harness. DAI-318 — timeout messages.</summary>
public sealed class OrderSagaStateMachineTests
{
    private static void ConfigureOrderSagaHarness(IBusRegistrationConfigurator cfg) =>
        cfg.AddSagaStateMachine<OrderSaga, OrderSagaState>().InMemoryRepository();

    private static OrderPlacedV1 Placed(string orderId) =>
        new(
            orderId,
            TenantId: "tenant-1",
            StoreId: "store-1",
            CustomerId: "cust-1",
            TotalAmount: 42.5m,
            Currency: "USD",
            Lines: [new OrderPlacedLineV1("var-1", "wh-1", 2)],
            OccurredAt: DateTimeOffset.UtcNow);

    [Fact]
    public async Task OrderPlaced_publishes_ReserveStock_and_saga_enters_Placed()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(ConfigureOrderSagaHarness)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);

            await harness.Bus.Publish(Placed(orderId));
            await harness.InactivityTask;

            Assert.True(await harness.Published.Any<ReserveStockV1>());
            var sagaHarness = harness.GetSagaStateMachineHarness<OrderSaga, OrderSagaState>();
            Assert.True(await sagaHarness.Created.Any(x => x.CorrelationId == correlationId));
            var instance = sagaHarness.Created.ContainsInState(
                correlationId,
                sagaHarness.StateMachine,
                sagaHarness.StateMachine.Placed);
            Assert.NotNull(instance);
            Assert.Equal(orderId, instance!.OrderId);
            Assert.Equal(correlationId, instance.CorrelationId);
            Assert.Equal("Placed", instance.CurrentState);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Happy_path_StockReserved_then_PaymentCompleted_enters_Confirmed()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(ConfigureOrderSagaHarness)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var sagaHarness = harness.GetSagaStateMachineHarness<OrderSaga, OrderSagaState>();
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);
            var at = DateTimeOffset.UtcNow;

            await harness.Bus.Publish(Placed(orderId));
            await harness.InactivityTask;
            Assert.True(await harness.Published.Any<ReserveStockV1>());

            await harness.Bus.Publish(new StockReservedV1(orderId, "tenant-1", "store-1", at));
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.PaymentPending, TimeSpan.FromSeconds(5)));

            await harness.Bus.Publish(new PaymentCompletedV1(orderId, "pay-1", "tenant-1", "store-1", at));
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Confirmed, TimeSpan.FromSeconds(5)));
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task StockReservationFailed_enters_Cancelled_without_ProcessPayment()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(ConfigureOrderSagaHarness)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);
            var at = DateTimeOffset.UtcNow;

            await harness.Bus.Publish(Placed(orderId));
            await harness.InactivityTask;

            await harness.Bus.Publish(
                new StockReservationFailedV1(orderId, "stock_unavailable", "tenant-1", "store-1", at));
            await harness.InactivityTask;

            var sagaHarness = harness.GetSagaStateMachineHarness<OrderSaga, OrderSagaState>();
            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Cancelled, TimeSpan.FromSeconds(5)));

            Assert.True(
                await harness.Sent.Any<OrderCancelledV1>() || await harness.Published.Any<OrderCancelledV1>());
            Assert.Contains(
                harness.Published.Select<OrderCancelledV1>().Select(x => x.Context.Message.Reason),
                r => r == "stock_unavailable");
            Assert.False(
                await harness.Sent.Any<ReleaseStockV1>() || await harness.Published.Any<ReleaseStockV1>());
            Assert.False(await harness.Published.Any<ProcessPaymentV1>());
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task PaymentFailed_transitions_saga_to_Cancelled()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(ConfigureOrderSagaHarness)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);
            var at = DateTimeOffset.UtcNow;

            await harness.Bus.Publish(Placed(orderId));
            await harness.InactivityTask;
            await harness.Bus.Publish(new StockReservedV1(orderId, "tenant-1", "store-1", at));
            await harness.InactivityTask;

            await harness.Bus.Publish(new PaymentFailedV1(orderId, "payment_failed", "tenant-1", "store-1", at));
            await harness.InactivityTask;

            var sagaHarness = harness.GetSagaStateMachineHarness<OrderSaga, OrderSagaState>();
            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Cancelled, TimeSpan.FromSeconds(5)));

            Assert.True(
                await harness.Sent.Any<ReleaseStockV1>() || await harness.Published.Any<ReleaseStockV1>());
            Assert.True(
                await harness.Sent.Any<OrderCancelledV1>() || await harness.Published.Any<OrderCancelledV1>());
            Assert.Contains(
                harness.Published.Select<OrderCancelledV1>().Select(x => x.Context.Message.Reason),
                r => r == "payment_failed");
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task StockReservationTimeout_enters_Cancelled_without_ProcessPayment()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(ConfigureOrderSagaHarness)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);

            await harness.Bus.Publish(Placed(orderId));
            await harness.InactivityTask;

            await harness.Bus.Publish(new StockReservationTimeoutV1(orderId));
            await harness.InactivityTask;

            var sagaHarness = harness.GetSagaStateMachineHarness<OrderSaga, OrderSagaState>();
            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Cancelled, TimeSpan.FromSeconds(5)));

            Assert.True(
                await harness.Sent.Any<OrderCancelledV1>() || await harness.Published.Any<OrderCancelledV1>());
            Assert.Contains(
                harness.Published.Select<OrderCancelledV1>().Select(x => x.Context.Message.Reason),
                r => r == "stock_reservation_timeout");
            Assert.False(await harness.Published.Any<ProcessPaymentV1>());
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Fulfillment_messages_reach_Delivered_after_Confirmed()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(ConfigureOrderSagaHarness)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var sagaHarness = harness.GetSagaStateMachineHarness<OrderSaga, OrderSagaState>();
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);
            var at = DateTimeOffset.UtcNow;

            await harness.Bus.Publish(Placed(orderId));
            await harness.InactivityTask;
            await harness.Bus.Publish(new StockReservedV1(orderId, "tenant-1", "store-1", at));
            await harness.InactivityTask;
            await harness.Bus.Publish(new PaymentCompletedV1(orderId, "pay-1", "tenant-1", "store-1", at));
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Confirmed, TimeSpan.FromSeconds(5)));

            await harness.Bus.Publish(new OrderFulfillmentStartedV1(orderId, "tenant-1", "store-1", at));
            await harness.InactivityTask;
            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Processing, TimeSpan.FromSeconds(5)));

            await harness.Bus.Publish(new OrderShippedForSagaV1(orderId, "tenant-1", "store-1", at));
            await harness.InactivityTask;
            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Shipped, TimeSpan.FromSeconds(5)));

            await harness.Bus.Publish(new OrderDeliveredForSagaV1(orderId, "tenant-1", "store-1", at));
            await harness.InactivityTask;
            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Delivered, TimeSpan.FromSeconds(5)));
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task PaymentTimeout_then_late_PaymentCompleted_enters_LatePaymentRefunding_then_PaymentRefunded_settles()
    {
        var orderId = Guid.NewGuid().ToString();
        var correlationId = Guid.Parse(orderId);
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var at = DateTimeOffset.UtcNow;

        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddMassTransitTestHarness(ConfigureOrderSagaHarness)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var sagaHarness = harness.GetSagaStateMachineHarness<OrderSaga, OrderSagaState>();

            await harness.Bus.Publish(
                new OrderPlacedV1(
                    orderId,
                    tenantId.ToString("D"),
                    storeId.ToString("D"),
                    "cust-1",
                    42.5m,
                    "USD",
                    [new OrderPlacedLineV1("var-1", "wh-1", 2)],
                    at));
            await harness.InactivityTask;

            await harness.Bus.Publish(new StockReservedV1(orderId, tenantId.ToString("D"), storeId.ToString("D"), at));
            await harness.InactivityTask;

            await harness.Bus.Publish(new PaymentTimeoutV1(orderId));
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Cancelled, TimeSpan.FromSeconds(5)));

            await harness.Bus.Publish(
                new PaymentCompletedV1(orderId, "ch_1", tenantId.ToString("D"), storeId.ToString("D"), at));
            await harness.InactivityTask;
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(
                    correlationId,
                    sagaHarness.StateMachine.LatePaymentRefunding,
                    TimeSpan.FromSeconds(5)));

            await harness.Bus.Publish(
                new PaymentRefundedV1(
                    orderId,
                    RefundId: "re_test",
                    Amount: 42.5m,
                    tenantId.ToString("D"),
                    storeId.ToString("D"),
                    DateTimeOffset.UtcNow));
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(
                    correlationId,
                    sagaHarness.StateMachine.LatePaymentRefunded,
                    TimeSpan.FromSeconds(5)));
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task After_PaymentTimeout_PaymentCompleted_on_Cancelled_publishes_RefundPayment_saga_only()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(ConfigureOrderSagaHarness)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var sagaHarness = harness.GetSagaStateMachineHarness<OrderSaga, OrderSagaState>();
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);
            var at = DateTimeOffset.UtcNow;

            await harness.Bus.Publish(Placed(orderId));
            await harness.InactivityTask;
            await harness.Bus.Publish(new StockReservedV1(orderId, "tenant-1", "store-1", at));
            await harness.InactivityTask;
            await harness.Bus.Publish(new PaymentTimeoutV1(orderId));
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Cancelled, TimeSpan.FromSeconds(5)));

            await harness.Bus.Publish(new PaymentCompletedV1(orderId, "ch_1", "tenant-1", "store-1", at));
            await harness.InactivityTask;

            var inLateRefund = await sagaHarness.Exists(
                correlationId,
                sagaHarness.StateMachine.LatePaymentRefunding,
                TimeSpan.FromSeconds(5));
            Assert.True(
                inLateRefund is not null
                    || await harness.Published.Any<RefundPaymentV1>()
                    || await harness.Sent.Any<RefundPaymentV1>(),
                "Saga should enter LatePaymentRefunding or emit RefundPaymentV1 after late PaymentCompleted.");
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task PaymentTimeout_publishes_ReleaseStock_and_enters_Cancelled()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(ConfigureOrderSagaHarness)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var sagaHarness = harness.GetSagaStateMachineHarness<OrderSaga, OrderSagaState>();
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);
            var at = DateTimeOffset.UtcNow;

            await harness.Bus.Publish(Placed(orderId));
            await harness.InactivityTask;
            await harness.Bus.Publish(new StockReservedV1(orderId, "tenant-1", "store-1", at));
            await harness.InactivityTask;

            Assert.NotNull(await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.PaymentPending, TimeSpan.FromSeconds(5)));

            await harness.Bus.Publish(new PaymentTimeoutV1(orderId));
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Cancelled, TimeSpan.FromSeconds(5)));
            Assert.True(
                await harness.Sent.Any<ReleaseStockV1>() || await harness.Published.Any<ReleaseStockV1>());
            Assert.True(
                await harness.Sent.Any<OrderCancelledV1>() || await harness.Published.Any<OrderCancelledV1>());
            Assert.Contains(
                harness.Published.Select<OrderCancelledV1>().Select(x => x.Context.Message.Reason),
                r => r == "payment_timeout");
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task OrderCustomerCancellation_in_Placed_enters_Cancelled_without_ReleaseStock()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(ConfigureOrderSagaHarness)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var sagaHarness = harness.GetSagaStateMachineHarness<OrderSaga, OrderSagaState>();
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);
            var at = DateTimeOffset.UtcNow;

            await harness.Bus.Publish(Placed(orderId));
            await harness.InactivityTask;

            await harness.Bus.Publish(
                new OrderCustomerCancellationV1(orderId, "tenant-1", "store-1", "customer_request", at));
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Cancelled, TimeSpan.FromSeconds(5)));
            Assert.False(
                await harness.Sent.Any<ReleaseStockV1>() || await harness.Published.Any<ReleaseStockV1>());
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task OrderCustomerCancellation_in_PaymentPending_publishes_ReleaseStock_and_enters_Cancelled()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(ConfigureOrderSagaHarness)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var sagaHarness = harness.GetSagaStateMachineHarness<OrderSaga, OrderSagaState>();
            var orderId = Guid.NewGuid().ToString();
            var correlationId = Guid.Parse(orderId);
            var at = DateTimeOffset.UtcNow;

            await harness.Bus.Publish(Placed(orderId));
            await harness.InactivityTask;
            await harness.Bus.Publish(new StockReservedV1(orderId, "tenant-1", "store-1", at));
            await harness.InactivityTask;

            await harness.Bus.Publish(
                new OrderCustomerCancellationV1(orderId, "tenant-1", "store-1", "customer_request", at));
            await harness.InactivityTask;

            Assert.NotNull(
                await sagaHarness.Exists(correlationId, sagaHarness.StateMachine.Cancelled, TimeSpan.FromSeconds(5)));
            Assert.True(
                await harness.Sent.Any<ReleaseStockV1>() || await harness.Published.Any<ReleaseStockV1>());
        }
        finally
        {
            await harness.Stop();
        }
    }
}
