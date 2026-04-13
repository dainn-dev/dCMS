using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common.Attributes;

namespace dCMS.Web.CatalogProxy;

/// <summary>BFF: Umbraco backoffice → Catalog.Api notification endpoints (DAI-297).</summary>
[PluginController("DcmsCatalog")]
[IsBackOffice]
public sealed class NotificationBellProxyController : UmbracoAuthorizedJsonController
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<CatalogProxyOptions> _catalogOptions;
    private readonly CatalogJwtIssuer _jwtIssuer;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurity;
    private readonly IUserService _userService;

    public NotificationBellProxyController(
        IHttpClientFactory httpClientFactory,
        IOptions<CatalogProxyOptions> catalogOptions,
        CatalogJwtIssuer jwtIssuer,
        IBackOfficeSecurityAccessor backOfficeSecurity,
        IUserService userService)
    {
        _httpClientFactory = httpClientFactory;
        _catalogOptions = catalogOptions;
        _jwtIssuer = jwtIssuer;
        _backOfficeSecurity = backOfficeSecurity;
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount([FromQuery] string? tenantId, [FromQuery] string? storeId,
        CancellationToken cancellationToken) =>
        await ForwardCatalogAsync(HttpMethod.Get, "notifications/unread-count", tenantId, storeId, null, cancellationToken)
            .ConfigureAwait(false);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? tenantId, [FromQuery] string? storeId, [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var q = limit is > 0 and <= 100 ? $"?limit={limit.Value}" : "?limit=20";
        return await ForwardCatalogAsync(HttpMethod.Get, "notifications" + q, tenantId, storeId, null, cancellationToken)
            .ConfigureAwait(false);
    }

    [HttpPatch]
    public async Task<IActionResult> ReadAll([FromQuery] string? tenantId, [FromQuery] string? storeId,
        CancellationToken cancellationToken) =>
        await ForwardCatalogAsync(HttpMethod.Patch, "notifications/read-all", tenantId, storeId, null, cancellationToken)
            .ConfigureAwait(false);

    private async Task<IActionResult> ForwardCatalogAsync(HttpMethod method, string pathUnderStore, string? tenantId,
        string? storeId, HttpContent? content, CancellationToken cancellationToken)
    {
        if (!TryResolveScope(tenantId, storeId, out var tenant, out var store, out var badRequest))
            return badRequest!;

        var user = _backOfficeSecurity.BackOfficeSecurity?.CurrentUser;
        if (user is null)
            return Unauthorized();

        var subject = user.Id.ToString();
        var iUser = BackOfficeUserResolver.GetCurrentIUser(_backOfficeSecurity, _userService);
        string token;
        try
        {
            token = _jwtIssuer.CreateForBackOfficeUser(iUser, subject, tenant, store);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }

        var baseUrl = _catalogOptions.Value.CatalogApiBaseUrl.TrimEnd('/');
        var url =
            $"{baseUrl}/api/v1/tenants/{Uri.EscapeDataString(tenant)}/stores/{Uri.EscapeDataString(store)}/{pathUnderStore.TrimStart('/')}";
        using var msg = new HttpRequestMessage(method, url);
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        msg.Headers.TryAddWithoutValidation("X-Tenant-Id", tenant);
        msg.Headers.TryAddWithoutValidation("X-Store-Id", store);
        if (Request.Headers.TryGetValue("Idempotency-Key", out var idem) && !StringValues.IsNullOrEmpty(idem))
            msg.Headers.TryAddWithoutValidation("Idempotency-Key", idem.ToString());
        if (content is not null)
            msg.Content = content;

        var client = _httpClientFactory.CreateClient("dcmsCatalog");
        using var response = await client.SendAsync(msg, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = body,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json"
        };
    }

    private bool TryResolveScope(string? tenantId, string? storeId, out string tenant, out string store,
        out IActionResult? badRequest)
    {
        tenant = (tenantId ?? "").Trim();
        store = (storeId ?? "").Trim();
        badRequest = null;
        if (tenant.Length == 0)
            tenant = (_catalogOptions.Value.DefaultTenantId ?? "").Trim();
        if (store.Length == 0)
            store = (_catalogOptions.Value.DefaultStoreId ?? "").Trim();

        if (tenant.Length is 0 or > 64 || store.Length is 0 or > 64)
        {
            badRequest = BadRequest("tenantId and storeId are required (query) or set DCMS:CatalogProxy defaults.");
            return false;
        }

        return true;
    }
}
