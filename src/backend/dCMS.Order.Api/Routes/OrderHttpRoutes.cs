using System.Text.Json;
using dCMS.AspNetCore.Auth;
using dCMS.Order.Api.Contracts;
using dCMS.Order.Api.Security;
using dCMS.Order.Core.Domain;
using dCMS.Order.Core.Integration;
using dCMS.Order.Core.Ordering;
using dCMS.Order.Infrastructure.Caching;
using dCMS.Order.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace dCMS.Order.Api.Routes;

public static class OrderHttpRoutes
{
    private static readonly JsonSerializerOptions JsonCamel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static void MapOrderHttpRoutes(this WebApplication app)
    {
        app.MapGet("/api/orders/{orderId}", GetOrderById)
            .WithName("GetOrderById")
            .WithTags("orders")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);
        app.MapGet("/api/orders", ListOrders)
            .WithName("ListOrders")
            .WithTags("orders")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);
        app.MapPost("/api/orders", CreateOrder)
            .WithName("CreateOrder")
            .WithTags("orders")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);
        app.MapPost("/api/orders/{orderId}/cancel", CancelOrder)
            .WithName("CancelOrder")
            .WithTags("orders")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapPost("/api/orders/{orderId}/ship", ShipOrder)
            .WithName("ShipOrder")
            .WithTags("shipments")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapGet("/api/orders/{orderId}/shipment", GetShipment)
            .WithName("GetShipment")
            .WithTags("shipments")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapPost("/api/orders/{orderId}/deliver", DeliverOrder)
            .WithName("DeliverOrder")
            .WithTags("shipments")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);
    }

    private static async Task<IResult> GetOrderById(
        string orderId,
        HttpContext http,
        [FromServices] IOrderService orders,
        [FromServices] IOrderDetailCache orderDetailCache,
        [FromServices] IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return MissingTenantStore();

        if (!Guid.TryParse(orderId, out _))
        {
            return Results.Json(
                new { error = new { code = "INVALID_ORDER_ID", message = "orderId must be a UUID." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var cached = await orderDetailCache.GetDetailJsonAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
            return Results.Text(cached, "application/json; charset=utf-8");

        var timed = await orders.GetTimedByIdAsync(tenantId, storeId, orderId, cancellationToken).ConfigureAwait(false);
        if (timed is null)
        {
            return Results.Json(
                new { error = new { code = "NOT_FOUND", message = "Order not found." } },
                statusCode: StatusCodes.Status404NotFound);
        }

        if (OrderAuthorization.ValidateCustomerOwnsOrder(http, configuration, timed.Order.CustomerId) is { } custErr)
            return custErr;

        var json = JsonSerializer.Serialize(ToOrderDetailJson(timed), JsonCamel);
        await orderDetailCache.SetDetailJsonAsync(orderId, json, cancellationToken).ConfigureAwait(false);
        return Results.Text(json, "application/json; charset=utf-8");
    }

    private static async Task<IResult> ListOrders(
        HttpContext http,
        [FromServices] IOrderService orders,
        [FromServices] IConfiguration configuration,
        [FromQuery] string? customerId,
        [FromQuery] string? status,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return MissingTenantStore();

        var lim = !limit.HasValue || limit.Value < 1 ? 20 : Math.Clamp(limit.Value, 1, 100);
        var effectiveCustomerId = OrderAuthorization.EffectiveListCustomerId(http, configuration, customerId);
        var query = new OrderListQuery(tenantId, storeId, effectiveCustomerId, status, cursor, lim);

        try
        {
            var page = await orders.ListOrdersAsync(query, cancellationToken).ConfigureAwait(false);
            var items = page.Items.Select(ToOrderListItemJson).ToList();
            return Results.Json(new { items, nextCursor = page.NextCursor });
        }
        catch (ArgumentException ex)
        {
            return Results.Json(
                new { error = new { code = "INVALID_QUERY", message = ex.Message } },
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static bool TryGetTenantStore(HttpContext http, out string tenantId, out string storeId)
    {
        tenantId = http.Request.Headers["X-Tenant-Id"].FirstOrDefault()?.Trim() ?? "";
        storeId = http.Request.Headers["X-Store-Id"].FirstOrDefault()?.Trim() ?? "";
        return !string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(storeId);
    }

    private static IResult MissingTenantStore() =>
        Results.Json(
            new
            {
                error = new
                {
                    code = "MISSING_TENANT_OR_STORE",
                    message = "X-Tenant-Id and X-Store-Id headers are required.",
                },
            },
            statusCode: StatusCodes.Status400BadRequest);

    private static object ToOrderListItemJson(TimedOrder t) =>
        new
        {
            orderId = t.Order.Id,
            customerId = t.Order.CustomerId,
            status = ToApiStatus(t.Order.Status),
            totalAmount = t.Order.Total.Amount,
            currency = t.Order.Total.Currency,
            createdAt = t.CreatedAt,
            lineCount = t.Order.Items.Count,
        };

    private static object ToOrderDetailJson(TimedOrder t)
    {
        var o = t.Order;
        var ship = o.ShippingAddress;
        return new
        {
            orderId = o.Id,
            tenantId = o.TenantId,
            storeId = o.StoreId,
            customerId = o.CustomerId,
            status = ToApiStatus(o.Status),
            total = new { amount = o.Total.Amount, currency = o.Total.Currency },
            paymentIntentId = o.PaymentIntentId,
            createdAt = t.CreatedAt,
            shippingAddress = new
            {
                line1 = ship.Line1,
                line2 = ship.Line2,
                city = ship.City,
                region = ship.Region,
                postalCode = ship.PostalCode,
                countryCode = ship.CountryCode,
            },
            shipment = new
            {
                status = (string?)null,
                carrier = (string?)null,
                trackingNumber = (string?)null,
                address = new
                {
                    line1 = ship.Line1,
                    line2 = ship.Line2,
                    city = ship.City,
                    region = ship.Region,
                    postalCode = ship.PostalCode,
                    countryCode = ship.CountryCode,
                },
            },
            lines = o.Items.Select(i => new
            {
                lineId = i.Id,
                productId = i.ProductId,
                variantId = i.VariantId,
                quantity = i.Quantity,
                unitPrice = new { amount = i.UnitPrice.Amount, currency = i.UnitPrice.Currency },
                lineTotal = new { amount = i.LineTotal().Amount, currency = i.UnitPrice.Currency },
                productNameSnapshot = i.ProductNameSnapshot,
                variantSnapshot = ParseVariantSnapshotElement(i.VariantSnapshotJson),
            }).ToList(),
        };
    }

    private static JsonElement ParseVariantSnapshotElement(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        }
        catch (JsonException)
        {
            return JsonSerializer.Deserialize<JsonElement>("{}");
        }
    }

    private static async Task<IResult> CancelOrder(
        string orderId,
        HttpContext http,
        [FromBody] CancelOrderApiRequest? body,
        [FromServices] IOrderService orders,
        [FromServices] IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return MissingTenantStore();

        var idempotencyKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Json(
                new { error = new { code = "MISSING_IDEMPOTENCY_KEY", message = "Idempotency-Key header is required." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!Guid.TryParse(orderId, out _))
        {
            return Results.Json(
                new { error = new { code = "INVALID_ORDER_ID", message = "orderId must be a UUID." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var reason = string.IsNullOrWhiteSpace(body?.Reason) ? "customer_request" : body.Reason.Trim();
        string? callerCustomerId;
        if (configuration.IsDcmsAuthEnabled() && OrderAuthorization.IsCustomerOnly(http.User))
            callerCustomerId = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        else
        {
            callerCustomerId = http.Request.Headers["X-Customer-Id"].FirstOrDefault()?.Trim();
            if (string.IsNullOrWhiteSpace(callerCustomerId))
                callerCustomerId = null;
        }

        var cmd = new CancelOrderCommand(
            tenantId,
            storeId,
            orderId,
            idempotencyKey,
            callerCustomerId,
            reason,
            DateTimeOffset.UtcNow);

        var timedPreview = await orders.GetTimedByIdAsync(tenantId, storeId, orderId, cancellationToken).ConfigureAwait(false);
        if (timedPreview is not null &&
            OrderAuthorization.ValidateCustomerOwnsOrder(http, configuration, timedPreview.Order.CustomerId) is { } prevErr)
            return prevErr;

        var result = await orders.CancelOrderAsync(cmd, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            CancelOrderResult.Ok ok => Results.Json(new { orderId = ok.Order.Id, status = ToApiStatus(ok.Order.Status) }),
            CancelOrderResult.AlreadyCancelled ac =>
                Results.Json(new { orderId = ac.Order.Id, status = ToApiStatus(ac.Order.Status) }),
            CancelOrderResult.NotFound => Results.Json(
                new { error = new { code = "NOT_FOUND", message = "Order not found." } },
                statusCode: StatusCodes.Status404NotFound),
            CancelOrderResult.Forbidden => Results.Json(
                new { error = new { code = "FORBIDDEN", message = "Caller cannot cancel this order." } },
                statusCode: StatusCodes.Status403Forbidden),
            CancelOrderResult.NotCancellable nc => Results.Json(
                new { error = new { code = "NOT_CANCELLABLE", message = nc.Message } },
                statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> CreateOrder(
        HttpContext http,
        [FromBody] CreateOrderApiRequest? body,
        [FromServices] IOrderService orders,
        [FromServices] IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (body is null)
        {
            return Results.Json(
                new { error = new { code = "validation", message = "Request body is required." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var idempotencyKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Json(
                new { error = new { code = "MISSING_IDEMPOTENCY_KEY", message = "Idempotency-Key header is required." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var tenantId = http.Request.Headers["X-Tenant-Id"].FirstOrDefault()?.Trim() ?? "";
        var storeId = http.Request.Headers["X-Store-Id"].FirstOrDefault()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(storeId))
        {
            return Results.Json(
                new
                {
                    error = new
                    {
                        code = "MISSING_TENANT_OR_STORE",
                        message = "X-Tenant-Id and X-Store-Id headers are required.",
                    },
                },
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TryBuildCommand(tenantId, storeId, idempotencyKey, body, out var command, out var errorMessage, out var errorCode))
        {
            return Results.Json(
                new { error = new { code = errorCode, message = errorMessage } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (OrderAuthorization.ValidateCreateOrderCustomer(http, configuration, command.CustomerId) is { } custErr)
            return custErr;

        try
        {
            var result = await orders.CreateOrderAsync(command, cancellationToken).ConfigureAwait(false);
            var statusCode = result.IsIdempotentReplay ? StatusCodes.Status200OK : StatusCodes.Status201Created;
            return Results.Json(
                new
                {
                    orderId = result.Order.Id,
                    status = ToApiStatus(result.Order.Status),
                    paymentUrl = result.PaymentUrl,
                },
                statusCode: statusCode);
        }
        catch (OutOfStockException ex)
        {
            return Results.Json(
                new
                {
                    error = new
                    {
                        code = "OUT_OF_STOCK",
                        message = ex.Message,
                        variantId = ex.VariantId,
                        warehouseId = ex.WarehouseId,
                        requested = ex.Requested,
                        available = ex.Available,
                    },
                },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (PaymentInitException ex)
        {
            return Results.Json(
                new { error = new { code = "PAYMENT_INIT_FAILED", message = ex.Message } },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<IResult> ShipOrder(
        string orderId,
        HttpContext http,
        [FromBody] ShipOrderApiRequest? body,
        [FromServices] IConfiguration configuration,
        [FromServices] IOrderService orders,
        [FromServices] IOrderDetailCache orderDetailCache,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return MissingTenantStore();

        if (configuration.IsDcmsAuthEnabled() && !OrderAuthorization.IsStaff(http.User))
        {
            return Results.Json(
                new { error = new { code = "FORBIDDEN", message = "Only staff can ship orders." } },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var idempotencyKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Json(
                new { error = new { code = "MISSING_IDEMPOTENCY_KEY", message = "Idempotency-Key header is required." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!Guid.TryParse(orderId, out _))
        {
            return Results.Json(
                new { error = new { code = "INVALID_ORDER_ID", message = "orderId must be a UUID." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Carrier) || string.IsNullOrWhiteSpace(body.TrackingNumber))
        {
            return Results.Json(
                new { error = new { code = "validation", message = "carrier and trackingNumber are required." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var timed = await orders.GetTimedByIdAsync(tenantId, storeId, orderId, cancellationToken).ConfigureAwait(false);
        if (timed is null)
        {
            return Results.Json(
                new { error = new { code = "NOT_FOUND", message = "Order not found." } },
                statusCode: StatusCodes.Status404NotFound);
        }

        var cs = configuration.GetConnectionString("Order")
                 ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");

        await using var uow = new OrderUnitOfWork(cs);
        await uow.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var outcome = await uow.TryCreateShipmentPendingFromApiAsync(
                    tenantId,
                    storeId,
                    orderId,
                    body.Carrier.Trim(),
                    body.TrackingNumber.Trim(),
                    DateTimeOffset.UtcNow,
                    cancellationToken)
                .ConfigureAwait(false);

            await uow.CommitAsync(cancellationToken).ConfigureAwait(false);

            await orderDetailCache.InvalidateAsync(orderId, cancellationToken).ConfigureAwait(false);

            return outcome switch
            {
                ManualOrderShipOutcome.Success => Results.Json(new { orderId, status = "pending" }),
                ManualOrderShipOutcome.AlreadyShipped => Results.Json(
                    new { error = new { code = "ALREADY_SHIPPED", message = "Shipment already exists or order already shipped." } },
                    statusCode: StatusCodes.Status409Conflict),
                ManualOrderShipOutcome.NotShippable => Results.Json(
                    new { error = new { code = "NOT_SHIPPABLE", message = "Order is not in a shippable state." } },
                    statusCode: StatusCodes.Status422UnprocessableEntity),
                ManualOrderShipOutcome.NotFound => Results.Json(
                    new { error = new { code = "NOT_FOUND", message = "Order not found." } },
                    statusCode: StatusCodes.Status404NotFound),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        }
        catch
        {
            await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<IResult> GetShipment(
        string orderId,
        HttpContext http,
        [FromServices] IConfiguration configuration,
        [FromServices] IOrderService orders,
        [FromServices] ShipmentQueryStore shipments,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return MissingTenantStore();

        if (!Guid.TryParse(orderId, out _))
        {
            return Results.Json(
                new { error = new { code = "INVALID_ORDER_ID", message = "orderId must be a UUID." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var timed = await orders.GetTimedByIdAsync(tenantId, storeId, orderId, cancellationToken).ConfigureAwait(false);
        if (timed is null)
        {
            return Results.Json(
                new { error = new { code = "NOT_FOUND", message = "Order not found." } },
                statusCode: StatusCodes.Status404NotFound);
        }

        if (OrderAuthorization.ValidateCustomerOwnsOrder(http, configuration, timed.Order.CustomerId) is { } custErr)
            return custErr;

        var ship = await shipments.GetByOrderIdAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (ship is null)
        {
            return Results.Json(
                new { error = new { code = "NOT_FOUND", message = "Shipment not found." } },
                statusCode: StatusCodes.Status404NotFound);
        }

        var events = ship.Events.Select(e =>
            new
            {
                status = e.Status,
                location = e.Location,
                occurredAt = e.OccurredAt,
                payload = ParseVariantSnapshotElement(e.PayloadJson),
            }).ToList();

        return Results.Json(new
        {
            carrier = ship.Carrier,
            trackingNumber = ship.TrackingNumber,
            status = ship.Status,
            events,
        });
    }

    private static async Task<IResult> DeliverOrder(
        string orderId,
        HttpContext http,
        [FromServices] IConfiguration configuration,
        [FromServices] IOrderDetailCache orderDetailCache,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return MissingTenantStore();

        if (configuration.IsDcmsAuthEnabled() && !OrderAuthorization.IsStaff(http.User))
        {
            return Results.Json(
                new { error = new { code = "FORBIDDEN", message = "Only staff can deliver orders." } },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var idempotencyKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Json(
                new { error = new { code = "MISSING_IDEMPOTENCY_KEY", message = "Idempotency-Key header is required." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!Guid.TryParse(orderId, out _))
        {
            return Results.Json(
                new { error = new { code = "INVALID_ORDER_ID", message = "orderId must be a UUID." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var cs = configuration.GetConnectionString("Order")
                 ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");

        await using var uow = new OrderUnitOfWork(cs);
        await uow.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var outcome = await uow.TryMarkShipmentDeliveredFromApiAsync(
                    tenantId,
                    storeId,
                    orderId,
                    DateTimeOffset.UtcNow,
                    cancellationToken)
                .ConfigureAwait(false);

            if (outcome == ManualOrderDeliverOutcome.Success)
                await uow.CommitAsync(cancellationToken).ConfigureAwait(false);
            else
                await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);

            if (outcome == ManualOrderDeliverOutcome.Success)
                await orderDetailCache.InvalidateAsync(orderId, cancellationToken).ConfigureAwait(false);

            return outcome switch
            {
                ManualOrderDeliverOutcome.Success => Results.Json(new { orderId, status = "delivered" }),
                ManualOrderDeliverOutcome.NotFound => Results.Json(
                    new { error = new { code = "NOT_FOUND", message = "Order not found." } },
                    statusCode: StatusCodes.Status404NotFound),
                ManualOrderDeliverOutcome.ShipmentMissing => Results.Json(
                    new { error = new { code = "SHIPMENT_MISSING", message = "Shipment not found." } },
                    statusCode: StatusCodes.Status404NotFound),
                ManualOrderDeliverOutcome.AlreadyDelivered => Results.Json(
                    new { error = new { code = "ALREADY_DELIVERED", message = "Order/shipment is already delivered." } },
                    statusCode: StatusCodes.Status409Conflict),
                ManualOrderDeliverOutcome.NotDeliverable => Results.Json(
                    new { error = new { code = "NOT_DELIVERABLE", message = "Order is not in a deliverable state." } },
                    statusCode: StatusCodes.Status422UnprocessableEntity),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        }
        catch
        {
            await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static bool TryBuildCommand(
        string tenantId,
        string storeId,
        string idempotencyKey,
        CreateOrderApiRequest body,
        out CreateOrderCommand command,
        out string errorMessage,
        out string errorCode)
    {
        errorCode = "validation";
        errorMessage = "";
        command = null!;

        if (string.IsNullOrWhiteSpace(body.CustomerId))
        {
            errorMessage = "customerId is required.";
            return false;
        }

        if (body.Lines is null || body.Lines.Count == 0)
        {
            errorMessage = "At least one line is required.";
            return false;
        }

        if (body.ShippingAddress is null)
        {
            errorMessage = "shippingAddress is required.";
            return false;
        }

        var ship = body.ShippingAddress;
        if (string.IsNullOrWhiteSpace(ship.Line1) || string.IsNullOrWhiteSpace(ship.City) ||
            string.IsNullOrWhiteSpace(ship.Region) || string.IsNullOrWhiteSpace(ship.PostalCode) ||
            string.IsNullOrWhiteSpace(ship.CountryCode))
        {
            errorMessage = "shippingAddress.line1, city, region, postalCode, and countryCode are required.";
            return false;
        }

        var lines = new List<CreateOrderLine>();
        foreach (var line in body.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.ProductId) || string.IsNullOrWhiteSpace(line.VariantId) ||
                string.IsNullOrWhiteSpace(line.WarehouseId) || line.UnitPrice is null)
            {
                errorMessage = "Each line requires productId, variantId, warehouseId, and unitPrice.";
                return false;
            }

            if (line.Quantity <= 0)
            {
                errorMessage = "Each line quantity must be positive.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(line.UnitPrice.Currency))
            {
                errorMessage = "unitPrice.currency is required.";
                return false;
            }

            var lineId = string.IsNullOrWhiteSpace(line.LineId) ? Guid.NewGuid().ToString("N") : line.LineId.Trim();
            var snap = VariantSnapshotJson(line.VariantSnapshot);
            var name = string.IsNullOrWhiteSpace(line.ProductNameSnapshot) ? line.ProductId.Trim() : line.ProductNameSnapshot.Trim();

            lines.Add(new CreateOrderLine(
                lineId,
                line.ProductId.Trim(),
                line.VariantId.Trim(),
                line.WarehouseId.Trim(),
                line.Quantity,
                new Money(line.UnitPrice.Amount, line.UnitPrice.Currency.Trim()),
                name,
                snap));
        }

        var shipping = new ShippingAddress(
            ship.Line1.Trim(),
            string.IsNullOrWhiteSpace(ship.Line2) ? null : ship.Line2.Trim(),
            ship.City.Trim(),
            ship.Region.Trim(),
            ship.PostalCode.Trim(),
            ship.CountryCode.Trim());

        var orderId = Guid.NewGuid().ToString();
        command = new CreateOrderCommand(
            orderId,
            tenantId,
            storeId,
            body.CustomerId.Trim(),
            idempotencyKey,
            lines,
            shipping,
            DateTimeOffset.UtcNow);

        return true;
    }

    private static string VariantSnapshotJson(JsonElement variantSnapshot) =>
        variantSnapshot.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => "{}",
            JsonValueKind.String => string.IsNullOrEmpty(variantSnapshot.GetString()) ? "{}" : variantSnapshot.GetString()!,
            _ => variantSnapshot.GetRawText(),
        };

    private static string ToApiStatus(OrderStatus s) =>
        s switch
        {
            OrderStatus.PaymentPending => "payment_pending",
            OrderStatus.Confirmed => "confirmed",
            OrderStatus.Processing => "processing",
            OrderStatus.Shipped => "shipped",
            OrderStatus.Delivered => "delivered",
            OrderStatus.Cancelled => "cancelled",
            _ => s.ToString().ToLowerInvariant(),
        };
}
