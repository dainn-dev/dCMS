using dCMS.AspNetCore.Auth;
using dCMS.Core.Messaging;
using dCMS.Loyalty.Api.Persistence;
using MassTransit;

namespace dCMS.Loyalty.Api.Routes;

public static class LoyaltyRoutes
{
    private static readonly TimeSpan DefaultHoldTtl = TimeSpan.FromMinutes(15);

    public static void MapLoyaltyRoutes(this WebApplication app)
    {
        var auth = app.Configuration.IsDcmsAuthEnabled();

        var balances = app.MapGroup("/api/v1/tenants/{tenantId}/loyalty/customers")
            .WithTags("loyalty")
            .WithTenantAccess(app.Configuration);

        var holds = app.MapGroup("/api/v1/tenants/{tenantId}/loyalty-holds")
            .WithTags("loyalty-holds")
            .WithTenantAccess(app.Configuration);

        if (auth)
        {
            balances.RequireAuthorization();
            holds.RequireAuthorization();
        }

        balances.MapGet("{customerId}/balance", GetBalance);
        balances.MapPost("{customerId}/reserve", Reserve);
        balances.MapPost("{customerId}/ledger", RecordLedger);

        holds.MapPost("{holdId:guid}/capture", Capture);
        holds.MapPost("{holdId:guid}/release", Release);
        holds.MapPost("{holdId:guid}/refund", Refund);
    }

    private static async Task<IResult> GetBalance(
        string tenantId,
        string customerId,
        ILoyaltyStore store,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            return BadRequest("invalid_request", "customerId is required.");
        var view = await store.GetBalanceAsync(tenantId, customerId.Trim(), ct);
        return Ok(view);
    }

    private static async Task<IResult> Reserve(
        string tenantId,
        string customerId,
        ReserveRequest req,
        ILoyaltyStore store,
        IPublishEndpoint publish,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(customerId) || req.OrderId == Guid.Empty || req.Amount <= 0m)
            return BadRequest("invalid_request", "customerId, orderId, amount(>0) are required.");

        var ttl = req.TtlSeconds is > 0 ? TimeSpan.FromSeconds(req.TtlSeconds.Value) : DefaultHoldTtl;
        var result = await store.ReserveAsync(tenantId, customerId.Trim(), req.OrderId, req.Amount, ttl, ct);
        if (!result.Success)
            return MapError(result.ErrorCode!, result.ErrorMessage!);

        var hold = result.Hold!;
        await publish.Publish(new LoyaltyReservedV1(
            tenantId, req.OrderId, hold.Id, hold.CustomerId, hold.Amount, hold.ExpiresAt, DateTimeOffset.UtcNow), ct);
        return Ok(MapHold(hold));
    }

    private static async Task<IResult> Capture(
        string tenantId,
        Guid holdId,
        ILoyaltyStore store,
        IPublishEndpoint publish,
        CancellationToken ct)
    {
        var result = await store.CaptureAsync(tenantId, holdId, ct);
        if (!result.Success)
            return MapError(result.ErrorCode!, result.ErrorMessage!);

        var hold = result.Hold!;
        await publish.Publish(new LoyaltyCapturedV1(
            tenantId, hold.OrderId, hold.Id, hold.CustomerId, hold.Amount, DateTimeOffset.UtcNow), ct);
        return Ok(MapHold(hold));
    }

    private static async Task<IResult> Release(
        string tenantId,
        Guid holdId,
        ReleaseRequest req,
        ILoyaltyStore store,
        IPublishEndpoint publish,
        CancellationToken ct)
    {
        var reason = string.IsNullOrWhiteSpace(req.Reason) ? "released" : req.Reason.Trim();
        var result = await store.ReleaseAsync(tenantId, holdId, reason, ct);
        if (!result.Success)
            return MapError(result.ErrorCode!, result.ErrorMessage!);

        var hold = result.Hold!;
        await publish.Publish(new LoyaltyReleasedV1(
            tenantId, hold.OrderId, hold.Id, hold.CustomerId, hold.Amount, reason, DateTimeOffset.UtcNow), ct);
        return Ok(MapHold(hold));
    }

    private static async Task<IResult> Refund(
        string tenantId,
        Guid holdId,
        ILoyaltyStore store,
        IPublishEndpoint publish,
        CancellationToken ct)
    {
        var result = await store.RefundAsync(tenantId, holdId, ct);
        if (!result.Success)
            return MapError(result.ErrorCode!, result.ErrorMessage!);

        var hold = result.Hold!;
        await publish.Publish(new LoyaltyRefundedV1(
            tenantId, hold.OrderId, hold.Id, hold.CustomerId, hold.Amount, DateTimeOffset.UtcNow), ct);
        return Ok(MapHold(hold));
    }

    private static async Task<IResult> RecordLedger(
        string tenantId,
        string customerId,
        LedgerRequest req,
        ILoyaltyStore store,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(customerId) || req.Delta == 0m || string.IsNullOrWhiteSpace(req.Reason))
            return BadRequest("invalid_request", "customerId, delta(!=0), reason are required.");

        var allowed = new[] { "EARN", "ADJUST", "REFUND" };
        var reason = req.Reason.Trim().ToUpperInvariant();
        if (Array.IndexOf(allowed, reason) < 0)
            return BadRequest("invalid_request", $"reason must be one of: {string.Join(", ", allowed)}.");

        var id = await store.RecordLedgerAsync(tenantId, customerId.Trim(), req.Delta, reason,
            req.OrderId, req.Notes, ct);
        return Ok(new { id });
    }

    private static object MapHold(dCMS.Loyalty.Api.Domain.LoyaltyHoldRow hold) => new
    {
        holdId = hold.Id,
        customerId = hold.CustomerId,
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
    public sealed record LedgerRequest(decimal Delta, string Reason, Guid? OrderId, string? Notes);
}
