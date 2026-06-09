using System.Text.Json;
using dCMS.AspNetCore.Auth;
using dCMS.Notification.Api.Rendering;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Notification.Api.Routes;

public static class TemplateRoutes
{
    private static readonly JsonSerializerOptions JsonCamel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static void MapTemplateRoutes(this WebApplication app)
    {
        app.MapGet("/api/v1/templates", ListTemplates)
            .WithTags("templates")
            .RequireAuthorization(DcmsPolicies.CatalogWrite)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapGet("/api/v1/templates/resolved", GetResolved)
            .WithTags("templates")
            .RequireAuthorization(DcmsPolicies.CatalogWrite)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapPut("/api/v1/templates", PutTemplate)
            .WithTags("templates")
            .RequireAuthorization(DcmsPolicies.CatalogWrite)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapDelete("/api/v1/templates", DeleteTemplate)
            .WithTags("templates")
            .RequireAuthorization(DcmsPolicies.CatalogWrite)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapPost("/api/v1/templates/preview", PreviewTemplate)
            .WithTags("templates")
            .RequireAuthorization(DcmsPolicies.CatalogWrite)
            .WithTenantStoreHeaderAccess(app.Configuration);
    }

    private static IResult Ok(object data, object? meta = null) =>
        Results.Json(new { data, meta, error = (object?)null }, JsonCamel);

    private static IResult Err(int statusCode, string code, string message) =>
        Results.Json(new { data = (object?)null, meta = (object?)null, error = new { code, message } }, JsonCamel, statusCode: statusCode);

    private static bool TryTenantStore(HttpContext http, out string tenantId, out string storeId)
    {
        tenantId = http.Request.Headers["X-Tenant-Id"].FirstOrDefault()?.Trim() ?? "";
        storeId = http.Request.Headers["X-Store-Id"].FirstOrDefault()?.Trim() ?? "";
        return tenantId.Length > 0;
    }

    private static async Task<IResult> ListTemplates(
        HttpContext http,
        [FromServices] TemplateRepository repo,
        CancellationToken ct)
    {
        if (!TryTenantStore(http, out var tenantId, out _))
            return Err(400, "MISSING_TENANT", "X-Tenant-Id header is required.");

        // Listing shows tenant + global defaults; tenantId is only used as filter.
        var rows = await repo.ListAsync(tenantId, ct).ConfigureAwait(false);
        return Ok(rows);
    }

    private static async Task<IResult> GetResolved(
        HttpContext http,
        [FromServices] TemplateRepository repo,
        [FromQuery] string? key,
        [FromQuery] string? locale,
        [FromQuery] string? channel,
        [FromQuery] string? defaultLocale,
        CancellationToken ct)
    {
        if (!TryTenantStore(http, out var tenantId, out _))
            return Err(400, "MISSING_TENANT", "X-Tenant-Id header is required.");
        if (string.IsNullOrWhiteSpace(key))
            return Err(400, "MISSING_KEY", "key is required.");

        var loc = string.IsNullOrWhiteSpace(locale) ? "en-US" : locale.Trim();
        var def = string.IsNullOrWhiteSpace(defaultLocale) ? "en-US" : defaultLocale.Trim();
        var ch = string.IsNullOrWhiteSpace(channel) ? "email" : channel.Trim();

        var row = await repo.GetResolvedAsync(tenantId, key.Trim(), loc, ch, def, ct).ConfigureAwait(false);
        return row is null ? Err(404, "NOT_FOUND", "No template found for this key/channel.") : Ok(row);
    }

    private static async Task<IResult> PutTemplate(
        HttpContext http,
        [FromServices] TemplateRepository repo,
        [FromBody] TemplateUpsertRequest body,
        CancellationToken ct)
    {
        if (!TryTenantStore(http, out var tenantId, out var storeId))
            return Err(400, "MISSING_TENANT", "X-Tenant-Id header is required.");
        if (string.IsNullOrWhiteSpace(body.Key) || string.IsNullOrWhiteSpace(body.Channel) || string.IsNullOrWhiteSpace(body.Body))
            return Err(400, "INVALID_BODY", "key, channel, body are required.");

        var actor = TemplateRepository.ActorUserId(http);
        // Role claim type varies by inbound claim mapping (ClaimTypes.Role vs literal "role"/"roles").
        var role = http.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
            ?? http.User.FindFirst("role")?.Value
            ?? http.User.FindFirst("roles")?.Value
            ?? "unknown";
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Tenant override: if client omitted TenantId, default to current tenant.
        body.TenantId = string.IsNullOrWhiteSpace(body.TenantId) ? tenantId : body.TenantId.Trim();

        await repo.UpsertAsync(body, actor, tenantId, string.IsNullOrWhiteSpace(storeId) ? "n/a" : storeId, role, ip, ct)
            .ConfigureAwait(false);
        return Ok(new { ok = true });
    }

    private static async Task<IResult> DeleteTemplate(
        HttpContext http,
        [FromServices] TemplateRepository repo,
        [FromQuery] string? key,
        [FromQuery] string? locale,
        [FromQuery] string? channel,
        CancellationToken ct)
    {
        if (!TryTenantStore(http, out var tenantId, out _))
            return Err(400, "MISSING_TENANT", "X-Tenant-Id header is required.");
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(locale) || string.IsNullOrWhiteSpace(channel))
            return Err(400, "INVALID_QUERY", "key, locale, channel are required.");

        var deleted = await repo.DeleteAsync(tenantId, key.Trim(), locale.Trim(), channel.Trim(), ct).ConfigureAwait(false);
        return Ok(new { deleted });
    }

    private static Task<IResult> PreviewTemplate(
        HttpContext http,
        [FromServices] ITemplateRenderer renderer,
        [FromBody] PreviewRequest body,
        CancellationToken ct)
    {
        if (!TryTenantStore(http, out var tenantId, out _))
            return Task.FromResult<IResult>(Err(400, "MISSING_TENANT", "X-Tenant-Id header is required."));
        if (string.IsNullOrWhiteSpace(body.Key) || string.IsNullOrWhiteSpace(body.Locale) || string.IsNullOrWhiteSpace(body.Channel))
            return Task.FromResult<IResult>(Err(400, "INVALID_BODY", "key, locale, channel are required."));

        return PreviewImpl(renderer, tenantId, body, ct);
    }

    private static async Task<IResult> PreviewImpl(ITemplateRenderer renderer, string tenantId, PreviewRequest req, CancellationToken ct)
    {
        // For preview, use supplied subject/body inline when present; otherwise resolve from DB (RenderAsync).
        // Keep v1 simple: upsert first then preview is also acceptable via UI.
        var model = req.SampleModel;
        if (!model.HasValue)
        {
            // empty JSON object
            using var doc = JsonDocument.Parse("{}");
            model = doc.RootElement.Clone();
        }

        // If inline body provided, render that as an ad-hoc template (no DB read).
        if (!string.IsNullOrWhiteSpace(req.Body))
        {
            var r = await AdhocRenderAsync(req, model, ct).ConfigureAwait(false);
            return Ok(r);
        }

        var result = await renderer.RenderAsync(tenantId, req.Key.Trim(), req.Locale.Trim(), req.Channel.Trim(), model, ct)
            .ConfigureAwait(false);
        return Ok(new { subject = result.Subject ?? "(no subject)", body = result.Body });
    }

    private static Task<object> AdhocRenderAsync(PreviewRequest req, object model, CancellationToken ct)
    {
        _ = ct;
        var bodyTpl = Scriban.Template.Parse(req.Body!);
        var subjTpl = string.IsNullOrWhiteSpace(req.Subject) ? null : Scriban.Template.Parse(req.Subject!);

        var ctx = new Scriban.TemplateContext { EnableRelaxedMemberAccess = false, LoopLimit = 10_000, RecursiveLimit = 128 };
        ctx.CancellationToken = ct;

        // Only allow JSON-like model object. In preview, accept JsonElement or serialize anonymous objects.
        JsonElement json;
        if (model is JsonElement je) json = je;
        else
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(model, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            using var doc = JsonDocument.Parse(bytes);
            json = doc.RootElement.Clone();
        }

        ctx.PushGlobal(ScribanModel.FromJsonElement(json));

        return Task.FromResult<object>(new
        {
            subject = subjTpl is null ? "(preview)" : subjTpl.Render(ctx),
            body = bodyTpl.Render(ctx),
        });
    }
}

public sealed class PreviewRequest
{
    public string Key { get; set; } = "";
    public string Locale { get; set; } = "en-US";
    public string Channel { get; set; } = "email";
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public JsonElement? SampleModel { get; set; }
}

