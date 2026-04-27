using dCMS.Core.Persistence;
using dCMS.Promotions.Api.Http;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Promotions.Api.Internal;

/// <summary>
/// Phase C: cross-service workflow endpoints used by dCMS.Approval.Api so the approval flow
/// no longer needs a direct connection string to dcms_promotions.
/// </summary>
public static class InternalPromotionsRoutes
{
    public static void MapInternalPromotionsRoutes(this WebApplication app)
    {
        var g = app.MapGroup("/internal/promotions")
            .WithTags("promotions-internal")
            .AddEndpointFilter<InternalPromotionsApiKeyEndpointFilter>()
            .DisableRateLimiting();

        g.MapGet("/tenants/{tenantId}/campaigns/{id}/workflow-state", GetCampaignState).AllowAnonymous();
        g.MapPost("/tenants/{tenantId}/campaigns/{id}/workflow-transition", TransitionCampaign).AllowAnonymous();

        g.MapGet("/tenants/{tenantId}/promo-codes/{id}/workflow-state", GetPromoCodeState).AllowAnonymous();
        g.MapPost("/tenants/{tenantId}/promo-codes/{id}/workflow-transition", TransitionPromoCode).AllowAnonymous();
    }

    private sealed record TransitionBody(string ToState, string ActorUserId, string? Comment);

    private static async Task<IResult> GetCampaignState(
        string tenantId, string id, ICampaignPersistence campaigns, CancellationToken ct)
    {
        var c = await campaigns.GetCampaignAsync(id, tenantId, ct).ConfigureAwait(false);
        if (c is null) return ApiEnvelope.Error("not_found", "Campaign not found.", StatusCodes.Status404NotFound);
        return ApiEnvelope.Ok(new { id = c.Id, workflowState = c.WorkflowState });
    }

    private static async Task<IResult> TransitionCampaign(
        string tenantId, string id, [FromBody] TransitionBody body,
        ICampaignPersistence campaigns, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.ToState) || string.IsNullOrWhiteSpace(body.ActorUserId))
            return ApiEnvelope.Error("validation_error", "toState and actorUserId are required.",
                StatusCodes.Status400BadRequest);
        try
        {
            var ok = await campaigns.TransitionWorkflowAsync(id, tenantId, body.ToState, body.ActorUserId,
                body.Comment ?? "", ct).ConfigureAwait(false);
            if (!ok) return ApiEnvelope.Error("not_found", "Campaign not found.", StatusCodes.Status404NotFound);
        }
        catch (InvalidOperationException ex)
        {
            return ApiEnvelope.Error("invalid_transition", ex.Message, StatusCodes.Status422UnprocessableEntity);
        }
        return ApiEnvelope.Ok(new { ok = true });
    }

    private static async Task<IResult> GetPromoCodeState(
        string tenantId, string id, IPromoCodePersistence promo, CancellationToken ct)
    {
        var p = await promo.GetPromoCodeAsync(id, tenantId, ct).ConfigureAwait(false);
        if (p is null) return ApiEnvelope.Error("not_found", "Promo code not found.", StatusCodes.Status404NotFound);
        return ApiEnvelope.Ok(new { id = p.Id, workflowState = p.WorkflowState });
    }

    private static async Task<IResult> TransitionPromoCode(
        string tenantId, string id, [FromBody] TransitionBody body,
        IPromoCodePersistence promo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.ToState) || string.IsNullOrWhiteSpace(body.ActorUserId))
            return ApiEnvelope.Error("validation_error", "toState and actorUserId are required.",
                StatusCodes.Status400BadRequest);
        try
        {
            var ok = await promo.TransitionWorkflowAsync(id, tenantId, body.ToState, body.ActorUserId,
                body.Comment ?? "", ct).ConfigureAwait(false);
            if (!ok) return ApiEnvelope.Error("not_found", "Promo code not found.", StatusCodes.Status404NotFound);
        }
        catch (InvalidOperationException ex)
        {
            return ApiEnvelope.Error("invalid_transition", ex.Message, StatusCodes.Status422UnprocessableEntity);
        }
        return ApiEnvelope.Ok(new { ok = true });
    }
}
