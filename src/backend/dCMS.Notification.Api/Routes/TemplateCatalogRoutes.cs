using System.Text.Json;
using dCMS.AspNetCore.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace dCMS.Notification.Api.Routes;

// ── Admin-managed catalog of message template types (tenant-scoped, DB-backed) ──
// Operators add/edit/remove message types and their variables from the UI.
// On first access per tenant the catalog is seeded from the "TemplateCatalog"
// config section (or built-in defaults) so there is always a starting point.

public sealed class TemplateCatalogOptions
{
    public List<TemplateCatalogEntryConfig> Entries { get; set; } = new();
}

public sealed class TemplateCatalogEntryConfig
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Key { get; set; } = "";
    public string Channel { get; set; } = "email";
    public List<TemplateCatalogVarConfig> Variables { get; set; } = new();
    public string? DefaultSubject { get; set; }
    public string? DefaultBody { get; set; }
}

public sealed class TemplateCatalogVarConfig
{
    public string Path { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Sample { get; set; }
}

public sealed class TemplateVarDto
{
    public string Path { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Sample { get; set; }
}

public sealed class TemplateDefinitionUpsertRequest
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Key { get; set; } = "";
    public string Channel { get; set; } = "email";
    public List<TemplateVarDto> Variables { get; set; } = new();
    public string? DefaultSubject { get; set; }
    public string? DefaultBody { get; set; }
}

public static class TemplateCatalogRoutes
{
    private static readonly JsonSerializerOptions JsonCamel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly string[] Channels = { "email", "sms", "print", "admin" };

    public static void MapTemplateCatalogRoutes(this WebApplication app)
    {
        app.MapGet("/api/v1/templates/catalog", GetCatalog)
            .WithTags("templates")
            .RequireAuthorization(DcmsPolicies.CatalogWrite)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapPut("/api/v1/templates/catalog", UpsertDefinition)
            .WithTags("templates")
            .RequireAuthorization(DcmsPolicies.CatalogWrite)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapDelete("/api/v1/templates/catalog", DeleteDefinition)
            .WithTags("templates")
            .RequireAuthorization(DcmsPolicies.CatalogWrite)
            .WithTenantStoreHeaderAccess(app.Configuration);
    }

    private static IResult Ok(object data) => Results.Json(new { data, meta = (object?)null, error = (object?)null }, JsonCamel);

    private static IResult Err(int status, string code, string message) =>
        Results.Json(new { data = (object?)null, meta = (object?)null, error = new { code, message } }, JsonCamel, statusCode: status);

    private static string? Tenant(HttpContext http) =>
        http.Request.Headers["X-Tenant-Id"].FirstOrDefault()?.Trim() is { Length: > 0 } t ? t : null;

    private static async Task<IResult> GetCatalog(
        HttpContext http,
        [FromServices] TemplateDefinitionRepository repo,
        [FromServices] IOptions<TemplateCatalogOptions> options,
        CancellationToken ct)
    {
        if (Tenant(http) is not { } tenantId)
            return Err(400, "MISSING_TENANT", "X-Tenant-Id header is required.");

        var defs = await repo.ListAsync(tenantId, ct).ConfigureAwait(false);
        if (defs.Count == 0)
        {
            await repo.SeedAsync(tenantId, SeedSource(options.Value), ct).ConfigureAwait(false);
            defs = await repo.ListAsync(tenantId, ct).ConfigureAwait(false);
        }

        var data = defs.Select(d => new
        {
            id = $"{d.Key}|{d.Channel}",
            name = d.Name,
            description = d.Description,
            key = d.Key,
            channel = d.Channel,
            variables = ParseVars(d.Variables),
            defaultSubject = d.DefaultSubject,
            defaultBody = d.DefaultBody,
        });
        return Ok(data);
    }

    private static async Task<IResult> UpsertDefinition(
        HttpContext http,
        [FromServices] TemplateDefinitionRepository repo,
        [FromBody] TemplateDefinitionUpsertRequest body,
        CancellationToken ct)
    {
        if (Tenant(http) is not { } tenantId)
            return Err(400, "MISSING_TENANT", "X-Tenant-Id header is required.");

        var key = body.Key?.Trim() ?? "";
        var channel = body.Channel?.Trim().ToLowerInvariant() ?? "";
        var name = body.Name?.Trim() ?? "";
        if (key.Length == 0 || name.Length == 0)
            return Err(400, "INVALID_BODY", "key and name are required.");
        if (!Channels.Contains(channel))
            return Err(400, "INVALID_CHANNEL", "channel must be one of email, sms, print, admin.");

        var vars = (body.Variables ?? new()).Where(v => !string.IsNullOrWhiteSpace(v.Path)).Select(v => new TemplateVarDto
        {
            Path = v.Path.Trim(),
            Label = string.IsNullOrWhiteSpace(v.Label) ? v.Path.Trim() : v.Label.Trim(),
            Sample = v.Sample,
        }).ToList();

        var actor = TemplateRepository.ActorUserId(http);
        await repo.UpsertAsync(
            tenantId, key, channel, name, body.Description?.Trim() ?? "",
            JsonSerializer.Serialize(vars, JsonCamel), body.DefaultSubject, body.DefaultBody, actor, ct).ConfigureAwait(false);

        return Ok(new { ok = true });
    }

    private static async Task<IResult> DeleteDefinition(
        HttpContext http,
        [FromServices] TemplateDefinitionRepository repo,
        [FromQuery] string? key,
        [FromQuery] string? channel,
        CancellationToken ct)
    {
        if (Tenant(http) is not { } tenantId)
            return Err(400, "MISSING_TENANT", "X-Tenant-Id header is required.");
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(channel))
            return Err(400, "INVALID_QUERY", "key and channel are required.");

        var deleted = await repo.DeleteAsync(tenantId, key.Trim(), channel.Trim(), ct).ConfigureAwait(false);
        return Ok(new { deleted });
    }

    private static List<TemplateVarDto> ParseVars(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<TemplateVarDto>>(json, JsonCamel) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static IReadOnlyList<(string Key, string Channel, string Name, string Description, string VariablesJson, string? DefaultSubject, string? DefaultBody)> SeedSource(TemplateCatalogOptions options)
    {
        var src = options.Entries.Count > 0 ? options.Entries : TemplateCatalogDefaults.Entries;
        return src.Select(e =>
        {
            var vars = e.Variables.Select(v => new TemplateVarDto { Path = v.Path, Label = v.Label, Sample = v.Sample }).ToList();
            return (e.Key, e.Channel, e.Name, e.Description, JsonSerializer.Serialize(vars, JsonCamel), e.DefaultSubject, e.DefaultBody);
        }).ToList();
    }
}

internal static class TemplateCatalogDefaults
{
    private static TemplateCatalogVarConfig V(string path, string label, string sample) => new() { Path = path, Label = label, Sample = sample };

    private static readonly List<TemplateCatalogVarConfig> OrderVars = new()
    {
        V("orderId", "Order number", "SO-100428"),
        V("customerName", "Customer name", "Jane Doe"),
        V("orderDate", "Order date", "4 Jun 2026"),
        V("total", "Order total", "$123.45"),
        V("storeName", "Store name", "Acme Mart"),
    };

    private static readonly List<TemplateCatalogVarConfig> ShipVars = new()
    {
        V("orderId", "Order number", "SO-100428"),
        V("customerName", "Customer name", "Jane Doe"),
        V("trackingNumber", "Tracking number", "VN1234567890"),
        V("carrier", "Carrier", "GHN Express"),
        V("storeName", "Store name", "Acme Mart"),
    };

    private static readonly List<TemplateCatalogVarConfig> AccountVars = new()
    {
        V("customerName", "Customer name", "Jane Doe"),
        V("storeName", "Store name", "Acme Mart"),
        V("actionUrl", "Action link", "https://shop.example.com/verify?t=abc"),
    };

    public static readonly List<TemplateCatalogEntryConfig> Entries = new()
    {
        new()
        {
            Name = "Order Confirmation",
            Description = "Sent to the customer right after they place an order.",
            Key = "order.confirmation",
            Channel = "email",
            Variables = OrderVars,
            DefaultSubject = "Your {{ model.storeName }} order {{ model.orderId }} is confirmed",
            DefaultBody =
                "<p>Hi {{ model.customerName }},</p>" +
                "<p>Thanks for your order <b>{{ model.orderId }}</b> placed on {{ model.orderDate }}.</p>" +
                "<p>Order total: <b>{{ model.total }}</b></p>" +
                "<p>We'll let you know when it ships.</p>",
        },
        new()
        {
            Name = "Shipping Notification",
            Description = "Sent when the customer's order has been dispatched.",
            Key = "order.shipped",
            Channel = "email",
            Variables = ShipVars,
            DefaultSubject = "Your order {{ model.orderId }} is on its way",
            DefaultBody =
                "<p>Hi {{ model.customerName }},</p>" +
                "<p>Good news — order <b>{{ model.orderId }}</b> has shipped with {{ model.carrier }}.</p>" +
                "<p>Tracking number: <b>{{ model.trackingNumber }}</b></p>",
        },
        new()
        {
            Name = "Order Cancellation",
            Description = "Sent when an order is cancelled.",
            Key = "order.cancelled",
            Channel = "email",
            Variables = OrderVars,
            DefaultSubject = "Your order {{ model.orderId }} has been cancelled",
            DefaultBody =
                "<p>Hi {{ model.customerName }},</p>" +
                "<p>Your order <b>{{ model.orderId }}</b> has been cancelled. Any payment will be refunded shortly.</p>",
        },
        new()
        {
            Name = "Welcome Email",
            Description = "Sent when a customer creates a new account.",
            Key = "account.welcome",
            Channel = "email",
            Variables = AccountVars,
            DefaultSubject = "Welcome to {{ model.storeName }}!",
            DefaultBody =
                "<p>Hi {{ model.customerName }},</p>" +
                "<p>Welcome to {{ model.storeName }}. We're glad to have you.</p>",
        },
        new()
        {
            Name = "Password Reset",
            Description = "Sent when a customer requests a password reset.",
            Key = "password.reset",
            Channel = "email",
            Variables = AccountVars,
            DefaultSubject = "Reset your {{ model.storeName }} password",
            DefaultBody =
                "<p>Hi {{ model.customerName }},</p>" +
                "<p>Click the link below to reset your password:</p>" +
                "<p><a href=\"{{ model.actionUrl }}\">Reset password</a></p>",
        },
    };
}
