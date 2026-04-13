using System.Net.Http.Headers;
using System.Text;
using dCMS.AspNetCore.Auth;
using dCMS.Web.CatalogProxy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common.Attributes;

namespace dCMS.Web.SystemHealth;

/// <summary>US-F4 / DAI-363 — BFF for Order DLQ admin API (SuperAdmin only).</summary>
[PluginController("DcmsSystemHealth")]
[IsBackOffice]
public sealed class SystemHealthBackofficeProxyController : UmbracoAuthorizedJsonController
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<OrderProxyOptions> _orderOptions;
    private readonly CatalogJwtIssuer _jwtIssuer;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurity;
    private readonly IUserService _userService;

    public SystemHealthBackofficeProxyController(
        IHttpClientFactory httpClientFactory,
        IOptions<OrderProxyOptions> orderOptions,
        CatalogJwtIssuer jwtIssuer,
        IBackOfficeSecurityAccessor backOfficeSecurity,
        IUserService userService)
    {
        _httpClientFactory = httpClientFactory;
        _orderOptions = orderOptions;
        _jwtIssuer = jwtIssuer;
        _backOfficeSecurity = backOfficeSecurity;
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> OrderDlqList(
        [FromQuery] string? eventType,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] bool includeDiscarded,
        CancellationToken cancellationToken)
    {
        if (!TryAuthorizeSuperAdmin(out var forbid))
            return forbid!;

        var q = new StringBuilder("/api/v1/admin/orders/dlq?");
        if (!string.IsNullOrWhiteSpace(eventType))
            q.Append("eventType=").Append(Uri.EscapeDataString(eventType.Trim())).Append('&');
        if (from is not null)
            q.Append("from=").Append(Uri.EscapeDataString(from.Value.ToString("O"))).Append('&');
        if (to is not null)
            q.Append("to=").Append(Uri.EscapeDataString(to.Value.ToString("O"))).Append('&');
        q.Append("includeDiscarded=").Append(includeDiscarded ? "true" : "false");

        return await ForwardGetAsync(q.ToString(), cancellationToken).ConfigureAwait(false);
    }

    [HttpPost]
    public async Task<IActionResult> OrderDlqRetry([FromBody] DlqIdRequest body, CancellationToken cancellationToken)
    {
        if (!TryAuthorizeSuperAdmin(out var forbid))
            return forbid!;
        if (body is null)
            return BadRequest("body required.");

        return await ForwardPostAsync($"/api/v1/admin/orders/dlq/{body.Id}/retry", null, cancellationToken).ConfigureAwait(false);
    }

    [HttpPost]
    public async Task<IActionResult> OrderDlqDiscard([FromBody] DlqDiscardRequest body, CancellationToken cancellationToken)
    {
        if (!TryAuthorizeSuperAdmin(out var forbid))
            return forbid!;
        if (body is null)
            return BadRequest("body required.");

        var json = new JObject { ["reason"] = body.Reason ?? "" }.ToString(Newtonsoft.Json.Formatting.None);
        return await ForwardPostAsync($"/api/v1/admin/orders/dlq/{body.Id}/discard", json, cancellationToken).ConfigureAwait(false);
    }

    private bool TryAuthorizeSuperAdmin(out IActionResult? forbid)
    {
        forbid = null;
        var user = _backOfficeSecurity.BackOfficeSecurity?.CurrentUser;
        if (user is null)
        {
            forbid = Unauthorized();
            return false;
        }

        var iUser = BackOfficeUserResolver.GetCurrentIUser(_backOfficeSecurity, _userService);
        var roles = CatalogBackofficeRoleMapping.GetDcmsRolesForCatalogJwt(iUser);
        if (!roles.Contains(DcmsRoles.SuperAdmin))
        {
            forbid = Forbid();
            return false;
        }

        return true;
    }

    private async Task<IActionResult> ForwardGetAsync(string pathAndQuery, CancellationToken cancellationToken)
    {
        var token = CreateJwtOrThrow();
        var baseUrl = _orderOptions.Value.OrderApiBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}{pathAndQuery}";
        var client = _httpClientFactory.CreateClient();
        using var msg = new HttpRequestMessage(HttpMethod.Get, url);
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        AddScopeHeaders(msg);
        using var resp = await client.SendAsync(msg, cancellationToken).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new ContentResult
        {
            StatusCode = (int)resp.StatusCode,
            Content = body,
            ContentType = resp.Content.Headers.ContentType?.ToString() ?? "application/json",
        };
    }

    private async Task<IActionResult> ForwardPostAsync(string path, string? jsonBody, CancellationToken cancellationToken)
    {
        var token = CreateJwtOrThrow();
        var baseUrl = _orderOptions.Value.OrderApiBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}{path}";
        var client = _httpClientFactory.CreateClient();
        using var msg = new HttpRequestMessage(HttpMethod.Post, url);
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        AddScopeHeaders(msg);
        if (jsonBody is not null)
            msg.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var resp = await client.SendAsync(msg, cancellationToken).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new ContentResult
        {
            StatusCode = (int)resp.StatusCode,
            Content = body,
            ContentType = resp.Content.Headers.ContentType?.ToString() ?? "application/json",
        };
    }

    private string CreateJwtOrThrow()
    {
        var user = _backOfficeSecurity.BackOfficeSecurity?.CurrentUser
            ?? throw new InvalidOperationException("Backoffice user required.");
        var iUser = BackOfficeUserResolver.GetCurrentIUser(_backOfficeSecurity, _userService);
        var tenant = _orderOptions.Value.DefaultTenantId.Trim();
        var store = _orderOptions.Value.DefaultStoreId.Trim();
        return _jwtIssuer.CreateForBackOfficeUser(iUser, user.Id.ToString(), tenant, store);
    }

    private void AddScopeHeaders(HttpRequestMessage msg)
    {
        msg.Headers.TryAddWithoutValidation("X-Tenant-Id", _orderOptions.Value.DefaultTenantId.Trim());
        msg.Headers.TryAddWithoutValidation("X-Store-Id", _orderOptions.Value.DefaultStoreId.Trim());
    }

    public sealed record DlqIdRequest(long Id);

    public sealed record DlqDiscardRequest(long Id, string? Reason);
}
