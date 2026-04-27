using dCMS.AspNetCore.Auth;
using dCMS.Order.Api.Security;
using dCMS.Order.Core.Domain;
using dCMS.Order.Core.Ordering;
using dCMS.Order.Infrastructure.Caching;
using dCMS.Order.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace dCMS.Order.Api.Routes;

/// <summary>DAI-697 — RMA / Returns endpoints. Pending → Approved/Rejected; Approved → Completed (with optional refund linkage).</summary>
public static class OrderReturnRoutes
{
    public static void MapOrderReturnRoutes(this WebApplication app)
    {
        app.MapPost("/api/orders/{orderId}/returns", CreateReturn)
            .WithName("CreateOrderReturn")
            .WithTags("returns")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapGet("/api/orders/{orderId}/returns", ListReturnsForOrder)
            .WithName("ListOrderReturns")
            .WithTags("returns")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapGet("/api/returns", ListReturns)
            .WithName("ListReturns")
            .WithTags("returns")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapGet("/api/returns/{returnId}", GetReturn)
            .WithName("GetReturn")
            .WithTags("returns")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapPost("/api/returns/{returnId}/approve", ApproveReturn)
            .WithName("ApproveReturn")
            .WithTags("returns")
            .RequireAuthorization(DcmsPolicies.OrderFailureManage)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapPost("/api/returns/{returnId}/reject", RejectReturn)
            .WithName("RejectReturn")
            .WithTags("returns")
            .RequireAuthorization(DcmsPolicies.OrderFailureManage)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapPost("/api/returns/{returnId}/complete", CompleteReturn)
            .WithName("CompleteReturn")
            .WithTags("returns")
            .RequireAuthorization(DcmsPolicies.OrderFailureManage)
            .WithTenantStoreHeaderAccess(app.Configuration);
    }

    public sealed record CreateReturnLineBody(string LineId, int Quantity, string? Reason);
    public sealed record CreateReturnBody(string Reason, string? Notes, IReadOnlyList<CreateReturnLineBody>? Lines);
    public sealed record ApproveReturnBody(string? ApprovedBy);
    public sealed record CompleteReturnBody(string? RefundCaseId);

    private static IResult Error(int status, string code, string message) =>
        Results.Json(new { error = new { code, message } }, statusCode: status);

    private static bool TryTenantStore(HttpContext http, out string tenantId, out string storeId)
    {
        tenantId = http.Request.Headers["X-Tenant-Id"].FirstOrDefault()?.Trim() ?? "";
        storeId = http.Request.Headers["X-Store-Id"].FirstOrDefault()?.Trim() ?? "";
        return !string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(storeId);
    }

    private static async Task<IResult> CreateReturn(
        string orderId,
        CreateReturnBody? body,
        HttpContext http,
        [FromServices] IConfiguration configuration,
        [FromServices] IOrderService orders,
        CancellationToken cancellationToken)
    {
        if (!TryTenantStore(http, out var tenantId, out var storeId))
            return Error(StatusCodes.Status400BadRequest, "MISSING_TENANT_OR_STORE", "X-Tenant-Id and X-Store-Id headers are required.");

        var idem = http.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim() ?? "";
        if (!Guid.TryParse(idem, out var idempotencyKey))
            return Error(StatusCodes.Status400BadRequest, "INVALID_IDEMPOTENCY_KEY", "Idempotency-Key header must be a UUID.");

        if (!Guid.TryParse(orderId, out var orderGuid))
            return Error(StatusCodes.Status400BadRequest, "INVALID_ORDER_ID", "orderId must be a UUID.");

        if (body is null || string.IsNullOrWhiteSpace(body.Reason))
            return Error(StatusCodes.Status400BadRequest, "validation", "reason is required.");

        if (!Enum.TryParse<ReturnReason>(body.Reason, ignoreCase: true, out var reason))
            return Error(StatusCodes.Status400BadRequest, "INVALID_REASON",
                "reason must be one of WrongItem, Defective, NotAsDescribed, ChangedMind, DamagedInTransit, Other.");

        if (body.Lines is null || body.Lines.Count == 0)
            return Error(StatusCodes.Status400BadRequest, "validation", "At least one line is required.");

        var timed = await orders.GetTimedByIdAsync(tenantId, storeId, orderId, cancellationToken).ConfigureAwait(false);
        if (timed is null)
            return Error(StatusCodes.Status404NotFound, "NOT_FOUND", "Order not found.");

        var orderItems = timed.Order.Items.ToDictionary(i => i.Id, i => i, StringComparer.Ordinal);
        var rows = new List<(Guid Id, string OrderItemId, int Quantity, string? Reason)>();

        foreach (var line in body.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.LineId) || line.Quantity <= 0)
                return Error(StatusCodes.Status400BadRequest, "validation", "Each return line requires a positive quantity and lineId.");
            if (!orderItems.TryGetValue(line.LineId, out var oi))
                return Error(StatusCodes.Status400BadRequest, "LINE_NOT_FOUND", $"Line {line.LineId} not on order.");
            if (oi.FulfillmentStatus is not (OrderItemFulfillmentStatus.Delivered or OrderItemFulfillmentStatus.PickedUp))
                return Error(StatusCodes.Status422UnprocessableEntity, "LINE_NOT_RETURNABLE",
                    $"Line {line.LineId} must be Delivered or PickedUp; current: {oi.FulfillmentStatus}.");
            var remaining = oi.Quantity - oi.ReturnedQuantity;
            if (line.Quantity > remaining)
                return Error(StatusCodes.Status422UnprocessableEntity, "QUANTITY_EXCEEDS_REMAINING",
                    $"Line {line.LineId} only has {remaining} returnable units.");

            string? itemReason = line.Reason;
            if (!string.IsNullOrWhiteSpace(itemReason)
                && !Enum.TryParse<ReturnReason>(itemReason, ignoreCase: true, out _))
            {
                return Error(StatusCodes.Status400BadRequest, "INVALID_REASON",
                    $"Line {line.LineId}: invalid reason {itemReason}.");
            }

            rows.Add((Guid.NewGuid(), line.LineId, line.Quantity, itemReason));
        }

        var returnId = Guid.NewGuid();
        var cs = configuration.GetConnectionString("Order")
                 ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");

        Guid rid = returnId;
        var created = true;
        await using var uow = new OrderUnitOfWork(cs);
        await uow.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (rid, created) = await uow.InsertPendingReturnAsync(
                returnId,
                orderGuid,
                tenantId,
                storeId,
                idempotencyKey,
                reason.ToString(),
                string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim(),
                rows,
                DateTimeOffset.UtcNow,
                cancellationToken).ConfigureAwait(false);

            await uow.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        var statusCode = created ? StatusCodes.Status201Created : StatusCodes.Status200OK;
        return Results.Json(new { returnId = rid.ToString(), orderId, status = "Pending" }, statusCode: statusCode);
    }

    private static async Task<IResult> ListReturnsForOrder(
        string orderId,
        HttpContext http,
        [FromServices] IReturnsRepository returns,
        CancellationToken cancellationToken)
    {
        if (!TryTenantStore(http, out var tenantId, out var storeId))
            return Error(StatusCodes.Status400BadRequest, "MISSING_TENANT_OR_STORE", "Tenant/store headers are required.");
        if (!Guid.TryParse(orderId, out var orderGuid))
            return Error(StatusCodes.Status400BadRequest, "INVALID_ORDER_ID", "orderId must be a UUID.");

        var list = await returns.ListByOrderAsync(tenantId, storeId, orderGuid, cancellationToken).ConfigureAwait(false);
        return Results.Json(new { items = list.Select(ToDto).ToList() });
    }

    private static async Task<IResult> ListReturns(
        HttpContext http,
        [FromServices] IReturnsRepository returns,
        [FromQuery] string? status,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        if (!TryTenantStore(http, out var tenantId, out var storeId))
            return Error(StatusCodes.Status400BadRequest, "MISSING_TENANT_OR_STORE", "Tenant/store headers are required.");

        ReturnStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ReturnStatus>(status, ignoreCase: true, out var s))
                return Error(StatusCodes.Status400BadRequest, "INVALID_STATUS",
                    "status must be Pending, Approved, Rejected, or Completed.");
            parsed = s;
        }

        var lim = !limit.HasValue || limit.Value < 1 ? 20 : Math.Clamp(limit.Value, 1, 100);
        var list = await returns.ListByStatusAsync(tenantId, storeId, parsed, lim, cancellationToken).ConfigureAwait(false);
        return Results.Json(new { items = list.Select(ToDto).ToList() });
    }

    private static async Task<IResult> GetReturn(
        string returnId,
        HttpContext http,
        [FromServices] IReturnsRepository returns,
        CancellationToken cancellationToken)
    {
        if (!TryTenantStore(http, out var tenantId, out var storeId))
            return Error(StatusCodes.Status400BadRequest, "MISSING_TENANT_OR_STORE", "Tenant/store headers are required.");
        if (!Guid.TryParse(returnId, out var rid))
            return Error(StatusCodes.Status400BadRequest, "INVALID_RETURN_ID", "returnId must be a UUID.");

        var rma = await returns.GetByIdAsync(tenantId, storeId, rid, cancellationToken).ConfigureAwait(false);
        if (rma is null)
            return Error(StatusCodes.Status404NotFound, "NOT_FOUND", "Return not found.");

        return Results.Json(ToDto(rma));
    }

    private static async Task<IResult> ApproveReturn(
        string returnId,
        ApproveReturnBody? body,
        HttpContext http,
        [FromServices] IConfiguration configuration,
        [FromServices] IReturnsRepository returns,
        [FromServices] IOrderDetailCache orderDetailCache,
        CancellationToken cancellationToken)
    {
        if (!TryTenantStore(http, out var tenantId, out var storeId))
            return Error(StatusCodes.Status400BadRequest, "MISSING_TENANT_OR_STORE", "Tenant/store headers are required.");
        if (!Guid.TryParse(returnId, out var rid))
            return Error(StatusCodes.Status400BadRequest, "INVALID_RETURN_ID", "returnId must be a UUID.");

        var rma = await returns.GetByIdAsync(tenantId, storeId, rid, cancellationToken).ConfigureAwait(false);
        if (rma is null)
            return Error(StatusCodes.Status404NotFound, "NOT_FOUND", "Return not found.");
        if (rma.Status != ReturnStatus.Pending)
            return Error(StatusCodes.Status409Conflict, "NOT_PENDING", $"Return is {rma.Status}; only Pending can be approved.");

        var approvedBy = body?.ApprovedBy?.Trim();
        if (string.IsNullOrWhiteSpace(approvedBy))
            approvedBy = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var cs = configuration.GetConnectionString("Order")
                 ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");

        await using var uow = new OrderUnitOfWork(cs);
        await uow.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = await uow.TransitionReturnAsync(
                rid, tenantId, storeId,
                expectedStatus: nameof(ReturnStatus.Pending),
                newStatus: nameof(ReturnStatus.Approved),
                approvedBy: approvedBy,
                refundCaseId: null,
                occurredAt: DateTimeOffset.UtcNow,
                cancellationToken).ConfigureAwait(false);

            if (rows == 0)
            {
                await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Error(StatusCodes.Status409Conflict, "STALE_STATE", "Return state changed concurrently.");
            }

            // Apply ReturnedQuantity per line and emit ProductRestocked events.
            if (!Guid.TryParse(rma.OrderId, out var orderGuid))
            {
                await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Error(StatusCodes.Status500InternalServerError, "INVALID_ORDER_ID", "Return references an invalid order id.");
            }

            foreach (var line in rma.Items)
            {
                var applied = await uow.ApplyReturnedQuantityAsync(
                    tenantId, storeId, orderGuid, line.OrderItemId,
                    line.Quantity, rid, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);

                if (applied == 0)
                {
                    await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return Error(StatusCodes.Status422UnprocessableEntity, "LINE_NOT_APPLICABLE",
                        $"Line {line.OrderItemId} cannot accept {line.Quantity} returned units.");
                }
            }

            await uow.RecalculateOrderStatusAsync(tenantId, storeId, rma.OrderId, DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);

            await uow.CommitAsync(cancellationToken).ConfigureAwait(false);
            await orderDetailCache.InvalidateAsync(rma.OrderId, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return Results.Json(new { returnId, status = nameof(ReturnStatus.Approved), approvedBy });
    }

    private static async Task<IResult> RejectReturn(
        string returnId,
        ApproveReturnBody? body,
        HttpContext http,
        [FromServices] IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!TryTenantStore(http, out var tenantId, out var storeId))
            return Error(StatusCodes.Status400BadRequest, "MISSING_TENANT_OR_STORE", "Tenant/store headers are required.");
        if (!Guid.TryParse(returnId, out var rid))
            return Error(StatusCodes.Status400BadRequest, "INVALID_RETURN_ID", "returnId must be a UUID.");

        var approvedBy = body?.ApprovedBy?.Trim();
        if (string.IsNullOrWhiteSpace(approvedBy))
            approvedBy = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var cs = configuration.GetConnectionString("Order")
                 ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");

        await using var uow = new OrderUnitOfWork(cs);
        await uow.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = await uow.TransitionReturnAsync(
                rid, tenantId, storeId,
                expectedStatus: nameof(ReturnStatus.Pending),
                newStatus: nameof(ReturnStatus.Rejected),
                approvedBy: approvedBy,
                refundCaseId: null,
                occurredAt: DateTimeOffset.UtcNow,
                cancellationToken).ConfigureAwait(false);

            if (rows == 0)
            {
                await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Error(StatusCodes.Status409Conflict, "STALE_STATE", "Return is not Pending or already moved.");
            }

            await uow.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return Results.Json(new { returnId, status = nameof(ReturnStatus.Rejected), approvedBy });
    }

    private static async Task<IResult> CompleteReturn(
        string returnId,
        CompleteReturnBody? body,
        HttpContext http,
        [FromServices] IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!TryTenantStore(http, out var tenantId, out var storeId))
            return Error(StatusCodes.Status400BadRequest, "MISSING_TENANT_OR_STORE", "Tenant/store headers are required.");
        if (!Guid.TryParse(returnId, out var rid))
            return Error(StatusCodes.Status400BadRequest, "INVALID_RETURN_ID", "returnId must be a UUID.");

        Guid? refundCaseGuid = null;
        if (!string.IsNullOrWhiteSpace(body?.RefundCaseId))
        {
            if (!Guid.TryParse(body.RefundCaseId, out var rcg))
                return Error(StatusCodes.Status400BadRequest, "INVALID_REFUND_CASE_ID", "refundCaseId must be a UUID.");
            refundCaseGuid = rcg;
        }

        var cs = configuration.GetConnectionString("Order")
                 ?? throw new InvalidOperationException("ConnectionStrings:Order is required.");

        await using var uow = new OrderUnitOfWork(cs);
        await uow.BeginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = await uow.TransitionReturnAsync(
                rid, tenantId, storeId,
                expectedStatus: nameof(ReturnStatus.Approved),
                newStatus: nameof(ReturnStatus.Completed),
                approvedBy: null,
                refundCaseId: refundCaseGuid,
                occurredAt: DateTimeOffset.UtcNow,
                cancellationToken).ConfigureAwait(false);

            if (rows == 0)
            {
                await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Error(StatusCodes.Status409Conflict, "STALE_STATE", "Return is not Approved or already moved.");
            }

            await uow.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return Results.Json(new { returnId, status = nameof(ReturnStatus.Completed), refundCaseId = refundCaseGuid?.ToString() });
    }

    private static object ToDto(Return r) => new
    {
        returnId = r.Id,
        orderId = r.OrderId,
        status = r.Status.ToString(),
        reason = r.Reason.ToString(),
        notes = r.Notes,
        refundCaseId = r.RefundCaseId,
        createdAt = r.CreatedAt,
        approvedAt = r.ApprovedAt,
        approvedBy = r.ApprovedBy,
        completedAt = r.CompletedAt,
        items = r.Items.Select(i => new
        {
            id = i.Id,
            orderItemId = i.OrderItemId,
            quantity = i.Quantity,
            reason = i.Reason?.ToString(),
        }).ToList(),
    };
}
