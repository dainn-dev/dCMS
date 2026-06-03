using System.Text.Json;
using System.Text.RegularExpressions;
using dCMS.AspNetCore.Auth;
using dCMS.Catalog.Api.Http;
using dCMS.Core.Persistence;
using dCMS.Core.Search;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Catalog.Api.Stores;

/// <summary>
/// Store-scoped dynamic custom fields for Add/Edit Product (eStore → Product Configuration).
/// Route group: /api/v1/tenants/{tenantId}/stores/{storeId}/product-field-config
/// </summary>
public static partial class ProductFieldConfigRoutes
{
    private static readonly string[] ControlTypes =
    [
        "Text Box", "WYSIWYG (Text Area)", "Dropdown List", "Checkbox", "Date Picker", "Multiple Select",
    ];

    private static readonly string[] TargetPages = ["General", "Product Page", "Recommendations"];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex PropertyRegex();

    public static void MapProductFieldConfigRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();
        var g = app.MapGroup("/api/v1/tenants/{tenantId}/stores/{storeId}/product-field-config")
            .WithTags("catalog-product-field-config")
            .WithTenantAccess(configuration);

        Auth(g.MapGet("", GetConfig), auth, write: false);
        Auth(g.MapPut("", PutConfig), auth, write: true);
    }

    private static RouteHandlerBuilder Auth(RouteHandlerBuilder b, bool authEnabled, bool write) =>
        authEnabled
            ? b.RequireAuthorization(write ? DcmsPolicies.CatalogWrite : DcmsPolicies.CatalogRead)
            : b;

    public sealed record ProductFieldOptionDto(string Name, string Value);

    public sealed record ProductFieldDto(
        string? Id,
        bool Enabled,
        bool Required,
        string Property,
        string ColumnLabel,
        string FieldName,
        string ControlType,
        string TargetPage,
        ProductFieldOptionDto[]? Options);

    private sealed record PutRequest(ProductFieldDto[]? Fields);

    private static async Task<IResult> GetConfig(
        string tenantId,
        string storeId,
        IStoreProductFieldConfigPersistence store,
        CancellationToken cancellationToken)
    {
        var (json, updatedAt) = await store.GetFieldsWithUpdatedAtAsync(tenantId, storeId, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
            return ApiEnvelope.Ok(new { configured = false, fields = Array.Empty<ProductFieldDto>(), updatedAt = (DateTimeOffset?)null });

        try
        {
            var fields = JsonSerializer.Deserialize<ProductFieldDto[]>(json, Json) ?? [];
            return ApiEnvelope.Ok(new { configured = true, fields, updatedAt });
        }
        catch (JsonException)
        {
            return ApiEnvelope.Ok(new { configured = false, fields = Array.Empty<ProductFieldDto>(), updatedAt = (DateTimeOffset?)null });
        }
    }

    private static async Task<IResult> PutConfig(
        string tenantId,
        string storeId,
        [FromBody] PutRequest body,
        IStoreProductFieldConfigPersistence store,
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
    {
        var fields = body.Fields ?? [];
        if (!Validate(fields, out var error, out var normalized))
            return ApiEnvelope.Error("validation_error", error!, StatusCodes.Status400BadRequest);

        var now = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(normalized, Json);
        await store.UpsertFieldsJsonAsync(tenantId, storeId, json, now, cancellationToken).ConfigureAwait(false);

        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var backgroundSync = scope.ServiceProvider.GetRequiredService<ProductFieldConfigSyncService>();
                await backgroundSync.SyncStoreAfterConfigChangeAsync(tenantId, storeId, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Errors are logged inside ProductFieldConfigSyncService.
            }
        }, CancellationToken.None);

        return ApiEnvelope.Ok(new { configured = true, fields = normalized, updatedAt = now, reindexQueued = true });
    }

    private static bool Validate(ProductFieldDto[] fields, out string? error, out ProductFieldDto[] normalized)
    {
        error = null;
        normalized = [];

        var result = new List<ProductFieldDto>(fields.Length);
        var seenProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < fields.Length; i++)
        {
            var f = fields[i];
            var position = $"Field #{i + 1}";

            var property = (f.Property ?? string.Empty).Trim().ToLowerInvariant();
            var columnLabel = (f.ColumnLabel ?? string.Empty).Trim();
            var fieldName = (f.FieldName ?? string.Empty).Trim();
            var controlType = (f.ControlType ?? string.Empty).Trim();
            var targetPage = (f.TargetPage ?? string.Empty).Trim();

            if (property.Length == 0) { error = $"{position}: Property is required."; return false; }
            if (columnLabel.Length == 0) { error = $"{position}: Column label is required."; return false; }
            if (fieldName.Length == 0) { error = $"{position}: Field name is required."; return false; }

            if (!PropertyRegex().IsMatch(property))
            {
                error = $"{position}: Property \"{property}\" is invalid. Use lowercase letters, digits or underscores (e.g. warranty_period).";
                return false;
            }

            if (Array.IndexOf(ControlTypes, controlType) < 0)
            {
                error = $"{position}: Unknown control type \"{controlType}\".";
                return false;
            }

            if (Array.IndexOf(TargetPages, targetPage) < 0)
            {
                error = $"{position}: Unknown target page \"{targetPage}\".";
                return false;
            }

            if (!seenProperties.Add(property))
            {
                error = $"Duplicate property \"{property}\". Properties must be unique.";
                return false;
            }

            var needsOptions = controlType is "Dropdown List" or "Multiple Select";
            ProductFieldOptionDto[] options = [];
            if (needsOptions)
            {
                var cleaned = (f.Options ?? [])
                    .Select(o => new ProductFieldOptionDto((o.Name ?? string.Empty).Trim(), (o.Value ?? string.Empty).Trim()))
                    .Where(o => o.Name.Length > 0 || o.Value.Length > 0)
                    .ToArray();

                if (cleaned.Length == 0)
                {
                    error = $"{position} (\"{columnLabel}\"): {controlType} needs at least one option.";
                    return false;
                }

                var seenValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var o in cleaned)
                {
                    if (o.Name.Length == 0 || o.Value.Length == 0)
                    {
                        error = $"{position} (\"{columnLabel}\"): every option needs both a display name and a value.";
                        return false;
                    }

                    if (!seenValues.Add(o.Value))
                    {
                        error = $"{position} (\"{columnLabel}\"): duplicate option value \"{o.Value}\".";
                        return false;
                    }
                }

                options = cleaned;
            }

            var id = string.IsNullOrWhiteSpace(f.Id) ? $"pfld-{Guid.NewGuid():N}"[..16] : f.Id!.Trim();
            result.Add(new ProductFieldDto(
                id, f.Enabled, f.Required, property, columnLabel, fieldName, controlType, targetPage, options));
        }

        normalized = result.ToArray();
        return true;
    }
}
