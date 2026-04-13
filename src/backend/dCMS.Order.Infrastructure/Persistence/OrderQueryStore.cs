using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Order.Infrastructure.Persistence;

/// <summary>Read-side access (no ambient transaction).</summary>
public sealed class OrderQueryStore
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly string _connectionString;

    public OrderQueryStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Order")
            ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");
    }

    public async Task<Core.Domain.Order?> GetByIdAsync(
        string tenantId,
        string storeId,
        string orderId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(orderId, out var orderGuid))
            return null;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string orderSql = """
            SELECT "Id", "TenantId", "StoreId", "CustomerId", "Status", "Currency", "Total",
                   "ShippingAddress"::text AS ShippingAddressJson,
                   "PaymentIntentId"
            FROM "Orders"
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId
            """;

        var row = await connection.QuerySingleOrDefaultAsync<OrderRow>(
            new CommandDefinition(orderSql, new { Id = orderGuid, TenantId = tenantId, StoreId = storeId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (row is null)
            return null;

        const string itemsSql = """
            SELECT "Id", "VariantId", "ProductId", "Quantity", "UnitPrice", "LineTotal",
                   "ProductName"::text AS ProductNameJson, "VariantSnapshot"::text AS VariantSnapshotJson
            FROM "OrderItems"
            WHERE "OrderId" = @OrderId
            ORDER BY "Id"
            """;

        var itemRows = await connection.QueryAsync<OrderItemRow>(
            new CommandDefinition(itemsSql, new { OrderId = orderGuid }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return MapOrder(row, itemRows);
    }

    public async Task<Core.Domain.Order?> GetByIdempotencyKeyAsync(
        string tenantId,
        string storeId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string orderSql = """
            SELECT "Id", "TenantId", "StoreId", "CustomerId", "Status", "Currency", "Total",
                   "ShippingAddress"::text AS ShippingAddressJson,
                   "PaymentIntentId"
            FROM "Orders"
            WHERE "TenantId" = @TenantId AND "StoreId" = @StoreId AND "IdempotencyKey" = @IdempotencyKey
            """;

        var row = await connection.QuerySingleOrDefaultAsync<OrderRow>(
            new CommandDefinition(orderSql, new { TenantId = tenantId, StoreId = storeId, IdempotencyKey = idempotencyKey },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (row is null)
            return null;

        const string itemsSql = """
            SELECT "Id", "VariantId", "ProductId", "Quantity", "UnitPrice", "LineTotal",
                   "ProductName"::text AS ProductNameJson, "VariantSnapshot"::text AS VariantSnapshotJson
            FROM "OrderItems"
            WHERE "OrderId" = @OrderId
            ORDER BY "Id"
            """;

        var itemRows = await connection.QueryAsync<OrderItemRow>(
            new CommandDefinition(itemsSql, new { OrderId = row.Id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return MapOrder(row, itemRows);
    }

    private static Core.Domain.Order MapOrder(OrderRow row, IEnumerable<OrderItemRow> itemRows)
    {
        var status = Enum.Parse<Core.Domain.OrderStatus>(row.Status, ignoreCase: true);
        var ship = JsonSerializer.Deserialize<Core.Domain.ShippingAddress>(row.ShippingAddressJson, Json)
                   ?? new Core.Domain.ShippingAddress("", null, "", "", "", "");

        var items = itemRows.Select(ir =>
        {
            var productName = JsonSerializer.Deserialize<string>(ir.ProductNameJson, Json)
                              ?? ir.ProductNameJson.Trim('"');
            var variantJson = string.IsNullOrWhiteSpace(ir.VariantSnapshotJson) ? "{}" : ir.VariantSnapshotJson;
            return new Core.Domain.OrderItem(
                ir.Id,
                ir.ProductId,
                ir.VariantId,
                ir.Quantity,
                new Core.Domain.Money(ir.UnitPrice, row.Currency),
                productName,
                variantJson);
        }).ToList();

        return Core.Domain.Order.FromPersistence(
            row.Id.ToString(),
            row.TenantId,
            row.StoreId,
            row.CustomerId,
            status,
            new Core.Domain.Money(row.Total, row.Currency),
            ship,
            items,
            row.PaymentIntentId);
    }

    private sealed record OrderRow(
        Guid Id,
        string TenantId,
        string StoreId,
        string CustomerId,
        string Status,
        string Currency,
        decimal Total,
        string ShippingAddressJson,
        string? PaymentIntentId);

    private sealed record OrderItemRow(
        string Id,
        string VariantId,
        string ProductId,
        int Quantity,
        decimal UnitPrice,
        decimal LineTotal,
        string ProductNameJson,
        string VariantSnapshotJson);
}
