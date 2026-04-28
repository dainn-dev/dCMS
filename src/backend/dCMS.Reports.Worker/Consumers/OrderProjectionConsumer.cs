using System.Text.Json;
using Dapper;
using dCMS.Core.Messaging;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Prometheus;

namespace dCMS.Reports.Worker.Consumers;

/// <summary>
/// DAI-710 / P0 #2: projects order lifecycle messages into analytics tables.
/// Sales-by-product is built from <see cref="OrderPlacedV1.Items"/> (no dcms_order read).
/// Category lookup uses Catalog.Api /internal/catalog/.../category (no dcms_catalog read).
/// </summary>
public sealed class OrderProjectionConsumer : IConsumer<OrderPlacedV1>
{
    private static readonly Gauge ProjectionLagSeconds = Metrics.CreateGauge(
        "dcms_reports_projection_lag_seconds",
        "Lag between event OccurredAt and projection processing time.",
        new GaugeConfiguration { LabelNames = ["message"] });

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly string _analyticsCs;
    private readonly IHttpClientFactory _httpFactory;
    private readonly CatalogClientOptions _catalogOptions;
    private readonly ILogger<OrderProjectionConsumer> _log;

    public OrderProjectionConsumer(
        IConfiguration configuration,
        IHttpClientFactory httpFactory,
        IOptions<CatalogClientOptions> catalogOptions,
        ILogger<OrderProjectionConsumer> log)
    {
        _analyticsCs = configuration.GetConnectionString("Analytics")
            ?? throw new InvalidOperationException("ConnectionStrings:Analytics is required.");
        _httpFactory = httpFactory;
        _catalogOptions = catalogOptions.Value;
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
                    m.TenantId,
                    m.StoreId,
                    Date = d,
                    Gross = m.TotalAmount,
                    Net = m.TotalAmount,
                }, tx, cancellationToken: context.CancellationToken)).ConfigureAwait(false);

            var items = m.Items ?? Array.Empty<OrderPlacedItemV1>();
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
                            m.TenantId,
                            m.StoreId,
                            ProductId = g.Key,
                            Date = d,
                            Units = units,
                            Gross = gross,
                        }, tx, cancellationToken: context.CancellationToken)).ConfigureAwait(false);

                    var categoryId = await TryResolveCategoryIdAsync(m.TenantId, g.Key, context.CancellationToken)
                        .ConfigureAwait(false);
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
                                m.TenantId,
                                m.StoreId,
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

    private async Task<int?> TryResolveCategoryIdAsync(string tenantId, string productId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_catalogOptions.BaseUrl) ||
            string.IsNullOrWhiteSpace(_catalogOptions.InternalApiKey))
            return null;

        try
        {
            var client = _httpFactory.CreateClient(CatalogClientOptions.HttpClientName);
            var baseUrl = _catalogOptions.BaseUrl!.TrimEnd('/');
            var url = $"{baseUrl}/internal/catalog/tenants/{Uri.EscapeDataString(tenantId)}/products/{Uri.EscapeDataString(productId)}/category";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("X-Internal-Api-Key", _catalogOptions.InternalApiKey);
            using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            if (!resp.IsSuccessStatusCode) return null;
            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var env = await JsonSerializer.DeserializeAsync<CategoryEnvelope>(stream, JsonOpts, ct).ConfigureAwait(false);
            return env?.Data?.CategoryId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Category lookup failed for product {ProductId}", productId);
            return null;
        }
    }

    private static Guid ResolveEventId<T>(ConsumeContext<T> ctx, string orderId, string typeName)
        where T : class
    {
        if (ctx.MessageId.HasValue)
            return ctx.MessageId.Value;
        if (Guid.TryParse(orderId, out var oid))
            return oid;
        return DeterministicGuid(typeName + ":" + orderId);
    }

    private static Guid DeterministicGuid(string input)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }

    private sealed class CategoryEnvelope
    {
        public CategoryData? Data { get; set; }
    }

    private sealed class CategoryData
    {
        public string? Id { get; set; }
        public int? CategoryId { get; set; }
    }
}
