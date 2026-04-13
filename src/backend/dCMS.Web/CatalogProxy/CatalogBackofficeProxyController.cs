using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common.Attributes;

namespace dCMS.Web.CatalogProxy;

/// <summary>BFF for Umbraco backoffice → Catalog.Api: public slug-check + authenticated forward (JWT with tenant/store).</summary>
[PluginController("DcmsCatalog")]
[IsBackOffice]
public sealed class CatalogBackofficeProxyController : UmbracoAuthorizedJsonController
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<CatalogProxyOptions> _catalogOptions;
    private readonly CatalogJwtIssuer _jwtIssuer;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurity;
    private readonly IUserService _userService;

    public CatalogBackofficeProxyController(
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

    /// <summary>Proxies anonymous Catalog storefront slug-check (no JWT).</summary>
    [HttpGet]
    public async Task<IActionResult> SlugCheck([FromQuery] string tenantId, [FromQuery] string storeId, [FromQuery] string slug,
        CancellationToken cancellationToken)
    {
        if (!TryResolveScope(tenantId, storeId, out var t, out var s, out var badRequest))
            return badRequest!;

        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 256)
            return BadRequest("slug is required (max 256 chars).");

        var baseUrl = _catalogOptions.Value.CatalogApiBaseUrl.TrimEnd('/');
        var url =
            $"{baseUrl}/api/v1/products/slug-check?tenantId={Uri.EscapeDataString(t)}&storeId={Uri.EscapeDataString(s)}&slug={Uri.EscapeDataString(slug.Trim())}";

        var client = _httpClientFactory.CreateClient("dcmsCatalog");
        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = body,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json"
        };
    }

    /// <summary>Roles embedded in Catalog JWT for the current Umbraco user (DAI-296 UI gating).</summary>
    [HttpGet]
    public IActionResult ForwardIdentity()
    {
        var user = BackOfficeUserResolver.GetCurrentIUser(_backOfficeSecurity, _userService);
        if (user is null)
            return Unauthorized();

        var roles = CatalogBackofficeRoleMapping.GetDcmsRolesForCatalogJwt(user);
        return Ok(new
        {
            data = new
            {
                roles,
                canRunCatalogApprovalActions = CatalogBackofficeRoleMapping.CanRunCatalogApprovalActions(roles),
                canCatalogWrite = CatalogBackofficeRoleMapping.CanCatalogWrite(roles)
            },
            error = (object?)null
        });
    }

    /// <summary>Forward GET/POST/PUT/DELETE to <c>/api/v1/tenants/{tenant}/stores/{store}/products…</c> with Catalog JWT.</summary>
    [HttpPost]
    public async Task<IActionResult> Forward([FromBody] CatalogForwardRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Method))
            return BadRequest("method is required.");

        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsPost(request.Method) && !HttpMethods.IsPut(request.Method) &&
            !HttpMethods.IsDelete(request.Method) && !HttpMethods.IsPatch(request.Method))
            return BadRequest("method must be GET, POST, PUT, PATCH, or DELETE.");

        if (!CatalogProxyPathValidator.IsAllowed(request.Path ?? ""))
            return BadRequest("path is not allowed for proxy (products/*, categories, variant-axes, store-catalog-settings).");

        if (!TryResolveScope(request.TenantId, request.StoreId, out var tenant, out var store, out var badRequest))
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
        var path = request.Path!.Trim().TrimStart('/');
        var url = $"{baseUrl}/api/v1/tenants/{Uri.EscapeDataString(tenant)}/stores/{Uri.EscapeDataString(store)}/{path}";

        using var msg = new HttpRequestMessage(new HttpMethod(request.Method.ToUpperInvariant()), url);
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        msg.Headers.TryAddWithoutValidation("X-Tenant-Id", tenant);
        msg.Headers.TryAddWithoutValidation("X-Store-Id", store);
        if (Request.Headers.TryGetValue("Idempotency-Key", out var idem) && !StringValues.IsNullOrEmpty(idem))
            msg.Headers.TryAddWithoutValidation("Idempotency-Key", idem.ToString());

        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsDelete(request.Method))
        {
            if (!string.IsNullOrEmpty(request.BinaryBodyBase64))
            {
                byte[] bytes;
                try
                {
                    bytes = WebEncoders.Base64UrlDecode(request.BinaryBodyBase64);
                }
                catch
                {
                    bytes = Convert.FromBase64String(request.BinaryBodyBase64);
                }

                var ct = string.IsNullOrWhiteSpace(request.BinaryContentType)
                    ? "application/octet-stream"
                    : request.BinaryContentType.Trim();
                msg.Content = new ByteArrayContent(bytes);
                msg.Content.Headers.ContentType = new MediaTypeHeaderValue(ct);
            }
            else if (request.Body is not null && request.Body.Type != JTokenType.Null && request.Body.Type != JTokenType.Undefined)
            {
                msg.Content = new StringContent(request.Body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
            }
        }

        var client = _httpClientFactory.CreateClient("dcmsCatalog");
        using var response = await client.SendAsync(msg, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = responseBody,
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
            badRequest = BadRequest("tenantId and storeId are required (query/body) or set DCMS:CatalogProxy defaults.");
            return false;
        }

        return true;
    }
}

public sealed class CatalogForwardRequest
{
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "";
    public string? TenantId { get; set; }
    public string? StoreId { get; set; }
    public JToken? Body { get; set; }

    /// <summary>Optional raw body for PUT uploads (US-14 images). Standard base64 or base64url.</summary>
    public string? BinaryBodyBase64 { get; set; }

    public string? BinaryContentType { get; set; }
}
