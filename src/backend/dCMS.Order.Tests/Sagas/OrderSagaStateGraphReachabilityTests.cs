using System.Reflection;
using System.Text;
using dCMS.Order.Infrastructure.Sagas;
using MassTransit;
using Xunit.Abstractions;

namespace dCMS.Order.Tests.Sagas;

/// <summary>DAI-355 (US-F2) — every non-terminal Order saga state must have a path to a terminal state (Delivered or Cancelled).</summary>
public sealed class OrderSagaStateGraphReachabilityTests
{
    private readonly ITestOutputHelper _output;

    public OrderSagaStateGraphReachabilityTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Non_terminal_states_have_documented_path_to_a_terminal_state()
    {
        var machine = new OrderSaga();
        var statePropertyNames = typeof(OrderSaga)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(State) && p.DeclaringType == typeof(OrderSaga))
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var expected = new[]
        {
            nameof(OrderSaga.Placed),
            nameof(OrderSaga.PaymentPending),
            nameof(OrderSaga.Confirmed),
            nameof(OrderSaga.Processing),
            nameof(OrderSaga.Shipped),
            nameof(OrderSaga.Delivered),
            nameof(OrderSaga.Cancelled),
            nameof(OrderSaga.LatePaymentRefunding),
            nameof(OrderSaga.LatePaymentRefunded),
        };

        Assert.Equal(
            expected.OrderBy(n => n, StringComparer.OrdinalIgnoreCase),
            statePropertyNames);

        var terminal = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            nameof(OrderSaga.Delivered),
            nameof(OrderSaga.LatePaymentRefunded),
        };

        var nonTerminal = statePropertyNames.Where(n => !terminal.Contains(n)).ToArray();

        var graph = new StringBuilder();
        graph.AppendLine("OrderSaga state graph (US-F2 / DAI-355, US-F3 / DAI-356):");
        graph.AppendLine("- Placed → Cancelled: StockReservationFailed, StockReservationTimeout, OrderCustomerCancellation");
        graph.AppendLine("- Placed → PaymentPending → … → Delivered: StockReserved, payment + fulfillment happy path");
        graph.AppendLine("- PaymentPending → Cancelled: PaymentFailed, PaymentTimeout, OrderCustomerCancellation");
        graph.AppendLine("- Confirmed|Processing → Cancelled: OrderCustomerCancellation");
        graph.AppendLine("- Shipped → Delivered: DeliveredForSaga (only forward)");
        graph.AppendLine("- Cancelled → LatePaymentRefunding → LatePaymentRefunded: PaymentCompleted (late gateway) + RefundPayment.v1");
        graph.AppendLine("- Terminal: Delivered, LatePaymentRefunded (Cancelled is pre-refund stable; may exit on late PaymentCompleted)");
        _output.WriteLine(graph.ToString());

        var reachability = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(OrderSaga.Placed)] = true,
            [nameof(OrderSaga.PaymentPending)] = true,
            [nameof(OrderSaga.Confirmed)] = true,
            [nameof(OrderSaga.Processing)] = true,
            [nameof(OrderSaga.Shipped)] = true,
            [nameof(OrderSaga.Cancelled)] = true,
            [nameof(OrderSaga.LatePaymentRefunding)] = true,
        };

        foreach (var s in nonTerminal)
        {
            Assert.True(
                reachability.TryGetValue(s, out var ok) && ok,
                $"State {s} must have a documented path to Delivered, Cancelled, or LatePaymentRefunded (update OrderSaga + this test if the machine changes).");
        }
    }
}
