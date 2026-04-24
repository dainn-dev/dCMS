using System.Security.Claims;
using dCMS.AspNetCore.Auth;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using dCMS.Promotions.Api.Http;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Promotions.Api.PromoCodes;

/// <summary>
/// DAI-659: Promo codes CRUD + workflow (submit / approve / reject). Tenant-scoped.
/// </summary>
public static class PromoCodeRoutes
{
    public static void MapPromoCodeRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();

        var g = app.MapGroup("/api/v1/tenants/{tenantId}/promo-codes")
            .WithTags("promo-codes")
            .WithTenantAccess(configuration);

        Auth(g.MapGet("", ListPromoCodes), auth, write: false);
        Auth(g.MapGet("{id}", GetPromoCode), auth, write: false);
        Auth(g.MapPost("", CreatePromoCode), auth, write: true);
        Auth(g.MapPut("{id}", UpdatePromoCode), auth, write: true);
        Auth(g.MapPost("{id}/submit", Submit), auth, write: true);
        Auth(g.MapPost("{id}/archive", Archive), auth, write: true);
        AuthApproval(g.MapPost("{id}/approve", Approve), auth);
        AuthApproval(g.MapPost("{id}/reject", Reject), auth);
    }

    private static RouteHandlerBuilder Auth(RouteHandlerBuilder b, bool authEnabled, bool write) =>
        authEnabled ? b.RequireAuthorization(write ? DcmsPolicies.CatalogWrite : DcmsPolicies.CatalogRead) : b;

    private static RouteHandlerBuilder AuthApproval(RouteHandlerBuilder b, bool authEnabled) =>
        authEnabled ? b.RequireAuthorization(DcmsPolicies.CatalogApproval) : b;

    private static object ToDto(PromoCodeRow c) => new
    {
        id = c.Id,
        tenantId = c.TenantId,
        code = c.Code,
        nameJson = c.NameJson,
        discountType = c.DiscountType,
        discountValue = c.DiscountValue,
        workflowState = c.WorkflowState,
        createdAt = c.CreatedAt,
        updatedAt = c.UpdatedAt,
        promoTypeLabel = c.PromoTypeLabel,
        minSpend = c.MinSpend,
        startDate = c.StartDate,
        endDate = c.EndDate,
        submittedBy = c.SubmittedByUserId,
        submittedDate = c.SubmittedAt,
    };

    private sealed record PromoCodeWriteRequest(
        string Code,
        string NameJson = "{}",
        string DiscountType = "percentage",
        string? DiscountValue = null,
        string? PromoTypeLabel = null,
        string MinSpend = "",
        DateTimeOffset? StartDate = null,
        DateTimeOffset? EndDate = null);

    private sealed record WorkflowActionRequest(string? Comment = null);

    private static async Task<IResult> ListPromoCodes(
        string tenantId, IPromoCodePersistence promo,
        string? status = null, int page = 1, int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);
        if (!string.IsNullOrWhiteSpace(status) && !PromoCodeRow.ValidWorkflowStates.Contains(status))
            return ApiEnvelope.Error("validation_error", $"Invalid status '{status}'.", StatusCodes.Status400BadRequest);

        var (items, total) = await promo.ListPromoCodesAsync(tenantId, status, page, pageSize, cancellationToken)
            .ConfigureAwait(false);
        return ApiEnvelope.Ok(items.Select(ToDto), new { total, page, pageSize });
    }

    private static async Task<IResult> GetPromoCode(
        string tenantId, string id, IPromoCodePersistence promo, CancellationToken cancellationToken = default)
    {
        var row = await promo.GetPromoCodeAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        return row is null
            ? ApiEnvelope.Error("not_found", $"Promo code '{id}' not found.", StatusCodes.Status404NotFound)
            : ApiEnvelope.Ok(ToDto(row));
    }

    private static async Task<IResult> CreatePromoCode(
        string tenantId, [FromBody] PromoCodeWriteRequest body,
        IPromoCodePersistence promo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body.Code))
            return ApiEnvelope.Error("validation_error", "Code is required.", StatusCodes.Status400BadRequest);
        var code = body.Code.Trim().ToUpperInvariant();
        if (!PromoCodeRow.IsValidCode(code))
            return ApiEnvelope.Error("validation_error", $"Code '{code}' is invalid.", StatusCodes.Status400BadRequest);
        if (!PromoCodeRow.ValidDiscountTypes.Contains(body.DiscountType))
            return ApiEnvelope.Error("validation_error", $"DiscountType '{body.DiscountType}' is invalid.",
                StatusCodes.Status400BadRequest);
        if (await promo.PromoCodeExistsAsync(tenantId, code, null, cancellationToken).ConfigureAwait(false))
            return ApiEnvelope.Error("conflict", $"Promo code '{code}' already exists.", StatusCodes.Status409Conflict);

        var now = DateTimeOffset.UtcNow;
        var id = $"promo_{Guid.NewGuid():N}";
        var row = new PromoCodeRow(id, tenantId, code, body.NameJson, body.DiscountType,
            body.DiscountValue ?? "", "draft", now, now,
            body.PromoTypeLabel ?? "", body.MinSpend ?? "", body.StartDate, body.EndDate);

        await promo.CreatePromoCodeAsync(row, cancellationToken).ConfigureAwait(false);
        return Results.Json(new { data = ToDto(row), meta = (object?)null, error = (object?)null },
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdatePromoCode(
        string tenantId, string id, [FromBody] PromoCodeWriteRequest body,
        IPromoCodePersistence promo, CancellationToken cancellationToken = default)
    {
        var existing = await promo.GetPromoCodeAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return ApiEnvelope.Error("not_found", $"Promo code '{id}' not found.", StatusCodes.Status404NotFound);
        if (existing.WorkflowState is "archived" or "approved")
            return ApiEnvelope.Error("conflict", "Cannot edit an archived or approved promo code.", StatusCodes.Status409Conflict);

        var code = string.IsNullOrWhiteSpace(body.Code) ? existing.Code : body.Code.Trim().ToUpperInvariant();
        if (!PromoCodeRow.IsValidCode(code))
            return ApiEnvelope.Error("validation_error", $"Code '{code}' is invalid.", StatusCodes.Status400BadRequest);
        if (!PromoCodeRow.ValidDiscountTypes.Contains(body.DiscountType))
            return ApiEnvelope.Error("validation_error", $"DiscountType '{body.DiscountType}' is invalid.",
                StatusCodes.Status400BadRequest);
        if (code != existing.Code && await promo.PromoCodeExistsAsync(tenantId, code, id, cancellationToken).ConfigureAwait(false))
            return ApiEnvelope.Error("conflict", $"Code '{code}' already exists.", StatusCodes.Status409Conflict);

        var updated = existing with
        {
            Code = code,
            NameJson = body.NameJson,
            DiscountType = body.DiscountType,
            DiscountValue = body.DiscountValue ?? "",
            PromoTypeLabel = body.PromoTypeLabel ?? "",
            MinSpend = body.MinSpend ?? "",
            StartDate = body.StartDate,
            EndDate = body.EndDate,
        };

        await promo.UpdatePromoCodeAsync(updated, cancellationToken).ConfigureAwait(false);
        var reloaded = await promo.GetPromoCodeAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(ToDto(reloaded!));
    }

    private static string ActorId(HttpContext ctx) =>
        ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";

    private static async Task<IResult> DoTransition(
        string tenantId, string id, string toState, WorkflowActionRequest? body,
        IPromoCodePersistence promo, HttpContext ctx, CancellationToken ct)
    {
        try
        {
            var ok = await promo.TransitionWorkflowAsync(id, tenantId, toState, ActorId(ctx), body?.Comment ?? "", ct)
                .ConfigureAwait(false);
            if (!ok) return ApiEnvelope.Error("not_found", $"Promo code '{id}' not found.", StatusCodes.Status404NotFound);
        }
        catch (InvalidOperationException ex)
        {
            return ApiEnvelope.Error("invalid_transition", ex.Message, StatusCodes.Status422UnprocessableEntity);
        }

        var updated = await promo.GetPromoCodeAsync(id, tenantId, ct).ConfigureAwait(false);
        return ApiEnvelope.Ok(ToDto(updated!));
    }

    private static Task<IResult> Submit(string tenantId, string id, [FromBody] WorkflowActionRequest? body,
        IPromoCodePersistence promo, HttpContext ctx, CancellationToken ct) =>
        DoTransition(tenantId, id, "pending_approval", body, promo, ctx, ct);

    private static Task<IResult> Archive(string tenantId, string id, [FromBody] WorkflowActionRequest? body,
        IPromoCodePersistence promo, HttpContext ctx, CancellationToken ct) =>
        DoTransition(tenantId, id, "archived", body, promo, ctx, ct);

    private static Task<IResult> Approve(string tenantId, string id, [FromBody] WorkflowActionRequest? body,
        IPromoCodePersistence promo, HttpContext ctx, CancellationToken ct) =>
        DoTransition(tenantId, id, "approved", body, promo, ctx, ct);

    private static async Task<IResult> Reject(string tenantId, string id, [FromBody] WorkflowActionRequest? body,
        IPromoCodePersistence promo, HttpContext ctx, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body?.Comment))
            return ApiEnvelope.Error("validation_error", "Comment is required for reject.", StatusCodes.Status400BadRequest);
        return await DoTransition(tenantId, id, "rejected", body, promo, ctx, ct).ConfigureAwait(false);
    }
}
