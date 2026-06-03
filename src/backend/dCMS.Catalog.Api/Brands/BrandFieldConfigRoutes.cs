using System.Text.Json;
using System.Text.RegularExpressions;
using dCMS.AspNetCore.Auth;
using dCMS.Catalog.Api.Http;
using dCMS.Core.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Catalog.Api.Brands;

/// <summary>
/// Tenant-scoped configuration of the dynamic "additional fields" rendered on the
/// Add/Edit Brand form (eStore → Brand Configuration page).
/// Route group: /api/v1/tenants/{tenantId}/brand-field-config
/// Auth: CatalogRead (GET), CatalogWrite (PUT).
/// </summary>
public static partial class BrandFieldConfigRoutes
{
    private static readonly string[] ControlTypes =
    [
        "Text Box", "WYSIWYG (Text Area)", "Dropdown List", "Checkbox", "Date Picker", "Multiple Select",
    ];

    private static readonly string[] Sections =
    [
        "General Information", "Contacts", "Product Recommendations", "SEO Configuration",
    ];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex FieldNameRegex();

    public static void MapBrandFieldConfigRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();

        var g = app.MapGroup("/api/v1/tenants/{tenantId}/brand-field-config")
            .WithTags("catalog-brand-field-config")
            .WithTenantAccess(configuration);

        Auth(g.MapGet("", GetConfig), auth, write: false);
        Auth(g.MapPut("", PutConfig), auth, write: true);
    }

    private static RouteHandlerBuilder Auth(RouteHandlerBuilder b, bool authEnabled, bool write) =>
        authEnabled
            ? b.RequireAuthorization(write ? DcmsPolicies.CatalogWrite : DcmsPolicies.CatalogRead)
            : b;

    // ── DTOs ───────────────────────────────────────────────────────────────────

    public sealed record BrandFieldOptionDto(string Name, string Value);

    public sealed record BrandFieldDto(
        string? Id,
        bool Enabled,
        bool Required,
        string Property,
        string ColumnLabel,
        string FieldName,
        string ControlType,
        string Section,
        BrandFieldOptionDto[]? Options);

    private sealed record PutRequest(BrandFieldDto[]? Fields);

    // ── Handlers ─────────────────────────────────────────────────────────────

    /// <summary>GET /api/v1/tenants/{tenantId}/brand-field-config</summary>
    private static async Task<IResult> GetConfig(
        string tenantId,
        IBrandFieldConfigPersistence store,
        CancellationToken cancellationToken = default)
    {
        var json = await store.GetFieldsJsonAsync(tenantId, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json))
            return ApiEnvelope.Ok(new { configured = false, fields = Array.Empty<BrandFieldDto>() });

        BrandFieldDto[] fields;
        try
        {
            fields = JsonSerializer.Deserialize<BrandFieldDto[]>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            // Corrupt row — treat as unconfigured rather than 500 so the UI can recover.
            fields = [];
            return ApiEnvelope.Ok(new { configured = false, fields });
        }

        return ApiEnvelope.Ok(new { configured = true, fields });
    }

    /// <summary>PUT /api/v1/tenants/{tenantId}/brand-field-config</summary>
    private static async Task<IResult> PutConfig(
        string tenantId,
        [FromBody] PutRequest body,
        IBrandFieldConfigPersistence store,
        CancellationToken cancellationToken = default)
    {
        var fields = body.Fields ?? [];

        if (Validate(fields, out var error, out var normalized))
        {
            var json = JsonSerializer.Serialize(normalized, Json);
            await store.UpsertFieldsJsonAsync(tenantId, json, DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new { configured = true, fields = normalized });
        }

        return ApiEnvelope.Error("validation_error", error!, StatusCodes.Status400BadRequest);
    }

    // ── Validation ─────────────────────────────────────────────────────────────

    private static bool Validate(BrandFieldDto[] fields, out string? error, out BrandFieldDto[] normalized)
    {
        error = null;
        normalized = [];

        var result = new List<BrandFieldDto>(fields.Length);
        var seenFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < fields.Length; i++)
        {
            var f = fields[i];
            var position = $"Field #{i + 1}";

            var property = (f.Property ?? string.Empty).Trim();
            var columnLabel = (f.ColumnLabel ?? string.Empty).Trim();
            var fieldName = (f.FieldName ?? string.Empty).Trim();
            var controlType = (f.ControlType ?? string.Empty).Trim();
            var section = (f.Section ?? string.Empty).Trim();

            if (property.Length == 0) { error = $"{position}: Property is required."; return false; }
            if (columnLabel.Length == 0) { error = $"{position}: Column label is required."; return false; }
            if (fieldName.Length == 0) { error = $"{position}: Field name is required."; return false; }

            if (!FieldNameRegex().IsMatch(fieldName))
            {
                error = $"{position}: Field name \"{fieldName}\" is invalid. Use a letter followed by letters, digits or underscores (e.g. loyaltyTier).";
                return false;
            }

            if (Array.IndexOf(ControlTypes, controlType) < 0)
            {
                error = $"{position}: Unknown control type \"{controlType}\".";
                return false;
            }

            if (Array.IndexOf(Sections, section) < 0)
            {
                error = $"{position}: Unknown group heading \"{section}\".";
                return false;
            }

            if (!seenFieldNames.Add(fieldName))
            {
                error = $"Duplicate field name \"{fieldName}\". Field names must be unique.";
                return false;
            }

            if (!seenProperties.Add(property))
            {
                error = $"Duplicate property \"{property}\". Properties must be unique.";
                return false;
            }

            var needsOptions = controlType is "Dropdown List" or "Multiple Select";
            BrandFieldOptionDto[] options = [];
            if (needsOptions)
            {
                var cleaned = (f.Options ?? [])
                    .Select(o => new BrandFieldOptionDto((o.Name ?? string.Empty).Trim(), (o.Value ?? string.Empty).Trim()))
                    .Where(o => o.Name.Length > 0 || o.Value.Length > 0)
                    .ToArray();

                if (cleaned.Length == 0)
                {
                    error = $"{position} (\"{columnLabel}\"): {controlType} needs at least one option.";
                    return false;
                }

                var seenOptionValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var o in cleaned)
                {
                    if (o.Name.Length == 0 || o.Value.Length == 0)
                    {
                        error = $"{position} (\"{columnLabel}\"): every option needs both a display name and a value.";
                        return false;
                    }
                    if (!seenOptionValues.Add(o.Value))
                    {
                        error = $"{position} (\"{columnLabel}\"): duplicate option value \"{o.Value}\".";
                        return false;
                    }
                }
                options = cleaned;
            }

            var id = string.IsNullOrWhiteSpace(f.Id) ? $"baf-{Guid.NewGuid():N}"[..12] : f.Id!.Trim();

            result.Add(new BrandFieldDto(
                id, f.Enabled, f.Required, property, columnLabel, fieldName, controlType, section, options));
        }

        normalized = result.ToArray();
        return true;
    }
}
