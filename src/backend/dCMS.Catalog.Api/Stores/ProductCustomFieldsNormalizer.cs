using System.Text.Json;
using dCMS.Core.Persistence;

namespace dCMS.Catalog.Api.Stores;

/// <summary>
/// Validates and normalizes posted custom field values against the store's Product Configuration schema.
/// </summary>
internal static class ProductCustomFieldsNormalizer
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal sealed record FieldDef(
        string Id,
        bool Enabled,
        bool Required,
        string ControlType,
        string ColumnLabel,
        string FieldName,
        FieldOptionDef[] Options);

    internal sealed record FieldOptionDef(string Name, string Value);

    public static async Task<(bool Ok, string Json, string? Error)> NormalizeAsync(
        JsonElement? raw,
        string tenantId,
        string storeId,
        IStoreProductFieldConfigPersistence configStore,
        CancellationToken cancellationToken)
    {
        var defs = await LoadDefinitionsAsync(configStore, tenantId, storeId, cancellationToken).ConfigureAwait(false);
        var enabled = defs.Where(d => d.Enabled).ToArray();

        if (enabled.Length == 0)
            return (true, "{}", null);

        if (raw is null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
        {
            var missing = enabled.FirstOrDefault(d => d.Required);
            if (missing is not null)
                return (false, "{}", $"{Label(missing)} is required.");
            return (true, "{}", null);
        }

        if (raw.Value.ValueKind != JsonValueKind.Object)
            return (false, "{}", "customFields must be a JSON object.");

        var input = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var prop in raw.Value.EnumerateObject())
            input[prop.Name] = prop.Value;

        var output = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var field in enabled)
        {
            input.TryGetValue(field.Id, out var rawVal);
            if (!TryNormalizeValue(field, rawVal, out var normalized, out var error))
                return (false, "{}", error);

            var isEmpty = normalized switch
            {
                null => true,
                string s => s.Length == 0,
                string[] arr => arr.Length == 0,
                _ => false,
            };
            if (isEmpty)
            {
                if (field.Required)
                    return (false, "{}", $"{Label(field)} is required.");
                continue;
            }

            output[field.Id] = normalized;
        }

        return (true, JsonSerializer.Serialize(output, Json), null);
    }

    private static async Task<FieldDef[]> LoadDefinitionsAsync(
        IStoreProductFieldConfigPersistence configStore,
        string tenantId,
        string storeId,
        CancellationToken cancellationToken)
    {
        var json = await configStore.GetFieldsJsonAsync(tenantId, storeId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<FieldDef[]>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool TryNormalizeValue(FieldDef field, JsonElement rawVal, out object? normalized, out string? error)
    {
        normalized = null;
        error = null;
        var label = Label(field);

        switch (field.ControlType)
        {
            case "Checkbox":
                normalized = rawVal.ValueKind switch
                {
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "",
                    JsonValueKind.String => rawVal.GetString()?.Trim().ToLowerInvariant() is "true" or "1" or "yes" ? "true" : "",
                    _ => "",
                };
                return true;

            case "Multiple Select":
                if (rawVal.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    normalized = Array.Empty<string>();
                    return true;
                }

                if (rawVal.ValueKind != JsonValueKind.Array)
                {
                    error = $"{label}: expected an array of option values.";
                    return false;
                }

                var selected = new List<string>();
                foreach (var item in rawVal.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                    {
                        error = $"{label}: every selected value must be a string.";
                        return false;
                    }

                    var v = item.GetString()?.Trim() ?? "";
                    if (v.Length > 0) selected.Add(v);
                }

                var allowedMulti = field.Options.Select(o => o.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var v in selected)
                {
                    if (!allowedMulti.Contains(v))
                    {
                        error = $"{label}: unknown option value \"{v}\".";
                        return false;
                    }
                }

                normalized = selected.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                return true;

            case "Dropdown List":
                var str = rawVal.ValueKind switch
                {
                    JsonValueKind.String => rawVal.GetString()?.Trim() ?? "",
                    JsonValueKind.Null or JsonValueKind.Undefined => "",
                    _ => null,
                };
                if (str is null)
                {
                    error = $"{label}: expected a string option value.";
                    return false;
                }

                if (str.Length == 0)
                {
                    normalized = "";
                    return true;
                }

                var allowed = field.Options.Select(o => o.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!allowed.Contains(str))
                {
                    error = $"{label}: unknown option value \"{str}\".";
                    return false;
                }

                normalized = field.Options.First(o => string.Equals(o.Value, str, StringComparison.OrdinalIgnoreCase)).Value;
                return true;

            case "Date Picker":
                var dateStr = rawVal.ValueKind switch
                {
                    JsonValueKind.String => rawVal.GetString()?.Trim() ?? "",
                    JsonValueKind.Null or JsonValueKind.Undefined => "",
                    _ => null,
                };
                if (dateStr is null)
                {
                    error = $"{label}: expected a date string (YYYY-MM-DD).";
                    return false;
                }

                if (dateStr.Length > 0 && !DateOnly.TryParse(dateStr, out _))
                {
                    error = $"{label}: invalid date \"{dateStr}\".";
                    return false;
                }

                normalized = dateStr;
                return true;

            default:
                var text = rawVal.ValueKind switch
                {
                    JsonValueKind.String => rawVal.GetString()?.Trim() ?? "",
                    JsonValueKind.Null or JsonValueKind.Undefined => "",
                    _ => null,
                };
                if (text is null)
                {
                    error = $"{label}: expected a string value.";
                    return false;
                }

                normalized = text;
                return true;
        }
    }

    private static string Label(FieldDef field) =>
        string.IsNullOrWhiteSpace(field.ColumnLabel) ? field.FieldName : field.ColumnLabel;
}
