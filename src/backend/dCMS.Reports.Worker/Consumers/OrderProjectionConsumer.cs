using Dapper;
using dCMS.Core.Messaging;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Prometheus;

namespace dCMS.Reports.Worker.Consumers;

/// <summary>
/// DAI-710: projects order lifecycle messages into analytics tables.
/// Read workload is on analytics DB; this consumer may read OLTP for enrichment (order items, category mapping).
/// </summary>
public sealed class OrderProjectionConsumer : IConsumer<OrderPlacedV1>
{
    private static readonly Gauge ProjectionLagSeconds = Metrics.CreateGauge(
        "dcms_reports_projection_lag_seconds",
        "Lag between event OccurredAt and projection processing time.",
        new GaugeConfiguration { LabelNames = ["message"] });

    private readonly string _analyticsCs;
    private readonly string _orderCs;
    private readonly string? _catalogCs;
    private readonly ILogger<OrderProjectionConsumer> _log;

    public OrderProjectionConsumer(IConfiguration configuration, ILogger<OrderProjectionConsumer> log)
    {
        _analyticsCs = configuration.GetConnectionString("Analytics")
            ?? throw new InvalidOperationException("ConnectionStrings:Analytics is required.");
        _orderCs = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
        _catalogCs = configuration.GetConnectionString("Catalog");
        _log = log;
    }

    public async Task Consume(ConsumeContext<OrderPlacedV1> context)
    {
        var m = context.Message;
        var eventId = ResolveEventId(context, m.OrderId, nameof(OrderPlacedV1));
        var at = m.OccurredAt;
        ProjectionLagSeconds.WithLabels(nameof(OrderPlacedV1))
            .Set(Math.Max(0, (DateTimeOffset.UtcNow - at).TotalSeconds));

        await using var analytics = new NpgsqlConnection(_analyticsCs);
        await analytics.OpenAsync(context.CancellationToken).ConfigureAwait(false);
        await using var tx = await analytics.BeginTransactionAsync(context.CancellationToken).ConfigureAwait(false);

        try
        {
            var inserted = await analytics.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO analytics.event_dedup(event_id) VALUES (@EventId)
                    ON CONFLICT (event_id) DO NOTHING
                    """,
                    new { EventId = eventId },
                    tx,
                    cancellationToken: context.CancellationToken)).ConfigureAwait(false);

            if (inserted == 0)
                return; // idempotent re-delivery

            var d = DateOnly.FromDateTime(at.UtcDateTime);

            // Daily order rollup.
            await analytics.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO analytics.orders_daily
                    (tenant_id, store_id, date, orders_count, gross_amount, net_amount, refund_count, refund_amount)
                VALUES
                    (@TenantId, @StoreId, @Date, 1, @Gross, @Net, 0, 0)
                ON CONFLICT (tenant_id, store_id, date)
                DO UPDATE SET
                    orders_count = analytics.orders_daily.orders_count + 1,
                    gross_amount = analytics.orders_daily.gross_amount + EXCLUDED.gross_amount,
                    net_amount   = analytics.orders_daily.net_amount   + EXCLUDED.net_amount
                """,
                new
                {
                    TenantId = m.TenantId,
                    StoreId = m.StoreId,
                    Date = d,
                    Gross = m.TotalAmount,
                    Net = m.TotalAmount,
                }, tx, cancellationToken: context.CancellationToken)).ConfigureAwait(false);

            // Enrich from OLTP order items for product/category rollups.
            var items = await LoadOrderItemsAsync(m.OrderId, context.CancellationToken).ConfigureAwait(false);
            if (items.Count > 0)
            {
                foreach (var g in items.GroupBy(i => i.ProductId, StringComparer.Ordinal))
                {
                    var units = g.Sum(x => x.Quantity);
                    var gross = g.Sum(x => x.LineTotal);
                    await analytics.ExecuteAsync(new CommandDefinition(
                        """
                        INSERT INTO analytics.sales_by_product
                            (tenant_id, store_id, product_id, date, units_sold, gross_amount)
                        VALUES
                            (@TenantId, @StoreId, @ProductId, @Date, @Units, @Gross)
                        ON CONFLICT (tenant_id, store_id, product_id, date)
                        DO UPDATE SET
                            units_sold = analytics.sales_by_product.units_sold + EXCLUDED.units_sold,
                            gross_amount = analytics.sales_by_product.gross_amount + EXCLUDED.gross_amount
                        """,
                        new
                        {
                            TenantId = m.TenantId,
                            StoreId = m.StoreId,
                            ProductId = g.Key,
                            Date = d,
                            Units = units,
                            Gross = gross,
                        }, tx, cancellationToken: context.CancellationToken)).ConfigureAwait(false);

                    var categoryId = await TryResolveCategoryIdAsync(g.Key, context.CancellationToken).ConfigureAwait(false);
                    if (categoryId.HasValue)
                    {
                        await analytics.ExecuteAsync(new CommandDefinition(
                            """
                            INSERT INTO analytics.sales_by_category
                                (tenant_id, store_id, category_id, date, units_sold, gross_amount)
                            VALUES
                                (@TenantId, @StoreId, @CategoryId, @Date, @Units, @Gross)
                            ON CONFLICT (tenant_id, store_id, category_id, date)
                            DO UPDATE SET
                                units_sold = analytics.sales_by_category.units_sold + EXCLUDED.units_sold,
                                gross_amount = analytics.sales_by_category.gross_amount + EXCLUDED.gross_amount
                            """,
                            new
                            {
                                TenantId = m.TenantId,
                                StoreId = m.StoreId,
                                CategoryId = categoryId.Value,
                                Date = d,
                                Units = units,
                                Gross = gross,
                            }, tx, cancellationToken: context.CancellationToken)).ConfigureAwait(false);
                    }
                }
            }

            await tx.CommitAsync(context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(context.CancellationToken).ConfigureAwait(false);
            _log.LogError(ex, "OrderPlaced projection failed for order {OrderId}", m.OrderId);
            throw;
        }
    }

    private async Task<IReadOnlyList<OrderItemRow>> LoadOrderItemsAsync(string orderId, CancellationToken ct)
    {
        if (!Guid.TryParse(orderId, out var oid))
            return Array.Empty<OrderItemRow>();

        await using var conn = new NpgsqlConnection(_orderCs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<OrderItemRow>(
            new CommandDefinition(
                """
                SELECT "ProductId" AS ProductId, "Quantity" AS Quantity, "LineTotal" AS LineTotal
                FROM "OrderItems"
                WHERE "OrderId" = @OrderId
                """,
                new { OrderId = oid },
                cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    private async Task<int?> TryResolveCategoryIdAsync(string productId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_catalogCs))
            return null;

        await using var conn = new NpgsqlConnection(_catalogCs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var cat = await conn.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                """
                SELECT "CategoryId"
                FROM "Products"
                WHERE "Id" = @Id
                """,
                new { Id = productId },
                cancellationToken: ct)).ConfigureAwait(false);
        return cat;
    }

    private static Guid ResolveEventId<T>(ConsumeContext<T> ctx, string orderId, string typeName)
        where T : class
    {
        if (ctx.MessageId.HasValue)
            return ctx.MessageId.Value;
        if (Guid.TryParse(orderId, out var oid))
            return oid;
        // last resort: stable-ish per order/type in this process (not perfect; prefer MessageId from outbox).
        return DeterministicGuid(typeName + ":" + orderId);
    }

    private static Guid DeterministicGuid(string input)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }

    private sealed class OrderItemRow
    {
        public string ProductId { get; init; } = "";
        public int Quantity { get; init; }
        public decimal LineTotal { get; init; }
    }
}

