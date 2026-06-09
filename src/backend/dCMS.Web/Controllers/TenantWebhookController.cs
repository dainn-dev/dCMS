using System.Security.Cryptography;
using dCMS.Infrastructure.Platform;
using dCMS.Provisioning.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;

namespace dCMS.Web.Controllers;

[ApiController]
[Route("umbraco/dcms/api/tenants/{tenantId}/webhooks")]
[Authorize(AuthenticationSchemes = Constants.Security.BackOfficeAuthenticationType)]
public sealed class TenantWebhookController : ControllerBase
{
    private static readonly HashSet<string> PlatformAdminGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "dcmsItAdministrator",
        "dcmsSysAdministrator",
    };

    private readonly ITenantWebhookSubscriptionRepository _subscriptions;
    private readonly ITenantWebhookDeliveryRepository _deliveries;
    private readonly TenantWebhookDispatcher _dispatcher;
    private readonly IBackOfficeSecurityAccessor _securityAccessor;

    public TenantWebhookController(
        ITenantWebhookSubscriptionRepository subscriptions,
        ITenantWebhookDeliveryRepository deliveries,
        TenantWebhookDispatcher dispatcher,
        IBackOfficeSecurityAccessor securityAccessor)
    {
        _subscriptions = subscriptions;
        _deliveries = deliveries;
        _dispatcher = dispatcher;
        _securityAccessor = securityAccessor;
    }

    [HttpGet]
    public async Task<IActionResult> List(string tenantId, CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        var subs = await _subscriptions.ListByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(new
        {
            data = subs.Select(s => new { id = s.Id, url = s.Url, events = s.Events, status = s.Status.ToString().ToLowerInvariant(), failureCount = s.FailureCount }),
            meta = (object?)null,
            error = (object?)null,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        string tenantId,
        [FromBody] CreateWebhookRequest request,
        CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        if (string.IsNullOrWhiteSpace(request.Url) || request.Events is null || request.Events.Count == 0)
            return BadRequest(ErrorEnvelope("Url and at least one event are required."));

        var id = $"wh_{Guid.NewGuid():N}";
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;
        var record = new TenantWebhookSubscriptionRecord(
            id, tenantId, request.Url.Trim(), secret, request.Events,
            WebhookSubscriptionStatus.Active, 0, now, now);

        await _subscriptions.CreateAsync(record, cancellationToken).ConfigureAwait(false);
        return Created($"/umbraco/dcms/api/tenants/{tenantId}/webhooks/{id}",
            new { data = new { id, url = record.Url, events = record.Events, secret }, meta = (object?)null, error = (object?)null });
    }

    [HttpPost("deliveries/{deliveryId:long}/replay")]
    public async Task<IActionResult> Replay(string tenantId, long deliveryId, CancellationToken cancellationToken)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        var delivery = await _deliveries.GetByIdAsync(deliveryId, cancellationToken).ConfigureAwait(false);
        if (delivery is null || !string.Equals(delivery.TenantId, tenantId, StringComparison.Ordinal))
            return NotFound(ErrorEnvelope("Delivery not found."));

        await _dispatcher.ReplayAsync(deliveryId, cancellationToken).ConfigureAwait(false);
        return Ok(new { data = new { deliveryId, replayed = true }, meta = (object?)null, error = (object?)null });
    }

    [HttpGet("dead-letter")]
    public async Task<IActionResult> ListDeadLetter(string tenantId, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        if (DenyIfNotPlatformAdmin() is { } denied)
            return denied;

        var rows = await _deliveries.ListDeadLetterAsync(tenantId, limit, cancellationToken).ConfigureAwait(false);
        return Ok(new
        {
            data = rows.Select(r => new { id = r.Id, eventType = r.EventType, status = r.Status.ToString().ToLowerInvariant(), attemptCount = r.AttemptCount, lastError = r.LastError }),
            meta = (object?)null,
            error = (object?)null,
        });
    }

    private IActionResult? DenyIfNotPlatformAdmin()
    {
        var user = _securityAccessor.BackOfficeSecurity?.CurrentUser;
        if (user is null)
            return Unauthorized(ErrorEnvelope("Authentication required."));
        if (!user.Groups.Any(g => PlatformAdminGroups.Contains(g.Alias)))
            return StatusCode(403, ErrorEnvelope("Platform administrator access required."));
        return null;
    }

    private static object ErrorEnvelope(string message) => new
    {
        data = (object?)null,
        meta = (object?)null,
        error = new { code = "error", message },
    };

    public sealed class CreateWebhookRequest
    {
        public string Url { get; set; } = "";
        public List<string> Events { get; set; } = [];
    }
}
