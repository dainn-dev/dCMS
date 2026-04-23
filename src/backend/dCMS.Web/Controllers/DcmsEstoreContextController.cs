using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace dCMS.Web.Controllers;

/// <summary>
/// Optional bootstrap for the eStore SPA: tenant/store ids for Catalog API calls.
/// Values come from configuration (<c>Dcms:Estore:TenantId</c>, <c>StoreId</c>) — set per environment.
/// Does not expose secrets; Bearer tokens for the gateway must be supplied by the host when auth is enabled.
/// </summary>
[ApiController]
[Route("umbraco/dcms/api/estore")]
[AllowAnonymous]
public sealed class DcmsEstoreContextController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public DcmsEstoreContextController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>GET /umbraco/dcms/api/estore/context</summary>
    [HttpGet("context")]
    public IActionResult GetContext()
    {
        var section = _configuration.GetSection("Dcms:Estore");
        var tenantId = section["TenantId"];
        var storeId = section["StoreId"];

        return Ok(new
        {
            tenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim(),
            storeId = string.IsNullOrWhiteSpace(storeId) ? null : storeId.Trim(),
        });
    }
}
