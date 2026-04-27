using dCMS.AspNetCore.Auth;
using dCMS.Core.Messaging;
using dCMS.Voucher.Api.Persistence;
using MassTransit;

namespace dCMS.Voucher.Api.Routes;

public static class VoucherRoutes
{
    private static readonly TimeSpan DefaultHoldTtl = TimeSpan.FromMinutes(15);

    public static void MapVoucherRoutes(this WebApplication app)
    {
        var auth = app.Configuration.IsDcmsAuthEnabled();
        var group = app.MapGroup("/api/v1/tenants/{tenantId}/vouchers")
            .WithTags("vouchers")
            .WithTenantAccess(app.Configuration);

        var holdsGroup = app.MapGroup("/api/v1/tenants/{tenantId}/voucher-holds")
            .WithTags("voucher-holds")
            .WithTenantAccess(app.Configuration);

        if (auth)
        {
            group.RequireAuthorization();
            holdsGroup.RequireAuthorization();
        }

        group.MapGet("{code}/balance", GetBalance);
        group.MapPost("{code}/reserve", Reserve);

        holdsGroup.MapPost("{holdId:guid}/capture", Capture);
        holdsGroup.MapPost("{holdId:guid}/release", Release);
        holdsGroup.MapPost("{holdId:guid}/refund", Refund);
    }

    private static async Task<IResult> GetBalance(
        string tenantId,
        string code,
        IVoucherStore store,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("invalid_request", "code is required.");

        var view = await store.GetBalanceAsync(tenantId, code.Trim(), ct);
        if (view is null)
            return NotFound("not_found", "Voucher not found.");
        return Ok(view);
    }

    private static async Task<IResult> Reserve(
        string tenantId,
        string code,
        ReserveRequest req,
        IVoucherStore store,
        IPublishEndpoint publish,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || req.OrderId == Guid.Empty || req.Amount <= 0m)
            return BadRequest("invalid_request", "code, orderId, amount(>0) are required.");

        var ttl = req.TtlSeconds is > 0 ? TimeSpan.FromSeconds(req.TtlSeconds.Value) : DefaultHoldTtl;
        var result = await store.ReserveAsync(tenantId, code.Trim(), req.OrderId, req.Amount, ttl, ct);

        if (!result.Success)
            return MapError(result.ErrorCode!, result.ErrorMessage!);

        var hold = result.Hold!;
        await publish.Publish(new VoucherReservedV1(
            tenantId, req.OrderId, hold.Id, code.Trim(), hold.Amount, hold.ExpiresAt, DateTimeOffset.UtcNow), ct);

        return Ok(MapHold(hold, code.Trim()));
    }

    private static async Task<IResult> Capture(
        string tenantId,
        Guid holdId,
        IVoucherStore store,
        IPublishEndpoint publish,
        CancellationToken ct)
    {
        var result = await store.CaptureAsync(tenantId, holdId, ct);
        if (!result.Success)
            return MapError(result.ErrorCode!, result.ErrorMessage!);

        var hold = result.Hold!;
        await publish.Publish(new VoucherCapturedV1(
            tenantId, hold.OrderId, hold.Id, hold.VoucherId, hold.Amount, DateTimeOffset.UtcNow), ct);
        return Ok(MapHold(hold, null));
    }

    private static async Task<IResult> Release(
        string tenantId,
        Guid holdId,
        ReleaseRequest req,
        IVoucherStore store,
        IPublishEndpoint publish,
        CancellationToken ct)
    {
        var reason = string.IsNullOrWhiteSpace(req.Reason) ? "released" : req.Reason.Trim();
        var result = await store.ReleaseAsync(tenantId, holdId, reason, ct);
        if (!result.Success)
            return MapError(result.ErrorCode!, result.ErrorMessage!);

        var hold = result.Hold!;
        await publish.Publish(new VoucherReleasedV1(
            tenantId, hold.OrderId, hold.Id, hold.VoucherId, hold.Amount, reason, DateTimeOffset.UtcNow), ct);
        return Ok(MapHold(hold, null));
    }

    private static async Task<IResult> Refund(
        string tenantId,
        Guid holdId,
        IVoucherStore store,
        IPublishEndpoint publish,
        CancellationToken ct)
    {
        var result = await store.RefundAsync(tenantId, holdId, ct);
        if (!result.Success)
            return MapError(result.ErrorCode!, result.ErrorMessage!);

        var hold = result.Hold!;
        await publish.Publish(new VoucherRefundedV1(
            tenantId, hold.OrderId, hold.Id, hold.VoucherId, hold.Amount, DateTimeOffset.UtcNow), ct);
        return Ok(MapHold(hold, null));
    }

    private static object MapHold(dCMS.Voucher.Api.Domain.VoucherHoldRow hold, string? code) => new
    {
        holdId = hold.Id,
        voucherId = hold.VoucherId,
        code,
        orderId = hold.OrderId,
        amount = hold.Amount,
        status = hold.Status,
        expiresAt = hold.ExpiresAt,
        createdAt = hold.CreatedAt,
        updatedAt = hold.UpdatedAt,
    };

    private static IResult MapError(string code, string message) => code switch
    {
        "not_found" => NotFound(code, message),
        "invalid_state" or "voucher_inactive" or "voucher_expired" or "hold_expired"
            or "insufficient_balance" or "invalid_amount" => BadRequest(code, message),
        _ => BadRequest(code, message),
    };

    private static IResult Ok(object data) =>
        Results.Json(new { data, meta = (object?)null, error = (object?)null });

    private static IResult BadRequest(string code, string message) =>
        Results.BadRequest(new { data = (object?)null, meta = (object?)null, error = new { code, message } });

    private static IResult NotFound(string code, string message) =>
        Results.Json(new { data = (object?)null, meta = (object?)null, error = new { code, message } }, statusCode: 404);

    public sealed record ReserveRequest(Guid OrderId, decimal Amount, int? TtlSeconds);
    public sealed record ReleaseRequest(string? Reason);
}
