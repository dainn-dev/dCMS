using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using dCMS.Web.CatalogProxy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common.Attributes;

namespace dCMS.Web.InventoryProxy;

/// <summary>BFF for Umbraco backoffice → Inventory.Api (warehouses + stock) with the same JWT shape as Catalog.</summary>
[PluginController("DcmsCatalog")]
[IsBackOffice]
public sealed class InventoryBackofficeProxyController : UmbracoAuthorizedJsonController
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<InventoryProxyOptions> _inventoryOptions;
    private readonly CatalogJwtIssuer _jwtIssuer;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurity;
    private readonly IUserService _userService;

    public InventoryBackofficeProxyController(
        IHttpClientFactory httpClientFactory,
        IOptions<InventoryProxyOptions> inventoryOptions,
        CatalogJwtIssuer jwtIssuer,
        IBackOfficeSecurityAccessor backOfficeSecurity,
        IUserService userService)
    {
        _httpClientFactory = httpClientFactory;
        _inventoryOptions = inventoryOptions;
        _jwtIssuer = jwtIssuer;
        _backOfficeSecurity = backOfficeSecurity;
        _userService = userService;
    }

    [HttpPost]
    public async Task<IActionResult> Forward([FromBody] InventoryForwardRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Method))
            return BadRequest("method is required.");

        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsPost(request.Method) && !HttpMethods.IsPut(request.Method) &&
            !HttpMethods.IsDelete(request.Method))
            return BadRequest("method must be GET, POST, PUT, or DELETE.");

        if (!InventoryProxyPathValidator.IsAllowed(request.Path ?? ""))
            return BadRequest("path is not allowed for Inventory proxy.");

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

        var baseUrl = _inventoryOptions.Value.InventoryApiBaseUrl.TrimEnd('/');
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
            if (request.Body is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } body)
                msg.Content = new StringContent(body.GetRawText(), Encoding.UTF8, "application/json");
        }

        var client = _httpClientFactory.CreateClient("dcmsInventory");
        using var response = await client.SendAsync(msg, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = responseBody,
            ContentType = "application/json"
        };
    }

    private bool TryResolveScope(string? tenantId, string? storeId, out string tenant, out string store,
        out IActionResult? badRequest)
    {
        tenant = (tenantId ?? "").Trim();
        store = (storeId ?? "").Trim();
        badRequest = null;
        if (tenant.Length == 0)
            tenant = (_inventoryOptions.Value.DefaultTenantId ?? "").Trim();
        if (store.Length == 0)
            store = (_inventoryOptions.Value.DefaultStoreId ?? "").Trim();

        if (tenant.Length is 0 or > 64 || store.Length is 0 or > 64)
        {
            badRequest = BadRequest("tenantId and storeId are required (query/body) or set DCMS:InventoryProxy defaults.");
            return false;
        }

        return true;
    }
}

public sealed class InventoryForwardRequest
{
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "";
    public string? TenantId { get; set; }
    public string? StoreId { get; set; }
    public JsonElement? Body { get; set; }
}
