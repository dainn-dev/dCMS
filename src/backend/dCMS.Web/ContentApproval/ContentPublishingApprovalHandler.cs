using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Security;

namespace dCMS.Web.ContentApproval;

/// <summary>
/// DAI-721: Intercepts content publish for any doctype whose alias appears in
/// <c>ContentApproval:ApprovalRequiredDoctypes</c>. Cancels the publish and submits an approval
/// request to dCMS.Approval.Api. The approval callback (<see cref="ApprovalGate"/>) bypasses
/// this handler when it later performs the actual publish.
/// </summary>
public sealed class ContentPublishingApprovalHandler
    : INotificationAsyncHandler<ContentPublishingNotification>
{
    private readonly ApprovalApiClient _client;
    private readonly IConfiguration _configuration;
    private readonly IBackOfficeSecurityAccessor _security;
    private readonly ILogger<ContentPublishingApprovalHandler> _logger;

    public ContentPublishingApprovalHandler(
        ApprovalApiClient client,
        IConfiguration configuration,
        IBackOfficeSecurityAccessor security,
        ILogger<ContentPublishingApprovalHandler> logger)
    {
        _client = client;
        _configuration = configuration;
        _security = security;
        _logger = logger;
    }

    public async Task HandleAsync(ContentPublishingNotification notification, CancellationToken ct)
    {
        if (ApprovalGate.IsBypassed) return;

        var required = (_configuration["ContentApproval:ApprovalRequiredDoctypes"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (required.Count == 0) return;

        var tenantId = _configuration["Dcms:Estore:TenantId"]?.Trim();
        if (string.IsNullOrWhiteSpace(tenantId)) return;

        var userKey = _security.BackOfficeSecurity?.CurrentUser?.Key.ToString() ?? "system";

        foreach (var entity in notification.PublishedEntities)
        {
            if (!required.Contains(entity.ContentType.Alias)) continue;

            var ok = await _client.SubmitContentApprovalAsync(tenantId, entity.Key, userKey, ct).ConfigureAwait(false);
            notification.CancelOperation(new EventMessage(
                "Approval required",
                ok
                    ? "This content type requires approval before publishing. An approval request has been submitted."
                    : "Approval submission failed — please retry or contact an administrator.",
                EventMessageType.Warning));
            _logger.LogInformation(
                "Content publish intercepted (alias={Alias}, key={Key}, tenant={Tenant}, submitted={Ok}).",
                entity.ContentType.Alias, entity.Key, tenantId, ok);
        }
    }
}
