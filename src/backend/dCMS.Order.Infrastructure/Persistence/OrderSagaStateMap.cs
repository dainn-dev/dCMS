using System.Text.Json;
using dCMS.Core.Messaging;
using dCMS.Order.Infrastructure.Sagas;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace dCMS.Order.Infrastructure.Persistence;

public sealed class OrderSagaStateMap : SagaClassMap<OrderSagaState>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected override void Configure(EntityTypeBuilder<OrderSagaState> entity, ModelBuilder model)
    {
        entity.ToTable("OrderSagaState");
        entity.Property(x => x.CurrentState).HasMaxLength(128);
        entity.Property(x => x.OrderId).HasMaxLength(64).IsRequired();
        entity.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
        entity.Property(x => x.StoreId).HasMaxLength(64).IsRequired();
        entity.Property(x => x.CustomerId).HasMaxLength(64).IsRequired();
        entity.Property(x => x.Currency).HasMaxLength(16).IsRequired();
        entity.Property(x => x.TotalAmount).HasPrecision(18, 2);

        entity.Property(x => x.ReserveLines)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<ReserveStockLineV1>>(v, JsonOptions)
                    ?? new List<ReserveStockLineV1>());
    }
}
