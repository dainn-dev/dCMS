using System.Text.Json;
using Dapper;
using dCMS.Order.Infrastructure.Shipping;
using Npgsql;

namespace dCMS.Order.Infrastructure.Persistence;

/// <summary>Result of <see cref="OrderUnitOfWork.TryMarkOrderCancelledFromApiAsync"/> (DAI-326).</summary>
public enum ManualOrderCancelOutcome
{
    Success,
    NotFound,
    AlreadyCancelled,
    NotCancellable,
}

public enum ManualOrderShipOutcome
{
    Success,
    NotFound,
    AlreadyShipped,
    NotShippable,
}

public enum ManualOrderDeliverOutcome
{
    Success,
    NotFound,
    ShipmentMissing,
    AlreadyDelivered,
    NotDeliverable,
}

/// <summary>Single Postgres transaction for order rows + outbox (DAI-313).</summary>
public sealed class OrderUnitOfWork : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly string _connectionString;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public OrderUnitOfWork(string connectionString) =>
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
            throw new InvalidOperationException("Transaction already started.");

        _connection = new NpgsqlConnection(_connectionString);
        await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        _transaction = await _connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveOrderAsync(Core.Domain.Order order, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();

        if (!Guid.TryParse(order.Id, out var orderGuid))
            throw new ArgumentException("Order id must be a UUID string.", nameof(order));

        var now = DateTimeOffset.UtcNow;
        var shipJson = JsonSerializer.Serialize(order.ShippingAddress, Json);
        var statusName = order.Status.ToString();
        var subTotal = order.Total.Amount;
        const decimal taxTotal = 0m;

        const string insertOrder = """
            INSERT INTO "Orders" (
                "Id", "TenantId", "StoreId", "CustomerId", "Status", "Currency", "SubTotal", "TaxTotal", "Total",
                "PaymentIntentId", "IdempotencyKey", "CreatedAt", "UpdatedAt", "ShippingAddress",
                "FailureReason", "FailureErrorCode", "FailedAt", "RetryCount",
                "CustomerName", "CustomerEmail", "CustomerPhone",
                "OrderDiscount", "PromoCode", "PromoCodeId")
            VALUES (
                @Id, @TenantId, @StoreId, @CustomerId, @Status, @Currency, @SubTotal, @TaxTotal, @Total,
                @PaymentIntentId, @IdempotencyKey, @Now, @Now, @ShippingAddress::jsonb,
                @FailureReason, @FailureErrorCode, @FailedAt, @RetryCount,
                @CustomerName, @CustomerEmail, @CustomerPhone,
                @OrderDiscount, @PromoCode, @PromoCodeId)
            """;

        await conn.ExecuteAsync(new CommandDefinition(insertOrder,
            new
            {
                Id = orderGuid,
                order.TenantId,
                order.StoreId,
                order.CustomerId,
                Status = statusName,
                Currency = order.Total.Currency,
                SubTotal = subTotal,
                TaxTotal = taxTotal,
                Total = order.Total.Amount,
                PaymentIntentId = order.PaymentIntentId,
                IdempotencyKey = idempotencyKey,
                Now = now,
                ShippingAddress = shipJson,
                order.FailureReason,
                order.FailureErrorCode,
                order.FailedAt,
                order.RetryCount,
                order.CustomerName,
                order.CustomerEmail,
                order.CustomerPhone,
                order.OrderDiscount,
                order.PromoCode,
                order.PromoCodeId,
            },
            tx,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        const string insertItem = """
            INSERT INTO "OrderItems" (
                "Id", "OrderId", "VariantId", "ProductId", "Quantity", "UnitPrice", "LineTotal",
                "ProductName", "VariantSnapshot", "FulfillmentStatus", "ReturnedQuantity",
                "PickupPinHash", "PickedUpAt", "PickedUpBy", "LineDiscount")
            VALUES (
                @Id, @OrderId, @VariantId, @ProductId, @Quantity, @UnitPrice, @LineTotal,
                @ProductName::jsonb, @VariantSnapshot::jsonb, @FulfillmentStatus, @ReturnedQuantity,
                @PickupPinHash, @PickedUpAt, @PickedUpBy, @LineDiscount)
            """;

        foreach (var line in order.Items)
        {
            var productNameJson = JsonSerializer.Serialize(line.ProductNameSnapshot, Json);
            var variantJson = string.IsNullOrWhiteSpace(line.VariantSnapshotJson) ? "{}" : line.VariantSnapshotJson;
            await conn.ExecuteAsync(new CommandDefinition(insertItem,
                new
                {
                    line.Id,
                    OrderId = orderGuid,
                    line.VariantId,
                    line.ProductId,
                    line.Quantity,
                    UnitPrice = line.UnitPrice.Amount,
                    LineTotal = line.LineTotal().Amount,
                    ProductName = productNameJson,
                    VariantSnapshot = variantJson,
                    FulfillmentStatus = line.FulfillmentStatus.ToString(),
                    line.ReturnedQuantity,
                    line.PickupPinHash,
                    line.PickedUpAt,
                    line.PickedUpBy,
                    line.LineDiscount,
                },
                tx,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        if (order.AppliedPromotions.Count > 0)
        {
            // DAI-693: single round-trip bulk insert via unnest (avoid N+1).
            const string insertPromos = """
                INSERT INTO "OrderPromotions" (
                    "Id", "TenantId", "OrderId", "CampaignId", "EditorKind", "Name", "Amount", "PromoCode", "AppliedAt")
                SELECT * FROM UNNEST(
                    @Ids::text[],
                    @TenantIds::text[],
                    @OrderIds::text[],
                    @CampaignIds::text[],
                    @EditorKinds::text[],
                    @Names::text[],
                    @Amounts::numeric[],
                    @PromoCodes::text[],
                    @AppliedAts::timestamptz[])
                """;

            var count = order.AppliedPromotions.Count;
            var ids = new string[count];
            var tenantIds = new string[count];
            var orderIds = new string[count];
            var campaignIds = new string[count];
            var editorKinds = new string[count];
            var names = new string[count];
            var amounts = new decimal[count];
            var promoCodes = new string?[count];
            var appliedAts = new DateTimeOffset[count];
            var orderIdStr = order.Id;

            for (var i = 0; i < count; i++)
            {
                var p = order.AppliedPromotions[i];
                ids[i] = p.Id;
                tenantIds[i] = order.TenantId;
                orderIds[i] = orderIdStr;
                campaignIds[i] = p.CampaignId;
                editorKinds[i] = p.EditorKind;
                names[i] = p.Name;
                amounts[i] = p.Amount;
                promoCodes[i] = p.PromoCode;
                appliedAts[i] = now;
            }

            await conn.ExecuteAsync(new CommandDefinition(insertPromos,
                new
                {
                    Ids = ids,
                    TenantIds = tenantIds,
                    OrderIds = orderIds,
                    CampaignIds = campaignIds,
                    EditorKinds = editorKinds,
                    Names = names,
                    Amounts = amounts,
                    PromoCodes = promoCodes,
                    AppliedAts = appliedAts,
                },
                tx,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }

    /// <summary>DAI-694 — admin update of one item's fulfillment status. Returns 0 if line/order not found
    /// or transition is not allowed (server-side guard via expected previous status set).</summary>
    public async Task<int> UpdateItemFulfillmentStatusAsync(
        string tenantId,
        string storeId,
        string orderId,
        string lineId,
        IReadOnlyList<string> expectedFromStatuses,
        string newStatus,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        if (!Guid.TryParse(orderId, out var orderGuid))
            return 0;
        if (string.IsNullOrWhiteSpace(lineId))
            return 0;
        if (expectedFromStatuses is null || expectedFromStatuses.Count == 0)
            return 0;

        const string sql = """
            UPDATE "OrderItems" oi
            SET "FulfillmentStatus" = @NewStatus
            FROM "Orders" o
            WHERE oi."Id" = @LineId
              AND oi."OrderId" = o."Id"
              AND o."Id" = @OrderId
              AND o."TenantId" = @TenantId
              AND o."StoreId" = @StoreId
              AND oi."FulfillmentStatus" = ANY(@Expected)
            """;

        var rows = await conn.ExecuteAsync(new CommandDefinition(sql,
            new
            {
                LineId = lineId,
                OrderId = orderGuid,
                TenantId = tenantId,
                StoreId = storeId,
                Expected = expectedFromStatuses.ToArray(),
                NewStatus = newStatus,
            },
            tx,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (rows > 0)
        {
            const string touch = """
                UPDATE "Orders" SET "UpdatedAt" = @Now
                WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId
                """;
            await conn.ExecuteAsync(new CommandDefinition(touch,
                new { Id = orderGuid, TenantId = tenantId, StoreId = storeId, Now = occurredAt },
                tx, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        return rows;
    }

    /// <summary>DAI-695 — recompute Orders.Status from item states (PartialFulfilled/Delivered/etc.).</summary>
    public async Task<int> RecalculateOrderStatusAsync(
        string tenantId,
        string storeId,
        string orderId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        if (!Guid.TryParse(orderId, out var orderGuid))
            return 0;

        const string currentSql = """
            SELECT "Status" FROM "Orders"
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId
            """;
        var currentStatus = await conn.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
                currentSql, new { Id = orderGuid, TenantId = tenantId, StoreId = storeId },
                tx, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        if (currentStatus is null)
            return 0;

        // Pre-fulfillment statuses are owned by the saga — never overwrite.
        if (currentStatus is "PaymentPending" or "Confirmed"
            or "PaymentFailed" or "AuthFailed" or "AddressError" or "StockError" or "SystemError")
            return 0;

        const string itemsSql = """
            SELECT "FulfillmentStatus" FROM "OrderItems" WHERE "OrderId" = @OrderId
            """;
        var statuses = (await conn.QueryAsync<string>(
                new CommandDefinition(itemsSql, new { OrderId = orderGuid }, tx, cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        if (statuses.Count == 0)
            return 0;

        var derived = DeriveOrderStatusFromItems(statuses);
        if (string.Equals(derived, currentStatus, StringComparison.Ordinal))
            return 0;

        const string updateSql = """
            UPDATE "Orders"
            SET "Status" = @NewStatus, "UpdatedAt" = @Now
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId
            """;
        return await conn.ExecuteAsync(new CommandDefinition(updateSql,
                new { Id = orderGuid, TenantId = tenantId, StoreId = storeId, NewStatus = derived, Now = occurredAt },
                tx, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private static string DeriveOrderStatusFromItems(IReadOnlyList<string> statuses)
    {
        if (statuses.All(s => s == "Cancelled")) return "Cancelled";
        if (statuses.All(s => s == "Returned")) return "Returned";
        if (statuses.All(s => s == "Delivered")) return "Delivered";
        if (statuses.All(s => s == "PickedUp")) return "PickedUp";
        if (statuses.All(s => s == "Shipped")) return "Shipped";
        if (statuses.All(s => s == "ReadyForDelivery")) return "ReadyForDelivery";

        var distinct = statuses.Distinct().Count();
        var hasTerminalProgress = statuses.Any(s =>
            s is "Delivered" or "Shipped" or "PickedUp" or "Returned" or "Cancelled");
        if (hasTerminalProgress && distinct > 1)
            return "PartialFulfilled";
        return "Processing";
    }

    /// <summary>DAI-696 — store hashed pickup PIN (issued when item moves to ReadyForDelivery).</summary>
    public async Task<int> SetItemPickupPinHashAsync(
        string tenantId,
        string storeId,
        string orderId,
        string lineId,
        string pinHash,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        if (!Guid.TryParse(orderId, out var orderGuid))
            return 0;

        const string sql = """
            UPDATE "OrderItems" oi
            SET "PickupPinHash" = @PinHash
            FROM "Orders" o
            WHERE oi."Id" = @LineId AND oi."OrderId" = o."Id"
              AND o."Id" = @OrderId AND o."TenantId" = @TenantId AND o."StoreId" = @StoreId
              AND oi."FulfillmentStatus" IN ('Allocated','ReadyForDelivery')
            """;

        return await conn.ExecuteAsync(new CommandDefinition(sql,
            new { LineId = lineId, OrderId = orderGuid, TenantId = tenantId, StoreId = storeId, PinHash = pinHash },
            tx, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>DAI-696 — confirm pickup: update fulfillment status + audit columns. Caller must have verified the PIN.</summary>
    public async Task<int> ConfirmItemPickupAsync(
        string tenantId,
        string storeId,
        string orderId,
        string lineId,
        string pickedUpBy,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        if (!Guid.TryParse(orderId, out var orderGuid))
            return 0;

        const string sql = """
            UPDATE "OrderItems" oi
            SET "FulfillmentStatus" = 'PickedUp',
                "PickedUpAt" = @Now,
                "PickedUpBy" = @PickedUpBy
            FROM "Orders" o
            WHERE oi."Id" = @LineId AND oi."OrderId" = o."Id"
              AND o."Id" = @OrderId AND o."TenantId" = @TenantId AND o."StoreId" = @StoreId
              AND oi."FulfillmentStatus" = 'ReadyForDelivery'
            """;

        return await conn.ExecuteAsync(new CommandDefinition(sql,
            new
            {
                LineId = lineId,
                OrderId = orderGuid,
                TenantId = tenantId,
                StoreId = storeId,
                PickedUpBy = string.IsNullOrWhiteSpace(pickedUpBy) ? null : pickedUpBy.Trim(),
                Now = occurredAt,
            },
            tx, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>DAI-696 — fetch line + PIN hash for verification.</summary>
    public async Task<PickupItemRow?> GetItemForPickupAsync(
        string tenantId,
        string storeId,
        string orderId,
        string lineId,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        if (!Guid.TryParse(orderId, out var orderGuid))
            return null;

        // DeliveryMethod is read from the variant snapshot (pickup if it has fulfillmentMethod=pickup).
        const string sql = """
            SELECT oi."FulfillmentStatus" AS FulfillmentStatus,
                   oi."PickupPinHash" AS PickupPinHash,
                   COALESCE(oi."VariantSnapshot"->>'fulfillmentMethod','') AS DeliveryMethod
            FROM "OrderItems" oi
            JOIN "Orders" o ON o."Id" = oi."OrderId"
            WHERE oi."Id" = @LineId AND o."Id" = @OrderId
              AND o."TenantId" = @TenantId AND o."StoreId" = @StoreId
            """;

        return await conn.QuerySingleOrDefaultAsync<PickupItemRow>(new CommandDefinition(
            sql,
            new { LineId = lineId, OrderId = orderGuid, TenantId = tenantId, StoreId = storeId },
            tx, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public sealed record PickupItemRow(string FulfillmentStatus, string? PickupPinHash, string DeliveryMethod);

    public async Task AppendOutboxAsync(IReadOnlyList<Core.Domain.IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        const string outSql = """
            INSERT INTO "OutboxEvents" ("EventType", "Payload", "CreatedAt", "RetryCount")
            VALUES (@EventType, @Payload, @CreatedAt, 0)
            """;

        var now = DateTimeOffset.UtcNow;
        foreach (var ev in events)
        {
            var (eventType, payload) = OrderOutboxSerializer.ToOutboxRow(ev);
            await conn.ExecuteAsync(new CommandDefinition(outSql,
                    new { EventType = eventType, Payload = payload, CreatedAt = now },
                    tx,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// US-22 / DAI-332 — create shipment (pending) + append <see cref="Core.Domain.OrderShipped"/> outbox.
    /// Only allowed when order is in a shippable state (e.g. Confirmed/Processing).
    /// </summary>
    public async Task<ManualOrderShipOutcome> TryCreateShipmentPendingFromApiAsync(
        string tenantId,
        string storeId,
        string orderId,
        string carrier,
        string trackingNumber,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        if (!Guid.TryParse(orderId, out var orderGuid))
            return ManualOrderShipOutcome.NotFound;

        // Ensure order exists and is shippable.
        const string statusSql = """
            SELECT "Status" FROM "Orders"
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId
            """;
        var current = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
                statusSql,
                new { Id = orderGuid, TenantId = tenantId, StoreId = storeId },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (current is null)
            return ManualOrderShipOutcome.NotFound;

        if (string.Equals(current, "Shipped", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(current, "Delivered", StringComparison.OrdinalIgnoreCase))
            return ManualOrderShipOutcome.AlreadyShipped;

        if (string.Equals(current, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(current, "PaymentPending", StringComparison.OrdinalIgnoreCase))
            return ManualOrderShipOutcome.NotShippable;

        // Create shipment row (idempotent per order).
        const string insertShipment = """
            INSERT INTO "Shipments" ("OrderId", "Carrier", "TrackingNumber", "Status", "CreatedAt", "UpdatedAt")
            VALUES (@OrderId, @Carrier, @TrackingNumber, @Status, @Now, @Now)
            ON CONFLICT ("OrderId") DO NOTHING
            RETURNING "Id"::uuid
            """;

        var shipmentId = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
                insertShipment,
                new
                {
                    OrderId = orderGuid,
                    Carrier = carrier ?? "",
                    TrackingNumber = trackingNumber ?? "",
                    Status = "pending",
                    Now = occurredAt,
                },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (!shipmentId.HasValue)
        {
            // Shipment already exists; treat as already shipped (API can retry with same idempotency key).
            return ManualOrderShipOutcome.AlreadyShipped;
        }

        const string insertEvent = """
            INSERT INTO "ShipmentEvents" ("ShipmentId", "Status", "Location", "OccurredAt", "Payload")
            VALUES (@ShipmentId, @Status, NULL, @OccurredAt, @Payload::jsonb)
            ON CONFLICT DO NOTHING
            """;
        var payloadJson = JsonSerializer.Serialize(new { carrier, trackingNumber }, Json);
        await conn.ExecuteAsync(new CommandDefinition(
                insertEvent,
                new { ShipmentId = shipmentId.Value, Status = "pending", OccurredAt = occurredAt, Payload = payloadJson },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        // Update order status to Shipped (from any non-terminal shippable state).
        const string shipOrderSql = """
            UPDATE "Orders"
            SET "Status" = 'Shipped', "UpdatedAt" = @Now
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId
              AND "Status" IN ('Confirmed', 'Processing')
            """;
        await conn.ExecuteAsync(new CommandDefinition(
                shipOrderSql,
                new { Id = orderGuid, TenantId = tenantId, StoreId = storeId, Now = occurredAt },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        await AppendOutboxAsync([new Core.Domain.OrderShipped(orderId, occurredAt)], cancellationToken).ConfigureAwait(false);
        return ManualOrderShipOutcome.Success;
    }

    /// <summary>
    /// US-22 / DAI-336 — manual fallback to mark shipment delivered + append <see cref="Core.Domain.OrderDelivered"/> outbox.
    /// </summary>
    public async Task<ManualOrderDeliverOutcome> TryMarkShipmentDeliveredFromApiAsync(
        string tenantId,
        string storeId,
        string orderId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        if (!Guid.TryParse(orderId, out var orderGuid))
            return ManualOrderDeliverOutcome.NotFound;

        const string orderSql = """
            SELECT "Status" FROM "Orders"
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId
            """;
        var current = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
                orderSql,
                new { Id = orderGuid, TenantId = tenantId, StoreId = storeId },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (current is null)
            return ManualOrderDeliverOutcome.NotFound;

        if (string.Equals(current, "Delivered", StringComparison.OrdinalIgnoreCase))
            return ManualOrderDeliverOutcome.AlreadyDelivered;

        if (!string.Equals(current, "Shipped", StringComparison.OrdinalIgnoreCase))
            return ManualOrderDeliverOutcome.NotDeliverable;

        const string shipSql = """
            SELECT "Id"::uuid AS Id, "Status" AS Status
            FROM "Shipments"
            WHERE "OrderId" = @OrderId
            """;
        var ship = await conn.QuerySingleOrDefaultAsync<(Guid Id, string Status)>(
                new CommandDefinition(shipSql, new { OrderId = orderGuid }, tx, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (ship == default || ship.Id == Guid.Empty)
            return ManualOrderDeliverOutcome.ShipmentMissing;

        if (string.Equals(ship.Status, "delivered", StringComparison.OrdinalIgnoreCase))
            return ManualOrderDeliverOutcome.AlreadyDelivered;

        const string updateShipment = """
            UPDATE "Shipments"
            SET "Status" = 'delivered', "DeliveredAt" = @Now, "UpdatedAt" = @Now
            WHERE "Id" = @Id AND "OrderId" = @OrderId AND "Status" <> 'delivered'
            """;
        await conn.ExecuteAsync(new CommandDefinition(
                updateShipment,
                new { Id = ship.Id, OrderId = orderGuid, Now = occurredAt },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        const string insertEvent = """
            INSERT INTO "ShipmentEvents" ("ShipmentId", "Status", "Location", "OccurredAt", "Payload")
            VALUES (@ShipmentId, @Status, NULL, @OccurredAt, @Payload::jsonb)
            ON CONFLICT DO NOTHING
            """;
        var payloadJson = JsonSerializer.Serialize(new { source = "manual_deliver" }, Json);
        await conn.ExecuteAsync(new CommandDefinition(
                insertEvent,
                new { ShipmentId = ship.Id, Status = "delivered", OccurredAt = occurredAt, Payload = payloadJson },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        await TrySetOrderStatusAsync(
                tenantId,
                storeId,
                orderId,
                expectedCurrentStatus: nameof(Core.Domain.OrderStatus.Shipped),
                newStatus: nameof(Core.Domain.OrderStatus.Delivered),
                outboxIfUpdated: [new Core.Domain.OrderDelivered(orderId, occurredAt)],
                occurredAt: occurredAt,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return ManualOrderDeliverOutcome.Success;
    }

    internal async Task<(string TenantId, string StoreId, string OrderId)> GetTenantStoreOrderIdByCarrierTrackingAsync(
        string carrier,
        string trackingNumber,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        const string sql = """
            SELECT o."TenantId" AS TenantId, o."StoreId" AS StoreId, o."Id"::text AS OrderId
            FROM "Shipments" s
            JOIN "Orders" o ON o."Id" = s."OrderId"
            WHERE s."Carrier" = @Carrier AND s."TrackingNumber" = @TrackingNumber
            LIMIT 1
            """;

        var row = await conn.QuerySingleOrDefaultAsync<(string TenantId, string StoreId, string OrderId)>(
                new CommandDefinition(sql, new { Carrier = carrier, TrackingNumber = trackingNumber }, tx,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return row == default ? ("", "", "") : row;
    }

    internal async Task<dCMS.Order.Infrastructure.Shipping.ShipmentWebhookDbOutcome> TryUpsertShipmentEventFromWebhookAsync(
        string carrier,
        string trackingNumber,
        string mappedStatus,
        DateTimeOffset occurredAt,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();

        const string findSql = """
            SELECT "Id"::uuid AS Id
            FROM "Shipments"
            WHERE "Carrier" = @Carrier AND "TrackingNumber" = @TrackingNumber
            LIMIT 1
            """;
        var shipmentId = await conn.QuerySingleOrDefaultAsync<Guid?>(
                new CommandDefinition(findSql, new { Carrier = carrier, TrackingNumber = trackingNumber }, tx,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (!shipmentId.HasValue)
            return dCMS.Order.Infrastructure.Shipping.ShipmentWebhookDbOutcome.NotFound;

        const string insertEvent = """
            INSERT INTO "ShipmentEvents" ("ShipmentId", "Status", "Location", "OccurredAt", "Payload")
            VALUES (@ShipmentId, @Status, NULL, @OccurredAt, @Payload::jsonb)
            ON CONFLICT DO NOTHING
            """;
        await conn.ExecuteAsync(new CommandDefinition(
                insertEvent,
                new { ShipmentId = shipmentId.Value, Status = mappedStatus, OccurredAt = occurredAt, Payload = payloadJson },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        const string updateShipment = """
            UPDATE "Shipments"
            SET "Status" = @Status,
                "DeliveredAt" = CASE WHEN @Status = 'delivered' THEN COALESCE("DeliveredAt", @Now) ELSE "DeliveredAt" END,
                "UpdatedAt" = @Now
            WHERE "Id" = @Id
            """;
        await conn.ExecuteAsync(new CommandDefinition(
                updateShipment,
                new { Id = shipmentId.Value, Status = mappedStatus, Now = occurredAt },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return dCMS.Order.Infrastructure.Shipping.ShipmentWebhookDbOutcome.Ok;
    }

    internal async Task<ShipmentPollingDbOutcome> TryUpsertShipmentEventFromPollingAsync(
        Guid shipmentId,
        string mappedStatus,
        string? location,
        DateTimeOffset occurredAt,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();

        const string getSql = """
            SELECT "Status"
            FROM "Shipments"
            WHERE "Id" = @Id
            """;

        var current = await conn.QuerySingleOrDefaultAsync<string?>(
                new CommandDefinition(getSql, new { Id = shipmentId }, tx, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (current is null)
            return ShipmentPollingDbOutcome.NotFound;

        if (string.Equals(current, mappedStatus, StringComparison.OrdinalIgnoreCase))
            return ShipmentPollingDbOutcome.NoChange;

        const string insertEvent = """
            INSERT INTO "ShipmentEvents" ("ShipmentId", "Status", "Location", "OccurredAt", "Payload")
            VALUES (@ShipmentId, @Status, @Location, @OccurredAt, @Payload::jsonb)
            ON CONFLICT DO NOTHING
            """;
        await conn.ExecuteAsync(new CommandDefinition(
                insertEvent,
                new { ShipmentId = shipmentId, Status = mappedStatus, Location = location, OccurredAt = occurredAt, Payload = payloadJson },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        const string updateShipment = """
            UPDATE "Shipments"
            SET "Status" = @Status,
                "DeliveredAt" = CASE WHEN @Status = 'delivered' THEN COALESCE("DeliveredAt", @Now) ELSE "DeliveredAt" END,
                "UpdatedAt" = @Now
            WHERE "Id" = @Id
            """;
        await conn.ExecuteAsync(new CommandDefinition(
                updateShipment,
                new { Id = shipmentId, Status = mappedStatus, Now = occurredAt },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return ShipmentPollingDbOutcome.Ok;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No active transaction.");
        return _transaction.CommitAsync(cancellationToken);
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        _transaction is null ? Task.CompletedTask : _transaction.RollbackAsync(cancellationToken);

    /// <summary>US-19 — idempotent confirm when saga settles payment (read model + <see cref="Core.Domain.OrderConfirmed"/> outbox).</summary>
    public async Task ConfirmIfPaymentPendingAsync(
        string tenantId,
        string storeId,
        string orderId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        if (!Guid.TryParse(orderId, out var id))
            return;

        const string sql = """
            UPDATE "Orders"
            SET "Status" = 'Confirmed', "UpdatedAt" = @Now
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId AND "Status" = 'PaymentPending'
            """;

        var rows = await conn.ExecuteAsync(new CommandDefinition(sql,
                new { Id = id, TenantId = tenantId, StoreId = storeId, Now = occurredAt },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (rows > 0)
            await AppendOutboxAsync([new Core.Domain.OrderConfirmed(orderId, occurredAt)], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>US-20 / DAI-321 — idempotent cancel when saga publishes <c>OrderCancelledV1</c> while order is still <c>PaymentPending</c>.</summary>
    public async Task CancelIfPaymentPendingAsync(
        string tenantId,
        string storeId,
        string orderId,
        string reason,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        if (!Guid.TryParse(orderId, out var id))
            return;

        const string sql = """
            UPDATE "Orders"
            SET "Status" = 'Cancelled', "UpdatedAt" = @Now
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId AND "Status" = 'PaymentPending'
            """;

        var rows = await conn.ExecuteAsync(new CommandDefinition(sql,
                new { Id = id, TenantId = tenantId, StoreId = storeId, Now = occurredAt },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (rows > 0)
            await AppendOutboxAsync([new Core.Domain.OrderCancelled(orderId, reason, occurredAt)], cancellationToken)
                .ConfigureAwait(false);
    }

    /// <summary>US-21 / DAI-326 — cancel read model + <see cref="Core.Domain.OrderCancelled"/> outbox when status is cancellable.</summary>
    public async Task<ManualOrderCancelOutcome> TryMarkOrderCancelledFromApiAsync(
        string tenantId,
        string storeId,
        string orderId,
        string reason,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        if (!Guid.TryParse(orderId, out var id))
            return ManualOrderCancelOutcome.NotFound;

        const string updateSql = """
            UPDATE "Orders"
            SET "Status" = 'Cancelled', "UpdatedAt" = @Now
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId
              AND "Status" IN ('PaymentPending', 'Confirmed', 'Processing')
            """;

        var rows = await conn.ExecuteAsync(new CommandDefinition(updateSql,
                new { Id = id, TenantId = tenantId, StoreId = storeId, Now = occurredAt },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (rows > 0)
        {
            await AppendOutboxAsync([new Core.Domain.OrderCancelled(orderId, reason, occurredAt)], cancellationToken)
                .ConfigureAwait(false);
            return ManualOrderCancelOutcome.Success;
        }

        const string selectSql = """
            SELECT "Status" FROM "Orders"
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId
            """;

        var current = await conn.QuerySingleOrDefaultAsync<string>(
                new CommandDefinition(selectSql, new { Id = id, TenantId = tenantId, StoreId = storeId },
                    tx,
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (current is null)
            return ManualOrderCancelOutcome.NotFound;

        if (string.Equals(current, nameof(Core.Domain.OrderStatus.Cancelled), StringComparison.OrdinalIgnoreCase))
            return ManualOrderCancelOutcome.AlreadyCancelled;

        return ManualOrderCancelOutcome.NotCancellable;
    }

    /// <summary>US-19 — advance order row when saga reaches fulfillment states; optional outbox for integrations.</summary>
    public async Task TrySetOrderStatusAsync(
        string tenantId,
        string storeId,
        string orderId,
        string expectedCurrentStatus,
        string newStatus,
        IReadOnlyList<Core.Domain.IDomainEvent>? outboxIfUpdated,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        if (!Guid.TryParse(orderId, out var id))
            return;

        const string sql = """
            UPDATE "Orders"
            SET "Status" = @NewStatus, "UpdatedAt" = @Now
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId AND "Status" = @Expected
            """;

        var rows = await conn.ExecuteAsync(new CommandDefinition(sql,
                new
                {
                    Id = id,
                    TenantId = tenantId,
                    StoreId = storeId,
                    Expected = expectedCurrentStatus,
                    NewStatus = newStatus,
                    Now = occurredAt,
                },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (rows > 0 && outboxIfUpdated is { Count: > 0 })
            await AppendOutboxAsync(outboxIfUpdated, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>DAI-637 — transition an order from any of <paramref name="expectedCurrentStatuses"/> to <paramref name="newStatus"/>.</summary>
    public async Task<int> TrySetOrderStatusFromAnyAsync(
        string tenantId,
        string storeId,
        string orderId,
        IReadOnlyList<string> expectedCurrentStatuses,
        string newStatus,
        IReadOnlyList<Core.Domain.IDomainEvent>? outboxIfUpdated,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        if (!Guid.TryParse(orderId, out var id) || expectedCurrentStatuses.Count == 0)
            return 0;

        const string sql = """
            UPDATE "Orders"
            SET "Status" = @NewStatus, "UpdatedAt" = @Now
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId AND "Status" = ANY(@Expected)
            """;

        var rows = await conn.ExecuteAsync(new CommandDefinition(sql,
                new
                {
                    Id = id,
                    TenantId = tenantId,
                    StoreId = storeId,
                    Expected = expectedCurrentStatuses.ToArray(),
                    NewStatus = newStatus,
                    Now = occurredAt,
                },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (rows > 0 && outboxIfUpdated is { Count: > 0 })
            await AppendOutboxAsync(outboxIfUpdated, cancellationToken).ConfigureAwait(false);

        return rows;
    }

    /// <summary>DAI-640: Retry failure transition with audit reset + RetryCount++.</summary>
    public async Task<int> RetryFailureAsync(
        string tenantId,
        string storeId,
        string orderId,
        IReadOnlyList<string> expectedFailureStatuses,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        if (!Guid.TryParse(orderId, out var id) || expectedFailureStatuses.Count == 0)
            return 0;

        const string sql = """
            UPDATE "Orders"
            SET "Status" = @NewStatus,
                "RetryCount" = COALESCE("RetryCount", 0) + 1,
                "FailureReason" = NULL,
                "FailureErrorCode" = NULL,
                "FailedAt" = NULL,
                "UpdatedAt" = @Now
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId AND "Status" = ANY(@Expected)
            """;

        return await conn.ExecuteAsync(new CommandDefinition(sql,
                new
                {
                    Id = id,
                    TenantId = tenantId,
                    StoreId = storeId,
                    Expected = expectedFailureStatuses.ToArray(),
                    NewStatus = nameof(Core.Domain.OrderStatus.PaymentPending),
                    Now = occurredAt,
                },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <summary>DAI-651 — update refund tracking for a cancelled order (no RefundCases table).</summary>
    public async Task<int> UpdateRefundTrackingAsync(
        string tenantId,
        string storeId,
        string orderId,
        string refundStatus,
        string refundRemark,
        DateTimeOffset? refundedAt,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        if (!Guid.TryParse(orderId, out var id))
            return 0;

        var status = (refundStatus ?? "").Trim();
        if (status.Length == 0)
            throw new ArgumentException("refundStatus is required.", nameof(refundStatus));

        var remark = (refundRemark ?? "").Trim();
        if (remark.Length > 1000)
            remark = remark[..1000];

        const string sql = """
            UPDATE "Orders"
            SET "RefundStatus" = @RefundStatus,
                "RefundRemark" = @RefundRemark,
                "RefundedAt" = @RefundedAt,
                "UpdatedAt" = @Now
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId
              AND "Status" IN ('Cancelled', 'AdminCancelled', 'UserCancelled')
            """;

        return await conn.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    Id = id,
                    TenantId = tenantId,
                    StoreId = storeId,
                    RefundStatus = status,
                    RefundRemark = remark,
                    RefundedAt = refundedAt,
                    Now = occurredAt,
                },
                tx,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <summary>DAI-697 — insert a Pending Return and its line items.</summary>
    public async Task<(Guid ReturnId, bool Created)> InsertPendingReturnAsync(
        Guid returnId,
        Guid orderId,
        string tenantId,
        string storeId,
        Guid idempotencyKey,
        string reason,
        string? notes,
        IReadOnlyList<(Guid Id, string OrderItemId, int Quantity, string? Reason)> items,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        if (items is null || items.Count == 0)
            throw new ArgumentException("At least one return item is required.", nameof(items));

        const string insertHeader = """
            INSERT INTO "Returns" ("Id","OrderId","TenantId","StoreId","IdempotencyKey","Status","Reason","Notes","CreatedAt")
            VALUES (@Id, @OrderId, @TenantId, @StoreId, @IdempotencyKey, 'Pending', @Reason, @Notes, @Now)
            ON CONFLICT ("TenantId","StoreId","OrderId","IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL
            DO NOTHING
            RETURNING "Id"::uuid
            """;

        var insertedId = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(insertHeader,
            new
            {
                Id = returnId,
                OrderId = orderId,
                TenantId = tenantId,
                StoreId = storeId,
                IdempotencyKey = idempotencyKey,
                Reason = reason,
                Notes = notes,
                Now = occurredAt,
            },
            tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (!insertedId.HasValue)
        {
            const string getExisting = """
                SELECT "Id"::uuid
                FROM "Returns"
                WHERE "TenantId" = @TenantId
                  AND "StoreId" = @StoreId
                  AND "OrderId" = @OrderId
                  AND "IdempotencyKey" = @IdempotencyKey
                LIMIT 1
                """;
            var existingId = await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(getExisting,
                new { TenantId = tenantId, StoreId = storeId, OrderId = orderId, IdempotencyKey = idempotencyKey },
                tx, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return (existingId, Created: false);
        }

        const string insertItem = """
            INSERT INTO "ReturnItems" ("Id","ReturnId","OrderItemId","Quantity","Reason")
            VALUES (@Id, @ReturnId, @OrderItemId, @Quantity, @Reason)
            """;

        foreach (var (id, orderItemId, qty, itemReason) in items)
        {
            await conn.ExecuteAsync(new CommandDefinition(insertItem,
                new { Id = id, ReturnId = returnId, OrderItemId = orderItemId, Quantity = qty, Reason = itemReason },
                tx, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        await AppendOutboxAsync(
            [new Core.Domain.ReturnStatusChanged(returnId.ToString(), orderId.ToString(), "", "Pending", occurredAt)],
            cancellationToken).ConfigureAwait(false);

        return (returnId, Created: true);
    }

    /// <summary>DAI-697 — transition a Return between expected statuses; emits ReturnStatusChanged + audit fields.</summary>
    public async Task<int> TransitionReturnAsync(
        Guid returnId,
        string tenantId,
        string storeId,
        string expectedStatus,
        string newStatus,
        string? approvedBy,
        Guid? refundCaseId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();

        const string sql = """
            UPDATE "Returns"
            SET "Status" = @NewStatus,
                "ApprovedBy" = COALESCE(@ApprovedBy, "ApprovedBy"),
                "ApprovedAt" = CASE
                    WHEN @NewStatus IN ('Approved','Rejected') AND "ApprovedAt" IS NULL THEN @Now
                    ELSE "ApprovedAt"
                END,
                "CompletedAt" = CASE WHEN @NewStatus = 'Completed' THEN @Now ELSE "CompletedAt" END,
                "RefundCaseId" = COALESCE(@RefundCaseId, "RefundCaseId")
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "StoreId" = @StoreId AND "Status" = @Expected
            RETURNING "OrderId"::uuid
            """;

        var orderId = await conn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(sql,
            new
            {
                Id = returnId, TenantId = tenantId, StoreId = storeId,
                Expected = expectedStatus, NewStatus = newStatus,
                ApprovedBy = approvedBy, RefundCaseId = refundCaseId, Now = occurredAt,
            }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (orderId is null)
            return 0;

        await AppendOutboxAsync(
            [new Core.Domain.ReturnStatusChanged(returnId.ToString(), orderId.Value.ToString(), expectedStatus, newStatus, occurredAt)],
            cancellationToken).ConfigureAwait(false);

        return 1;
    }

    /// <summary>DAI-697 — increment ReturnedQuantity on a line, flipping FulfillmentStatus to 'Returned' when fully returned.
    /// Emits a ProductRestocked event so Catalog/Inventory can restock.</summary>
    public async Task<int> ApplyReturnedQuantityAsync(
        string tenantId,
        string storeId,
        Guid orderId,
        string lineId,
        int quantity,
        Guid returnId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "quantity must be positive.");

        const string sql = """
            UPDATE "OrderItems" oi
            SET "ReturnedQuantity" = oi."ReturnedQuantity" + @Qty,
                "FulfillmentStatus" = CASE
                    WHEN oi."ReturnedQuantity" + @Qty >= oi."Quantity" THEN 'Returned'
                    ELSE oi."FulfillmentStatus"
                END
            FROM "Orders" o
            WHERE oi."Id" = @LineId
              AND oi."OrderId" = o."Id"
              AND o."Id" = @OrderId
              AND o."TenantId" = @TenantId
              AND o."StoreId" = @StoreId
              AND oi."FulfillmentStatus" IN ('Delivered','PickedUp','Returned')
              AND oi."ReturnedQuantity" + @Qty <= oi."Quantity"
            RETURNING oi."VariantId"
            """;

        var variantId = await conn.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(sql,
            new { OrderId = orderId, LineId = lineId, TenantId = tenantId, StoreId = storeId, Qty = quantity },
            tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (variantId is null)
            return 0;

        await AppendOutboxAsync(
            [new Core.Domain.ProductRestocked(orderId.ToString(), tenantId, storeId, variantId, quantity, returnId.ToString(), occurredAt)],
            cancellationToken).ConfigureAwait(false);

        return 1;
    }

    /// <summary>DAI-653 — alias for <see cref="UpdateRefundTrackingAsync"/> (refund-case naming).</summary>
    /// <remarks>Orders and payment transactions may use separate databases; composed reads use <c>OrderQueryStore</c> + <c>PaymentTransactionQueryStore</c>.</remarks>
    public Task<int> UpdateRefundCaseStatusAsync(
        string tenantId,
        string storeId,
        string orderId,
        string refundStatus,
        string refundRemark,
        DateTimeOffset? refundedAt,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default) =>
        UpdateRefundTrackingAsync(
            tenantId,
            storeId,
            orderId,
            refundStatus,
            refundRemark,
            refundedAt,
            occurredAt,
            cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync().ConfigureAwait(false);

        if (_connection is not null)
            await _connection.DisposeAsync().ConfigureAwait(false);

        _transaction = null;
        _connection = null;
    }

    private (NpgsqlConnection Connection, NpgsqlTransaction Transaction) Require()
    {
        if (_connection is null || _transaction is null)
            throw new InvalidOperationException("Call BeginAsync before persistence operations.");

        return (_connection, _transaction);
    }
}
