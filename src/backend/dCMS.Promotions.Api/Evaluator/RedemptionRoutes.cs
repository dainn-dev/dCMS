using dCMS.AspNetCore.Auth;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using dCMS.Promotions.Api.Http;
using dCMS.Promotions.Contracts.Evaluate;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Promotions.Api.Evaluator;

/// <summary>
/// DAI-692: redemption side-effect endpoints invoked from Order.Api outbox handlers.
/// </summary>
public static class RedemptionRoutes
{
    public static void MapRedemptionRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();

        var g = app.MapGroup("/api/v1/tenants/{tenantId}/promotions/redemptions")
            .WithTags("redemptions")
            .WithTenantAccess(configuration);

        var confirm = g.MapPost("/confirm", Confirm);
        var release = g.MapPost("/release", Release);
        if (auth)
        {
            confirm.RequireAuthorization(DcmsPolicies.CatalogWrite);
            release.RequireAuthorization(DcmsPolicies.CatalogWrite);
        }
    }

    private static async Task<IResult> Confirm(
        string tenantId,
        [FromBody] ConfirmRedemptionRequest request,
        IPromoCodeRedemptionPersistence redemptions,
        CancellationToken ct)
    {
        if (request is null)
            return ApiEnvelope.Error("invalid_request", "Body is required.", StatusCodes.Status400BadRequest);
        if (string.IsNullOrWhiteSpace(request.OrderId) || string.IsNullOrWhiteSpace(request.PromoCodeId))
            return ApiEnvelope.Error("invalid_request", "OrderId and PromoCodeId are required.", StatusCodes.Status400BadRequest);
        if (string.IsNullOrWhiteSpace(request.Currency))
            return ApiEnvelope.Error("invalid_request", "Currency is required.", StatusCodes.Status400BadRequest);

        // Atomic UNIQUE-on-(tenant,promoCode,order) makes this idempotent on retries.
        var existing = await redemptions.GetByOrderAsync(tenantId, request.OrderId, ct).ConfigureAwait(false);
        if (existing is not null)
            return ApiEnvelope.Ok(new { confirmed = existing.Status == "confirmed", existed = true });

        var row = new PromoCodeRedemptionRow(
            Id: Guid.NewGuid().ToString(),
            TenantId: tenantId,
            PromoCodeId: request.PromoCodeId,
            OrderId: request.OrderId,
            CustomerId: request.CustomerId,
            GroupId: null,
            Amount: request.Amount,
            Currency: request.Currency,
            Status: "confirmed",
            RedeemedAt: DateTimeOffset.UtcNow,
            ReleasedAt: null);

        var inserted = await redemptions.InsertConfirmedAsync(row, ct).ConfigureAwait(false);
        return ApiEnvelope.Ok(new { confirmed = true, existed = !inserted });
    }

    private static async Task<IResult> Release(
        string tenantId,
        [FromBody] ReleaseRedemptionRequest request,
        IPromoCodeRedemptionPersistence redemptions,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.OrderId))
            return ApiEnvelope.Error("invalid_request", "OrderId is required.", StatusCodes.Status400BadRequest);

        var rows = await redemptions.MarkReleasedAsync(tenantId, request.OrderId, ct).ConfigureAwait(false);
        return ApiEnvelope.Ok(new { released = rows > 0 });
    }
}
