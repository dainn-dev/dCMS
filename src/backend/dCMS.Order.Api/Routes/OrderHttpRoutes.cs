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
using Microsoft.Extensions.Logging;
using dCMS.Promotions.Contracts.Evaluate;

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
        app.MapGet("/api/orders/{orderId}/payment", GetOrderPayment)
            .WithName("GetOrderPayment")
            .WithTags("payments")
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

        // DAI-654 Refund case by order (detail + privileged update)
        app.MapGet("/api/orders/{orderId}/refund-case", GetRefundCaseForOrder)
            .WithName("GetRefundCaseForOrder")
            .WithTags("refunds")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapPut("/api/orders/{orderId}/refund-case", PutRefundCaseForOrder)
            .WithName("PutRefundCaseForOrder")
            .WithTags("refunds")
            .RequireAuthorization(DcmsPolicies.OrderFailureManage)
            .WithTenantStoreHeaderAccess(app.Configuration);

        // DAI-694 — admin per-item fulfillment status transition.
        app.MapPatch("/api/orders/{orderId}/items/{lineId}/fulfillment-status", PatchItemFulfillmentStatus)
            .WithName("PatchOrderItemFulfillmentStatus")
            .WithTags("orders")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        // DAI-696 — pickup PIN issuance + confirm.
        app.MapPost("/api/orders/{orderId}/items/{lineId}/issue-pickup-pin", IssuePickupPin)
            .WithName("IssueOrderItemPickupPin")
            .WithTags("orders")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapPost("/api/orders/{orderId}/items/{lineId}/confirm-pickup", ConfirmPickup)
            .WithName("ConfirmOrderItemPickup")
            .WithTags("orders")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        // DAI-637 Failed-order recovery transitions
        app.MapPost("/api/orders/{orderId}/retry-failure", RetryFailure)
            .WithName("RetryOrderFailure")
            .WithTags("orders")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapPost("/api/orders/{orderId}/resolve-failure", ResolveFailure)
            .WithName("ResolveOrderFailure")
            .WithTags("orders")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        // DAI-651 Refund cases (Cancelled + PaymentTransaction) — no RefundCases table.
        app.MapGet("/api/refund-cases", ListRefundCases)
            .WithName("ListRefundCases")
            .WithTags("refunds")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapPatch("/api/refund-cases/{orderId}", PatchRefundCaseLegacy)
            .WithName("PatchRefundCaseLegacy")
            .WithTags("refunds")
            .RequireAuthorization(DcmsPolicies.OrderFailureManage)
            .WithTenantStoreHeaderAccess(app.Configuration);
    }

    private static IResult RefundEnvelopeOk(object data, object? meta = null) =>
        Results.Json(new { data, meta, error = (object?)null }, JsonCamel);

    private static IResult RefundEnvelopeError(int statusCode, string code, string message) =>
        Results.Json(
            new { data = (object?)null, meta = (object?)null, error = new { code, message } },
            JsonCamel,
            statusCode: statusCode);

    private static IResult MissingRefundTenantStore() =>
        RefundEnvelopeError(StatusCodes.Status400BadRequest, "MISSING_TENANT_OR_STORE",
            "X-Tenant-Id and X-Store-Id headers are required.");

    private static async Task<IResult> ListRefundCases(
        HttpContext http,
        [FromServices] IOrderService orders,
        [FromQuery] string? status,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return MissingRefundTenantStore();

        var lim = !limit.HasValue || limit.Value < 1 ? 20 : Math.Clamp(limit.Value, 1, 100);

        try
        {
            var page = await orders
                .ListRefundCasesAsync(tenantId, storeId, status, cursor, lim, cancellationToken)
                .ConfigureAwait(false);

            var items = page.Items.Select(ToRefundCaseListItemDto).ToList();
            return RefundEnvelopeOk(new { items, nextCursor = page.NextCursor });
        }
        catch (ArgumentException ex)
        {
            return RefundEnvelopeError(StatusCodes.Status400BadRequest, "INVALID_QUERY", ex.Message);
        }
    }

    private static Task<IResult> PatchRefundCaseLegacy(
        string orderId,
        UpdateRefundCaseRequest body,
        HttpContext http,
        [FromServices] IOrderService orders,
        CancellationToken cancellationToken) =>
        ApplyRefundCaseUpdate(orderId, body, http, orders, cancellationToken);

    private static Task<IResult> PutRefundCaseForOrder(
        string orderId,
        UpdateRefundCaseRequest body,
        HttpContext http,
        [FromServices] IOrderService orders,
        CancellationToken cancellationToken) =>
        ApplyRefundCaseUpdate(orderId, body, http, orders, cancellationToken);

    private static async Task<IResult> GetRefundCaseForOrder(
        string orderId,
        HttpContext http,
        [FromServices] IOrderService orders,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return MissingRefundTenantStore();

        if (!Guid.TryParse(orderId, out _))
            return RefundEnvelopeError(StatusCodes.Status400BadRequest, "INVALID_ORDER_ID", "orderId must be a UUID.");

        var detail = await orders.GetRefundCaseAsync(orderId, tenantId, storeId, cancellationToken).ConfigureAwait(false);
        if (detail is null)
            return RefundEnvelopeError(StatusCodes.Status404NotFound, "NOT_FOUND", "Refund case not found for this order.");

        return RefundEnvelopeOk(ToRefundCaseListItemDto(detail));
    }

    private static async Task<IResult> ApplyRefundCaseUpdate(
        string orderId,
        UpdateRefundCaseRequest body,
        HttpContext http,
        IOrderService orders,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return MissingRefundTenantStore();

        if (!Guid.TryParse(orderId, out _))
            return RefundEnvelopeError(StatusCodes.Status400BadRequest, "INVALID_ORDER_ID", "orderId must be a UUID.");

        if (string.IsNullOrWhiteSpace(body.Status) || !RefundCaseStatusMaps.IsCanonicalUiStatus(body.Status))
        {
            return RefundEnvelopeError(StatusCodes.Status422UnprocessableEntity, "INVALID_STATUS",
                "status must be one of: Pending, Processing, Success, Rejected.");
        }

        if (RefundCaseStatusMaps.UiPatchToDb(body.Status) is null)
        {
            return RefundEnvelopeError(StatusCodes.Status422UnprocessableEntity, "INVALID_STATUS", "Invalid refund status.");
        }

        var remark = body.Remark ?? "";
        if (remark.Length > 1000)
        {
            return RefundEnvelopeError(StatusCodes.Status400BadRequest, "INVALID_REMARK",
                "remark must be at most 1000 characters.");
        }

        try
        {
            await orders
                .UpdateRefundCaseStatusAsync(orderId, tenantId, storeId, body.Status!, remark, cancellationToken)
                .ConfigureAwait(false);
            return RefundEnvelopeOk(new { orderId, status = body.Status, remark });
        }
        catch (KeyNotFoundException ex)
        {
            return RefundEnvelopeError(StatusCodes.Status422UnprocessableEntity, "ORDER_NOT_ELIGIBLE_FOR_REFUND",
                ex.Message);
        }
        catch (ArgumentException ex)
        {
            return RefundEnvelopeError(StatusCodes.Status400BadRequest, "INVALID_REQUEST", ex.Message);
        }
    }

    private static string BuildRefundNo(Guid orderId) => $"REF-{orderId.ToString("N")[..12].ToUpperInvariant()}";
    private static string BuildReferenceHash(Guid orderId) => $"#{orderId.ToString("N")[..6].ToUpperInvariant()}";

    private static RefundCaseListItemDto ToRefundCaseListItemDto(RefundCaseDetail d)
    {
        var oid = Guid.Parse(d.OrderId);
        var status = RefundCaseStatusMaps.UiToDisplay(d.RefundCaseStatus);
        var requestDate = d.OrderCancelledAt.ToUniversalTime().ToString("yyyy-MM-dd");
        var refundDate = d.RefundedAt?.ToUniversalTime().ToString("yyyy-MM-dd");
        return new RefundCaseListItemDto(
            RefundNo: BuildRefundNo(oid),
            ReferenceHash: BuildReferenceHash(oid),
            OrderId: d.OrderId,
            CustomerName: "",
            CustomerPhone: "",
            CustomerEmail: "",
            Amount: d.Amount,
            Currency: d.Currency,
            PaymentMethod: d.PaymentMethod,
            PaymentReferenceNo: d.PaymentIntentId,
            RequestDate: requestDate,
            RefundDate: refundDate,
            Status: status,
            Remark: d.RefundCaseRemark);
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

    private static async Task<IResult> GetOrderPayment(
        string orderId,
        HttpContext http,
        [FromServices] IOrderService orders,
        [FromServices] OrderPaymentQueryStore payments,
        [FromServices] IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return MissingTenantStore();

        if (!Guid.TryParse(orderId, out var oid))
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

        var view = await payments.GetByOrderIdAsync(oid, cancellationToken).ConfigureAwait(false);
        if (view is null)
        {
            // Back-compat: order might still be single-payment; expose basic gateway intent if present.
            return Results.Json(new
            {
                orderId = timed.Order.Id,
                total = new { amount = timed.Order.Total.Amount, currency = timed.Order.Total.Currency },
                components = timed.Order.PaymentIntentId is null
                    ? Array.Empty<object>()
                    : new object[]
                    {
                        new
                        {
                            type = "Gateway",
                            amount = timed.Order.Total.Amount,
                            externalRef = timed.Order.PaymentIntentId,
                            state = "Unknown"
                        }
                    }
            });
        }

        return Results.Json(new
        {
            orderId = view.OrderId,
            total = new { amount = view.Total, currency = timed.Order.Total.Currency },
            status = view.Status,
            components = view.Components.Select(c => new
            {
                id = c.Id,
                type = c.Type,
                amount = c.Amount,
                externalRef = c.ExternalRef,
                state = c.State,
                lastError = c.LastError,
                ordering = c.Ordering,
                createdAt = c.CreatedAt,
                updatedAt = c.UpdatedAt
            }).ToList()
        });
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
            failureReason = t.Order.FailureReason,
            failureErrorCode = t.Order.FailureErrorCode,
            failedAt = t.Order.FailedAt,
            retryCount = t.Order.RetryCount,
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
            // DAI-649: snapshot — null when order pre-dates the snapshot column.
            customerName = o.CustomerName,
            customerEmail = o.CustomerEmail,
            customerPhone = o.CustomerPhone,
            status = ToApiStatus(o.Status),
            total = new { amount = o.Total.Amount, currency = o.Total.Currency },
            paymentIntentId = o.PaymentIntentId,
            failureReason = o.FailureReason,
            failureErrorCode = o.FailureErrorCode,
            failedAt = o.FailedAt,
            retryCount = o.RetryCount,
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
                lineDiscount = i.LineDiscount,
                productNameSnapshot = i.ProductNameSnapshot,
                variantSnapshot = ParseVariantSnapshotElement(i.VariantSnapshotJson),
                fulfillmentStatus = ToApiItemStatus(i.FulfillmentStatus),
                returnedQuantity = i.ReturnedQuantity,
                pickedUpAt = i.PickedUpAt,
                pickedUpBy = i.PickedUpBy,
            }).ToList(),
            // DAI-725 — promotion snapshot fields
            orderDiscount = o.OrderDiscount,
            promoCode = o.PromoCode,
            promoCodeId = o.PromoCodeId,
            appliedPromotions = o.AppliedPromotions.Select(p => new
            {
                id = p.Id,
                campaignId = p.CampaignId,
                editorKind = p.EditorKind,
                name = p.Name,
                amount = p.Amount,
                promoCode = p.PromoCode,
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
        [FromServices] IPromotionsClient promotions,
        [FromServices] ILoggerFactory loggerFactory,
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

        var promoLogger = loggerFactory.CreateLogger("OrderHttpRoutes.Promotions");
        var (enrichedCommand, promoErr) = await EvaluatePromotionsAsync(
            promotions, configuration, promoLogger, command, body.PromoCode, cancellationToken).ConfigureAwait(false);
        if (promoErr is not null)
            return promoErr;
        command = enrichedCommand;

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
            DateTimeOffset.UtcNow,
            body.CustomerName,
            body.CustomerEmail,
            body.CustomerPhone);

        return true;
    }

    private static string VariantSnapshotJson(JsonElement variantSnapshot) =>
        variantSnapshot.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => "{}",
            JsonValueKind.String => string.IsNullOrEmpty(variantSnapshot.GetString()) ? "{}" : variantSnapshot.GetString()!,
            _ => variantSnapshot.GetRawText(),
        };

    // ── DAI-637 Failed-order recovery handlers ──────────────────────────────

    private static readonly IReadOnlyList<string> FailureStatusNames =
    [
        nameof(OrderStatus.PaymentFailed),
        nameof(OrderStatus.AuthFailed),
        nameof(OrderStatus.AddressError),
        nameof(OrderStatus.StockError),
        nameof(OrderStatus.SystemError),
    ];

    /// <summary>DAI-637 — re-queue a failed order for another attempt (moves to PaymentPending).</summary>
    private static async Task<IResult> RetryFailure(
        string orderId,
        HttpContext http,
        [FromServices] IConfiguration configuration,
        [FromServices] IOrderDetailCache orderDetailCache,
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

        var cs = configuration.GetConnectionString("Order")
                 ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");

        await using var uow = new OrderUnitOfWork(cs);
        await uow.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = await uow.RetryFailureAsync(
                tenantId,
                storeId,
                orderId,
                FailureStatusNames,
                DateTimeOffset.UtcNow,
                cancellationToken).ConfigureAwait(false);

            if (rows == 0)
            {
                await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Results.Json(
                    new { error = new { code = "NOT_FAILED", message = "Order is not in a failure state, or not found." } },
                    statusCode: StatusCodes.Status409Conflict);
            }

            await uow.CommitAsync(cancellationToken).ConfigureAwait(false);
            await orderDetailCache.InvalidateAsync(orderId, cancellationToken).ConfigureAwait(false);

            return Results.Json(new { orderId, status = ToApiStatus(OrderStatus.PaymentPending) });
        }
        catch
        {
            await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>DAI-637 — manually resolve a failed order (treated as cancelled with note).</summary>
    private static async Task<IResult> ResolveFailure(
        string orderId,
        ResolveFailureBody? body,
        HttpContext http,
        [FromServices] IConfiguration configuration,
        [FromServices] IOrderDetailCache orderDetailCache,
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

        var note = body?.Note?.Trim() ?? "";
        var cs = configuration.GetConnectionString("Order")
                 ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");

        await using var uow = new OrderUnitOfWork(cs);
        await uow.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = await uow.TrySetOrderStatusFromAnyAsync(
                tenantId,
                storeId,
                orderId,
                FailureStatusNames,
                nameof(OrderStatus.Cancelled),
                outboxIfUpdated: null,
                DateTimeOffset.UtcNow,
                cancellationToken).ConfigureAwait(false);

            if (rows == 0)
            {
                await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Results.Json(
                    new { error = new { code = "NOT_FAILED", message = "Order is not in a failure state, or not found." } },
                    statusCode: StatusCodes.Status409Conflict);
            }

            await uow.CommitAsync(cancellationToken).ConfigureAwait(false);
            await orderDetailCache.InvalidateAsync(orderId, cancellationToken).ConfigureAwait(false);

            return Results.Json(new { orderId, status = ToApiStatus(OrderStatus.Cancelled), note });
        }
        catch
        {
            await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public sealed record ResolveFailureBody(string? Note);

    public sealed record PatchItemStatusBody(string? Status);

    public sealed record ConfirmPickupBody(string? Pin, string? PickedUpBy);

    /// <summary>DAI-694 — admin transitions a single line's fulfillment status; order status is recomputed in the same transaction.</summary>
    private static async Task<IResult> PatchItemFulfillmentStatus(
        string orderId,
        string lineId,
        PatchItemStatusBody? body,
        HttpContext http,
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
                new { error = new { code = "FORBIDDEN", message = "Only staff can change item fulfillment." } },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!Guid.TryParse(orderId, out _))
        {
            return Results.Json(
                new { error = new { code = "INVALID_ORDER_ID", message = "orderId must be a UUID." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(lineId))
        {
            return Results.Json(
                new { error = new { code = "INVALID_LINE_ID", message = "lineId is required." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (body is null || !TryParseApiItemStatus(body.Status ?? "", out var nextStatus))
        {
            return Results.Json(
                new { error = new { code = "INVALID_STATUS", message = "status is required (one of open, allocated, ready_for_delivery, shipped, delivered, picked_up, returned, cancelled)." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var timed = await orders.GetTimedByIdAsync(tenantId, storeId, orderId, cancellationToken).ConfigureAwait(false);
        if (timed is null)
        {
            return Results.Json(
                new { error = new { code = "NOT_FOUND", message = "Order not found." } },
                statusCode: StatusCodes.Status404NotFound);
        }

        var line = timed.Order.Items.FirstOrDefault(i => string.Equals(i.Id, lineId, StringComparison.Ordinal));
        if (line is null)
        {
            return Results.Json(
                new { error = new { code = "LINE_NOT_FOUND", message = "Line not found on this order." } },
                statusCode: StatusCodes.Status404NotFound);
        }

        if (line.FulfillmentStatus == nextStatus)
        {
            return Results.Json(new
            {
                orderId,
                lineId,
                fulfillmentStatus = ToApiItemStatus(nextStatus),
                orderStatus = ToApiStatus(timed.Order.Status),
            });
        }

        if (!OrderItem.IsValidTransition(line.FulfillmentStatus, nextStatus))
        {
            return Results.Json(
                new
                {
                    error = new
                    {
                        code = "INVALID_TRANSITION",
                        message = $"Cannot transition line from {ToApiItemStatus(line.FulfillmentStatus)} to {ToApiItemStatus(nextStatus)}.",
                    },
                },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var cs = configuration.GetConnectionString("Order")
                 ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");

        await using var uow = new OrderUnitOfWork(cs);
        await uow.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = await uow.UpdateItemFulfillmentStatusAsync(
                tenantId,
                storeId,
                orderId,
                lineId,
                expectedFromStatuses: new[] { line.FulfillmentStatus.ToString() },
                newStatus: nextStatus.ToString(),
                occurredAt: DateTimeOffset.UtcNow,
                cancellationToken).ConfigureAwait(false);

            if (rows == 0)
            {
                await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Results.Json(
                    new { error = new { code = "STALE_STATE", message = "Item state changed concurrently; reload and retry." } },
                    statusCode: StatusCodes.Status409Conflict);
            }

            await uow.RecalculateOrderStatusAsync(tenantId, storeId, orderId, DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);

            await uow.CommitAsync(cancellationToken).ConfigureAwait(false);
            await orderDetailCache.InvalidateAsync(orderId, cancellationToken).ConfigureAwait(false);

            var refreshed = await orders.GetTimedByIdAsync(tenantId, storeId, orderId, cancellationToken).ConfigureAwait(false);
            var orderStatus = refreshed?.Order.Status ?? timed.Order.Status;

            return Results.Json(new
            {
                orderId,
                lineId,
                fulfillmentStatus = ToApiItemStatus(nextStatus),
                orderStatus = ToApiStatus(orderStatus),
            });
        }
        catch
        {
            await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>DAI-696 — issue a fresh pickup PIN. Plaintext PIN is returned exactly once; only the hash is stored.</summary>
    private static async Task<IResult> IssuePickupPin(
        string orderId,
        string lineId,
        HttpContext http,
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
                new { error = new { code = "FORBIDDEN", message = "Only staff can issue pickup PINs." } },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!Guid.TryParse(orderId, out _) || string.IsNullOrWhiteSpace(lineId))
        {
            return Results.Json(
                new { error = new { code = "INVALID_ARGUMENTS", message = "orderId must be a UUID and lineId is required." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var timed = await orders.GetTimedByIdAsync(tenantId, storeId, orderId, cancellationToken).ConfigureAwait(false);
        var line = timed?.Order.Items.FirstOrDefault(i => string.Equals(i.Id, lineId, StringComparison.Ordinal));
        if (timed is null || line is null)
        {
            return Results.Json(
                new { error = new { code = "NOT_FOUND", message = "Order or line not found." } },
                statusCode: StatusCodes.Status404NotFound);
        }

        if (line.FulfillmentStatus != OrderItemFulfillmentStatus.ReadyForDelivery)
        {
            return Results.Json(
                new { error = new { code = "NOT_READY", message = "PIN can only be issued when item is ready_for_delivery." } },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var pin = PickupPinService.GeneratePin();
        var hash = PickupPinService.Hash(pin);

        var cs = configuration.GetConnectionString("Order")
                 ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");

        await using var uow = new OrderUnitOfWork(cs);
        await uow.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = await uow.SetItemPickupPinHashAsync(tenantId, storeId, orderId, lineId, hash, cancellationToken)
                .ConfigureAwait(false);
            if (rows == 0)
            {
                await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Results.Json(
                    new { error = new { code = "INVALID_STATE", message = "Cannot issue pickup PIN for this line in its current state." } },
                    statusCode: StatusCodes.Status409Conflict);
            }

            await uow.CommitAsync(cancellationToken).ConfigureAwait(false);
            await orderDetailCache.InvalidateAsync(orderId, cancellationToken).ConfigureAwait(false);

            return Results.Json(new { orderId, lineId, pin });
        }
        catch
        {
            await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>DAI-696 — verify PIN, transition line to PickedUp, recompute order status.</summary>
    private static async Task<IResult> ConfirmPickup(
        string orderId,
        string lineId,
        ConfirmPickupBody? body,
        HttpContext http,
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
                new { error = new { code = "FORBIDDEN", message = "Only staff can confirm pickup." } },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!Guid.TryParse(orderId, out _) || string.IsNullOrWhiteSpace(lineId))
        {
            return Results.Json(
                new { error = new { code = "INVALID_ARGUMENTS", message = "orderId must be a UUID and lineId is required." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var pin = body?.Pin?.Trim() ?? "";
        if (pin.Length == 0)
        {
            return Results.Json(
                new { error = new { code = "INVALID_PIN", message = "pin is required." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var pickedUpBy = string.IsNullOrWhiteSpace(body?.PickedUpBy)
            ? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            : body!.PickedUpBy!.Trim();

        var cs = configuration.GetConnectionString("Order")
                 ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");

        await using var uow = new OrderUnitOfWork(cs);
        await uow.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var row = await uow.GetItemForPickupAsync(tenantId, storeId, orderId, lineId, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Results.Json(
                    new { error = new { code = "NOT_FOUND", message = "Order or line not found." } },
                    statusCode: StatusCodes.Status404NotFound);
            }

            if (!string.Equals(row.FulfillmentStatus, nameof(OrderItemFulfillmentStatus.ReadyForDelivery), StringComparison.OrdinalIgnoreCase))
            {
                await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Results.Json(
                    new { error = new { code = "NOT_READY", message = "Item is not ready for pickup." } },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            if (!PickupPinService.Verify(pin, row.PickupPinHash))
            {
                await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Results.Json(
                    new { error = new { code = "INVALID_PIN", message = "PIN does not match." } },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var rows = await uow.ConfirmItemPickupAsync(
                tenantId,
                storeId,
                orderId,
                lineId,
                pickedUpBy ?? "",
                DateTimeOffset.UtcNow,
                cancellationToken).ConfigureAwait(false);

            if (rows == 0)
            {
                await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Results.Json(
                    new { error = new { code = "STALE_STATE", message = "Item state changed concurrently; reload and retry." } },
                    statusCode: StatusCodes.Status409Conflict);
            }

            await uow.RecalculateOrderStatusAsync(tenantId, storeId, orderId, DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);

            await uow.CommitAsync(cancellationToken).ConfigureAwait(false);
            await orderDetailCache.InvalidateAsync(orderId, cancellationToken).ConfigureAwait(false);

            var refreshed = await orders.GetTimedByIdAsync(tenantId, storeId, orderId, cancellationToken).ConfigureAwait(false);
            return Results.Json(new
            {
                orderId,
                lineId,
                fulfillmentStatus = ToApiItemStatus(OrderItemFulfillmentStatus.PickedUp),
                orderStatus = ToApiStatus(refreshed?.Order.Status ?? OrderStatus.Processing),
                pickedUpBy,
            });
        }
        catch
        {
            await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    internal static string ToApiStatus(OrderStatus s) =>
        s switch
        {
            OrderStatus.PaymentPending => "payment_pending",
            OrderStatus.Confirmed => "confirmed",
            OrderStatus.Processing => "processing",
            OrderStatus.Shipped => "shipped",
            OrderStatus.Delivered => "delivered",
            OrderStatus.Cancelled => "cancelled",
            // DAI-637 failure states
            OrderStatus.PaymentFailed => "payment_failed",
            OrderStatus.AuthFailed => "auth_failed",
            OrderStatus.AddressError => "address_error",
            OrderStatus.StockError => "stock_error",
            OrderStatus.SystemError => "system_error",
            // DAI-695 item-derived states
            OrderStatus.ReadyForDelivery => "ready_for_delivery",
            OrderStatus.PickedUp => "picked_up",
            OrderStatus.Returned => "returned",
            OrderStatus.PartialFulfilled => "partial_fulfilled",
            _ => s.ToString().ToLowerInvariant(),
        };

    internal static string ToApiItemStatus(OrderItemFulfillmentStatus s) =>
        s switch
        {
            OrderItemFulfillmentStatus.Open => "open",
            OrderItemFulfillmentStatus.Allocated => "allocated",
            OrderItemFulfillmentStatus.ReadyForDelivery => "ready_for_delivery",
            OrderItemFulfillmentStatus.Shipped => "shipped",
            OrderItemFulfillmentStatus.Delivered => "delivered",
            OrderItemFulfillmentStatus.PickedUp => "picked_up",
            OrderItemFulfillmentStatus.Returned => "returned",
            OrderItemFulfillmentStatus.Cancelled => "cancelled",
            _ => s.ToString().ToLowerInvariant(),
        };

    internal static bool TryParseApiItemStatus(string api, out OrderItemFulfillmentStatus status)
    {
        switch ((api ?? "").Trim().ToLowerInvariant())
        {
            case "open": status = OrderItemFulfillmentStatus.Open; return true;
            case "allocated": status = OrderItemFulfillmentStatus.Allocated; return true;
            case "ready_for_delivery": status = OrderItemFulfillmentStatus.ReadyForDelivery; return true;
            case "shipped": status = OrderItemFulfillmentStatus.Shipped; return true;
            case "delivered": status = OrderItemFulfillmentStatus.Delivered; return true;
            case "picked_up": status = OrderItemFulfillmentStatus.PickedUp; return true;
            case "returned": status = OrderItemFulfillmentStatus.Returned; return true;
            case "cancelled": status = OrderItemFulfillmentStatus.Cancelled; return true;
            default: status = default; return false;
        }
    }

    /// <summary>
    /// DAI-693 / DAI-725 — Calls Promotions /evaluate and threads adjustments back into a new
    /// <see cref="CreateOrderCommand"/>. Returns (enrichedCommand, null) on success; (originalCommand, IResult)
    /// when the call fails and Promotions:Required=true (fail-closed) or when a promo code is rejected.
    /// </summary>
    private static async Task<(CreateOrderCommand Command, IResult? Error)> EvaluatePromotionsAsync(
        IPromotionsClient promotions,
        IConfiguration configuration,
        ILogger logger,
        CreateOrderCommand command,
        string? promoCode,
        CancellationToken cancellationToken)
    {
        var currency = command.Lines.FirstOrDefault()?.UnitPrice.Currency ?? "VND";
        var subtotal = command.Lines.Sum(l => l.UnitPrice.Amount * l.Quantity);
        var cartLines = command.Lines.Select(l => new CartLine(
            l.LineId,
            l.ProductId,
            l.VariantId,
            l.ProductId,
            l.Quantity,
            l.UnitPrice.Amount,
            Array.Empty<string>(),
            null)).ToList();

        var trimmedPromoCode = string.IsNullOrWhiteSpace(promoCode) ? null : promoCode!.Trim();
        var request = new EvaluateRequest(
            command.TenantId,
            command.StoreId,
            command.CustomerId,
            trimmedPromoCode,
            currency,
            cartLines,
            subtotal,
            command.IdempotencyKey);

        try
        {
            var response = await promotions.EvaluateAsync(request, cancellationToken).ConfigureAwait(false);
            if (response is null)
            {
                logger.LogInformation("Promotions evaluate returned null for tenant {TenantId} order {OrderId}",
                    command.TenantId, command.OrderId);
                return (command, null);
            }

            if (response.RejectedCode is { } rej)
            {
                logger.LogInformation("Promotions rejected code {Code} reason {Reason}", rej.Code, rej.Reason);
                return (command, Results.Json(
                    new { error = new { code = "PROMO_CODE_REJECTED", message = rej.Reason, promoCode = rej.Code } },
                    statusCode: StatusCodes.Status422UnprocessableEntity));
            }

            var lineDiscountByLineId = response.LineAdjustments
                .GroupBy(a => a.LineId)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));

            var enrichedLines = command.Lines.Select(l =>
                lineDiscountByLineId.TryGetValue(l.LineId, out var disc) && disc > 0m
                    ? l with { LineDiscount = disc }
                    : l).ToList();

            var orderDiscount = response.OrderAdjustments.Sum(a => a.Amount);

            var snapshots = response.AppliedPromotions
                .Select(p => new AppliedPromotionSnapshot(
                    Id: Guid.NewGuid().ToString(),
                    CampaignId: p.CampaignId,
                    EditorKind: p.EditorKind,
                    Name: p.Name,
                    Amount: p.Amount,
                    PromoCode: trimmedPromoCode))
                .ToList();

            var enriched = command with
            {
                Lines = enrichedLines,
                OrderDiscount = orderDiscount,
                PromoCode = trimmedPromoCode,
                PromoCodeId = response.PromoCodeId,
                AppliedPromotions = snapshots,
            };

            if (response.AppliedPromotions.Count > 0)
            {
                OrderPromotionMetrics.OrdersPromotionsApplied
                    .WithLabels(command.TenantId ?? "unknown")
                    .Inc();
                logger.LogInformation(
                    "Promotions applied for order {OrderId}: {Count} promotions, lineAdj={LineAdjCount}, orderAdj={OrderAdjCount}, totalDiscount={Total}",
                    command.OrderId, response.AppliedPromotions.Count, response.LineAdjustments.Count,
                    response.OrderAdjustments.Count, orderDiscount + lineDiscountByLineId.Values.Sum());
            }

            return (enriched, null);
        }
        catch (Exception ex)
        {
            var required = configuration.GetValue<bool>("Promotions:Required");
            var mode = required ? "fail-closed" : "fail-open";
            OrderPromotionMetrics.PromotionsEvaluateFailures
                .WithLabels(command.TenantId ?? "unknown", mode)
                .Inc();

            if (required)
            {
                logger.LogError(ex, "Promotions evaluate failed (fail-closed) for tenant {TenantId} order {OrderId}",
                    command.TenantId, command.OrderId);
                return (command, Results.Json(
                    new { error = new { code = "PROMOTIONS_UNAVAILABLE", message = "Promotions service is unavailable." } },
                    statusCode: StatusCodes.Status503ServiceUnavailable));
            }

            logger.LogWarning(ex, "Promotions evaluate failed (fail-open) for tenant {TenantId} order {OrderId}",
                command.TenantId, command.OrderId);
            return (command, null);
        }
    }
}
