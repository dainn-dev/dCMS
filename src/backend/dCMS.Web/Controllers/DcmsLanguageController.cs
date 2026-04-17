using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;

namespace dCMS.Web.Controllers;

/// <summary>
/// Lightweight public API that exposes language metadata to the dCMS React SPA.
/// Language codes/names are non-sensitive public configuration data — no auth required.
/// </summary>
[ApiController]
[Route("umbraco/dcms/api/language")]
[AllowAnonymous]
public sealed class DcmsLanguageController : ControllerBase
{
    private readonly ILanguageService _languageService;

    public DcmsLanguageController(ILanguageService languageService)
    {
        _languageService = languageService;
    }

    /// <summary>GET /umbraco/dcms/api/language/all</summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var languages = await _languageService.GetAllAsync();

        var items = languages.Select(l => new
        {
            isoCode = l.IsoCode,
            name = l.CultureName,
            isDefault = l.IsDefault,
            isMandatory = l.IsMandatory,
        });

        return Ok(new { items });
    }
}
