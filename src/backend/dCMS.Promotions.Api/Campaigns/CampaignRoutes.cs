using System.Security.Claims;
using dCMS.AspNetCore.Auth;
using dCMS.Promotions.Api.Http;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Promotions.Api.Campaigns;

/// <summary>
/// DAI-598 / DAI-606: Campaign CRUD + workflow transitions + history.
/// Moved from dCMS.Catalog.Api to dCMS.Promotions.Api.
/// Tenant-scoped; no storeId. Auth: CatalogRead/Write/Approval.
/// </summary>
public static class CampaignRoutes
{
    public static void MapCampaignRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();

        var g = app.MapGroup("/api/v1/tenants/{tenantId}/campaigns")
            .WithTags("campaigns")
            .WithTenantAccess(configuration);

        Auth(g.MapGet("",           ListCampaigns),   auth, write: false);
        Auth(g.MapGet("{id}",       GetCampaign),     auth, write: false);
        Auth(g.MapPost("",          CreateCampaign),  auth, write: true);
        Auth(g.MapPut("{id}",       UpdateCampaign),  auth, write: true);
        Auth(g.MapDelete("{id}",    DeleteCampaign),  auth, write: true);
        Auth(g.MapGet("{id}/history", GetHistory),    auth, write: false);

        Auth(g.MapPost("{id}/submit",   Submit),   auth, write: true);
        Auth(g.MapPost("{id}/activate", Activate), auth, write: true);
        Auth(g.MapPost("{id}/pause",    Pause),    auth, write: true);
        Auth(g.MapPost("{id}/archive",  Archive),  auth, write: true);
        AuthApproval(g.MapPost("{id}/approve", Approve), auth);
        AuthApproval(g.MapPost("{id}/reject",  Reject),  auth);
    }

    private static RouteHandlerBuilder Auth(RouteHandlerBuilder b, bool authEnabled, bool write) =>
        authEnabled ? b.RequireAuthorization(write ? DcmsPolicies.CatalogWrite : DcmsPolicies.CatalogRead) : b;
    private static RouteHandlerBuilder AuthApproval(RouteHandlerBuilder b, bool authEnabled) =>
        authEnabled ? b.RequireAuthorization(DcmsPolicies.CatalogApproval) : b;

    private static object ToDto(CampaignRow c) => new
    {
        id = c.Id, tenantId = c.TenantId, code = c.Code, nameJson = c.NameJson,
        editorKind = c.EditorKind, workflowState = c.WorkflowState, channel = c.Channel,
        startDate = c.StartDate, endDate = c.EndDate,
        activeDaysJson = c.ActiveDaysJson, activeMonthsJson = c.ActiveMonthsJson,
        qualifiersJson = c.QualifiersJson, mechanicsJson = c.MechanicsJson,
        promotionDetailsJson = c.PromotionDetailsJson,
        budget = c.Budget, audience = c.Audience, conversions = c.Conversions,
        createdAt = c.CreatedAt, updatedAt = c.UpdatedAt,
        submittedByUserId = c.SubmittedByUserId,
        submittedAt = c.SubmittedAt,
    };

    private static object ToHistoryDto(CampaignWorkflowHistoryRow h) => new
    {
        id = h.Id, campaignId = h.CampaignId, actorUserId = h.ActorUserId,
        fromState = h.FromState, toState = h.ToState,
        comment = h.Comment, createdAt = h.CreatedAt,
    };

    private sealed record CampaignWriteRequest(
        string  Code,
        string  NameJson             = "{}",
        string  EditorKind           = "product-discount",
        string  Channel              = "Email",
        DateTimeOffset? StartDate    = null,
        DateTimeOffset? EndDate      = null,
        string  ActiveDaysJson       = "[]",
        string  ActiveMonthsJson     = "[]",
        string  QualifiersJson       = "{}",
        string  MechanicsJson        = "{}",
        string  PromotionDetailsJson = "{}",
        string? Budget               = null,
        string? Audience             = null);

    private sealed record WorkflowActionRequest(string? Comment = null);

    private static async Task<IResult> ListCampaigns(
        string tenantId, ICampaignPersistence campaigns,
        string? status = null, string? channel = null, string? search = null,
        int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(1, page);
        var (items, total) = await campaigns.ListCampaignsAsync(
            tenantId, status, channel, search, page, pageSize, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(items.Select(ToDto), new { total, page, pageSize });
    }

    private static async Task<IResult> GetCampaign(
        string tenantId, string id, ICampaignPersistence campaigns, CancellationToken cancellationToken = default)
    {
        var c = await campaigns.GetCampaignAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        return c is null
            ? ApiEnvelope.Error("not_found", $"Campaign '{id}' not found.", StatusCodes.Status404NotFound)
            : ApiEnvelope.Ok(ToDto(c));
    }

    private static async Task<IResult> CreateCampaign(
        string tenantId, [FromBody] CampaignWriteRequest body,
        ICampaignPersistence campaigns, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body.Code))
            return ApiEnvelope.Error("validation_error", "Code is required.", StatusCodes.Status400BadRequest);
        var code = body.Code.Trim().ToUpperInvariant();
        if (!CampaignRow.IsValidCode(code))
            return ApiEnvelope.Error("validation_error", $"Code '{code}' is invalid.", StatusCodes.Status400BadRequest);
        if (!CampaignRow.ValidEditorKinds.Contains(body.EditorKind))
            return ApiEnvelope.Error("validation_error", $"EditorKind '{body.EditorKind}' is invalid.", StatusCodes.Status400BadRequest);
        if (!CampaignRow.ValidChannels.Contains(body.Channel))
            return ApiEnvelope.Error("validation_error", $"Channel '{body.Channel}' is invalid.", StatusCodes.Status400BadRequest);
        if (await campaigns.CampaignCodeExistsAsync(tenantId, code, null, cancellationToken).ConfigureAwait(false))
            return ApiEnvelope.Error("conflict", $"Campaign code '{code}' already exists.", StatusCodes.Status409Conflict);

        var now = DateTimeOffset.UtcNow;
        var id  = $"cmp_{Guid.NewGuid():N}";
        var row = new CampaignRow(id, tenantId, code, body.NameJson, body.EditorKind,
            "draft", body.Channel, body.StartDate, body.EndDate,
            body.ActiveDaysJson, body.ActiveMonthsJson,
            body.QualifiersJson, body.MechanicsJson, body.PromotionDetailsJson,
            body.Budget ?? "", body.Audience ?? "", 0, now, now);

        await campaigns.CreateCampaignAsync(row, cancellationToken).ConfigureAwait(false);
        return Results.Json(new { data = ToDto(row), meta = (object?)null, error = (object?)null },
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateCampaign(
        string tenantId, string id, [FromBody] CampaignWriteRequest body,
        ICampaignPersistence campaigns, CancellationToken cancellationToken = default)
    {
        var existing = await campaigns.GetCampaignAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return ApiEnvelope.Error("not_found", $"Campaign '{id}' not found.", StatusCodes.Status404NotFound);
        var code = string.IsNullOrWhiteSpace(body.Code) ? existing.Code : body.Code.Trim().ToUpperInvariant();
        if (code != existing.Code && await campaigns.CampaignCodeExistsAsync(tenantId, code, id, cancellationToken).ConfigureAwait(false))
            return ApiEnvelope.Error("conflict", $"Code '{code}' already exists.", StatusCodes.Status409Conflict);
        var updated = existing with
        {
            Code = code, NameJson = body.NameJson, EditorKind = body.EditorKind, Channel = body.Channel,
            StartDate = body.StartDate, EndDate = body.EndDate,
            ActiveDaysJson = body.ActiveDaysJson, ActiveMonthsJson = body.ActiveMonthsJson,
            QualifiersJson = body.QualifiersJson, MechanicsJson = body.MechanicsJson,
            PromotionDetailsJson = body.PromotionDetailsJson,
            Budget = body.Budget ?? existing.Budget, Audience = body.Audience ?? existing.Audience,
        };
        await campaigns.UpdateCampaignAsync(updated, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(ToDto(updated));
    }

    private static async Task<IResult> DeleteCampaign(
        string tenantId, string id, ICampaignPersistence campaigns, CancellationToken cancellationToken = default)
    {
        var existing = await campaigns.GetCampaignAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return ApiEnvelope.Error("not_found", $"Campaign '{id}' not found.", StatusCodes.Status404NotFound);
        if (existing.WorkflowState == "active")
            return ApiEnvelope.Error("conflict", "Cannot delete an active campaign. Pause or archive it first.", StatusCodes.Status409Conflict);
        await campaigns.DeleteCampaignAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> GetHistory(
        string tenantId, string id, ICampaignPersistence campaigns, CancellationToken cancellationToken = default)
    {
        var exists = await campaigns.GetCampaignAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        if (exists is null)
            return ApiEnvelope.Error("not_found", $"Campaign '{id}' not found.", StatusCodes.Status404NotFound);
        var history = await campaigns.GetWorkflowHistoryAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(history.Select(ToHistoryDto));
    }

    private static string ActorId(HttpContext ctx) =>
        ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";

    private static async Task<IResult> DoTransition(
        string tenantId, string id, string toState, WorkflowActionRequest? body,
        ICampaignPersistence campaigns, HttpContext ctx, CancellationToken ct)
    {
        try
        {
            var ok = await campaigns.TransitionWorkflowAsync(id, tenantId, toState, ActorId(ctx), body?.Comment ?? "", ct).ConfigureAwait(false);
            if (!ok) return ApiEnvelope.Error("not_found", $"Campaign '{id}' not found.", StatusCodes.Status404NotFound);
        }
        catch (InvalidOperationException ex)
        {
            return ApiEnvelope.Error("invalid_transition", ex.Message, StatusCodes.Status422UnprocessableEntity);
        }
        var updated = await campaigns.GetCampaignAsync(id, tenantId, ct).ConfigureAwait(false);
        return ApiEnvelope.Ok(ToDto(updated!));
    }

    private static Task<IResult> Submit(string tenantId, string id, [FromBody] WorkflowActionRequest? body, ICampaignPersistence campaigns, HttpContext ctx, CancellationToken ct) => DoTransition(tenantId, id, "pending_approval", body, campaigns, ctx, ct);
    private static Task<IResult> Approve(string tenantId, string id, [FromBody] WorkflowActionRequest? body, ICampaignPersistence campaigns, HttpContext ctx, CancellationToken ct) => DoTransition(tenantId, id, "approved", body, campaigns, ctx, ct);
    private static Task<IResult> Reject(string tenantId, string id, [FromBody] WorkflowActionRequest? body, ICampaignPersistence campaigns, HttpContext ctx, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body?.Comment))
            return Task.FromResult<IResult>(ApiEnvelope.Error("validation_error",
                "Comment is required when rejecting a campaign.", StatusCodes.Status400BadRequest));
        return DoTransition(tenantId, id, "rejected", body, campaigns, ctx, ct);
    }
    private static Task<IResult> Activate(string tenantId, string id, [FromBody] WorkflowActionRequest? body, ICampaignPersistence campaigns, HttpContext ctx, CancellationToken ct) => DoTransition(tenantId, id, "active", body, campaigns, ctx, ct);
    private static Task<IResult> Pause(string tenantId, string id, [FromBody] WorkflowActionRequest? body, ICampaignPersistence campaigns, HttpContext ctx, CancellationToken ct) => DoTransition(tenantId, id, "deactivated", body, campaigns, ctx, ct);
    private static Task<IResult> Archive(string tenantId, string id, [FromBody] WorkflowActionRequest? body, ICampaignPersistence campaigns, HttpContext ctx, CancellationToken ct) => DoTransition(tenantId, id, "archived", body, campaigns, ctx, ct);
}
