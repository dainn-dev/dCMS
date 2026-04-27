using System.Security.Claims;
using System.Text.Json;
using dCMS.AspNetCore.Auth;
using dCMS.Core.Approvals;
using dCMS.Core.Persistence;

namespace dCMS.Approval.Api.Routes;

public static class ApprovalRoutes
{
    public static void MapApprovalRoutes(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/tenants/{tenantId}/approvals")
            .WithTags("approvals")
            .WithTenantAccess(app.Configuration)
            .RequireAuthorization(DcmsPolicies.ApprovalManage);

        group.MapPost("/", CreateApprovalRequest);
        group.MapGet("/", ListApprovals);

        group.MapPost("/{id:guid}/approve", (HttpContext ctx, string tenantId, Guid id, TransitionRequest req,
                IApprovalRequestPersistence persistence, ApprovalSubjectRegistry registry, CancellationToken ct) =>
            Transition(ctx, tenantId, id, "Approved", ApprovalAction.Approve, req, persistence, registry, ct));

        group.MapPost("/{id:guid}/reject", (HttpContext ctx, string tenantId, Guid id, TransitionRequest req,
                IApprovalRequestPersistence persistence, ApprovalSubjectRegistry registry, CancellationToken ct) =>
            Transition(ctx, tenantId, id, "Rejected", ApprovalAction.Reject, req, persistence, registry, ct));

        group.MapPost("/{id:guid}/request-changes", (HttpContext ctx, string tenantId, Guid id, TransitionRequest req,
                IApprovalRequestPersistence persistence, ApprovalSubjectRegistry registry, CancellationToken ct) =>
            Transition(ctx, tenantId, id, "ChangesRequested", ApprovalAction.RequestChanges, req, persistence, registry, ct));

        group.MapPost("/bulk-approve", BulkApprove);
    }

    private static async Task<IResult> CreateApprovalRequest(
        HttpContext ctx,
        string tenantId,
        CreateApprovalRequestRequest req,
        IApprovalRequestPersistence persistence,
        ApprovalSubjectRegistry registry,
        CancellationToken ct)
    {
        var userId = RequireUserId(ctx);
        if (string.IsNullOrWhiteSpace(req.EntityType) || string.IsNullOrWhiteSpace(req.EntityId))
            return BadRequest("invalid_request", "entityType and entityId are required.");

        var payload = req.PayloadSnapshot.ValueKind == JsonValueKind.Undefined
            ? "{}"
            : req.PayloadSnapshot.GetRawText();

        // Strategy hook: allow creating requests even if the subject isn't registered yet (for extensibility),
        // but we can still validate basic submit rules when a subject exists.
        IApprovalSubject? subject = null;
        if (registry.TryGet(req.EntityType, out var s))
            subject = s;

        if (subject is not null)
        {
            using var doc = JsonDocument.Parse(payload);
            var err = await subject.ValidateAsync(tenantId, req.EntityId, ApprovalAction.Submit, doc, ct);
            if (!string.IsNullOrWhiteSpace(err))
                return BadRequest("validation_failed", err);
        }

        var now = DateTimeOffset.UtcNow;
        var id = await persistence.CreatePendingAsync(tenantId, req.EntityType.Trim(), req.EntityId.Trim(), userId,
            req.CurrentApprover?.Trim(), payload, now, ct);

        // For subjects that still maintain a legacy workflow column, keep it in sync on submit.
        if (subject is not null)
        {
            using var doc = JsonDocument.Parse(payload);
            await subject.ApplyAsync(tenantId, req.EntityId.Trim(), ApprovalAction.Submit, doc, userId, ct);
        }

        return Ok(new { id });
    }

    private static async Task<IResult> ListApprovals(
        string tenantId,
        string? entityType,
        string? state,
        string? assignedTo,
        int? page,
        int? pageSize,
        IApprovalRequestPersistence persistence,
        CancellationToken ct)
    {
        var (items, total) = await persistence.ListAsync(
            tenantId,
            string.IsNullOrWhiteSpace(entityType) ? null : entityType.Trim(),
            string.IsNullOrWhiteSpace(state) ? null : state.Trim(),
            string.IsNullOrWhiteSpace(assignedTo) ? null : assignedTo.Trim(),
            page ?? 1,
            pageSize ?? 50,
            ct);

        var mapped = items.Select(MapDto).ToList();
        return Results.Json(new { data = mapped, meta = new { total }, error = (object?)null });
    }

    private static async Task<IResult> BulkApprove(
        HttpContext ctx,
        string tenantId,
        BulkApproveRequest req,
        IApprovalRequestPersistence persistence,
        CancellationToken ct)
    {
        var userId = RequireUserId(ctx);
        var ids = (req.Ids ?? Array.Empty<Guid>()).Distinct().ToArray();
        if (ids.Length == 0)
            return BadRequest("invalid_request", "ids is required.");

        // NOTE: this endpoint only transitions rows; entity-specific side effects should be done by callers
        // using per-entity bulk endpoints, or later via background processing (DAI-688 follow-up).
        var updated = await persistence.BulkApproveAsync(tenantId, ids, userId, DateTimeOffset.UtcNow, ct);
        return Ok(new { updated });
    }

    private static async Task<IResult> Transition(
        HttpContext ctx,
        string tenantId,
        Guid id,
        string nextState,
        ApprovalAction action,
        TransitionRequest req,
        IApprovalRequestPersistence persistence,
        ApprovalSubjectRegistry registry,
        CancellationToken ct)
    {
        var userId = RequireUserId(ctx);
        var row = await persistence.GetByIdAsync(tenantId, id, ct);
        if (row is null)
            return Results.NotFound(new { data = (object?)null, meta = (object?)null, error = new { code = "not_found", message = "Approval request not found." } });

        if (!string.Equals(row.State, "PendingApproval", StringComparison.OrdinalIgnoreCase))
            return BadRequest("invalid_state", $"Cannot transition from {row.State}.");

        IApprovalSubject? subject = null;
        if (registry.TryGet(row.EntityType, out var s))
            subject = s;

        using var payload = JsonDocument.Parse(row.PayloadSnapshotJson);
        if (subject is not null)
        {
            var err = await subject.ValidateAsync(tenantId, row.EntityId, action, payload, ct);
            if (!string.IsNullOrWhiteSpace(err))
                return BadRequest("validation_failed", err);
        }

        var now = DateTimeOffset.UtcNow;
        var ok = await persistence.TryTransitionAsync(
            tenantId,
            id,
            expectedState: "PendingApproval",
            nextState: nextState,
            actedByUserId: userId,
            notes: string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
            now: now,
            finalize: nextState is "Approved" or "Rejected",
            ct: ct);

        if (!ok)
            return BadRequest("conflict", "Transition conflict (state changed).");

        if (subject is not null)
            await subject.ApplyAsync(tenantId, row.EntityId, action, payload, userId, ct);

        return Ok(new { id, state = nextState });
    }

    private static string RequireUserId(HttpContext ctx)
    {
        var sub = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(sub))
            throw new InvalidOperationException("Authenticated request is missing NameIdentifier claim.");
        return sub.Trim();
    }

    private static IResult Ok(object data) =>
        Results.Json(new { data, meta = (object?)null, error = (object?)null });

    private static IResult BadRequest(string code, string message) =>
        Results.BadRequest(new { data = (object?)null, meta = (object?)null, error = new { code, message } });

    private static ApprovalRequestDto MapDto(ApprovalRequestRow r) => new(
        r.Id,
        r.EntityType,
        r.EntityId,
        r.State,
        r.RequestedBy,
        r.RequestedAt,
        r.CurrentApprover,
        r.HistoryJson,
        r.PayloadSnapshotJson,
        r.FinalizedAt);

    public sealed record CreateApprovalRequestRequest(
        string EntityType,
        string EntityId,
        JsonElement PayloadSnapshot,
        string? CurrentApprover);

    public sealed record TransitionRequest(string? Notes);

    public sealed record BulkApproveRequest(Guid[]? Ids);

    public sealed record ApprovalRequestDto(
        Guid Id,
        string EntityType,
        string EntityId,
        string State,
        string RequestedBy,
        DateTimeOffset RequestedAt,
        string? CurrentApprover,
        string HistoryJson,
        string PayloadSnapshotJson,
        DateTimeOffset? FinalizedAt);
}

