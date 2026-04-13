using dCMS.Core.Messaging;
using MassTransit;

namespace dCMS.Order.Infrastructure.Sagas;

/// <summary>Persisted saga instance for <see cref="OrderSaga"/> (MassTransit state machine).</summary>
public sealed class OrderSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string? CurrentState { get; set; }
    public string OrderId { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string StoreId { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "";
    public List<ReserveStockLineV1> ReserveLines { get; set; } = [];
}
