using System.Text.Json;
using dCMS.AspNetCore.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace dCMS.Notification.Api.Routes;

// ── Config-bound catalog of editable notification templates ──────────────────
// Ops can add/remove template types and their variables by editing the
// "TemplateCatalog" config section (appsettings / env / mounted file) and
// restarting the service — no frontend rebuild required. When the section is
// empty the built-in defaults below are served.

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

public static class TemplateCatalogRoutes
{
    private static readonly JsonSerializerOptions JsonCamel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static void MapTemplateCatalogRoutes(this WebApplication app)
    {
        app.MapGet("/api/v1/templates/catalog", GetCatalog)
            .WithTags("templates")
            .RequireAuthorization(DcmsPolicies.CatalogWrite);
    }

    private static IResult GetCatalog([FromServices] IOptions<TemplateCatalogOptions> options)
    {
        var src = options.Value.Entries.Count > 0 ? options.Value.Entries : TemplateCatalogDefaults.Entries;
        var data = src.Select(e => new
        {
            id = $"{e.Key}|{e.Channel}",
            name = e.Name,
            description = e.Description,
            key = e.Key,
            channel = e.Channel,
            variables = e.Variables.Select(v => new { path = v.Path, label = v.Label, sample = v.Sample ?? "" }),
            defaultSubject = e.DefaultSubject,
            defaultBody = e.DefaultBody,
        });
        return Results.Json(new { data, meta = (object?)null, error = (object?)null }, JsonCamel);
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
