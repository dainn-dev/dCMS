using System.Text.Json;

namespace dCMS.Core.Search;

/// <summary>
/// Maps store product-field config + per-product values to index attributes and public storefront DTOs.
/// Values in DB are keyed by field id; storefront uses stable <see cref="FieldDef.Property"/> keys.
/// </summary>
public static class ProductCustomFieldsMapper
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public sealed record FieldOption(string Name, string Value);

    public sealed record FieldDef(
        string Id,
        bool Enabled,
        bool Required,
        string Property,
        string ColumnLabel,
        string FieldName,
        string ControlType,
        string TargetPage,
        FieldOption[] Options);

    public sealed record PublicField(
        string Property,
        string ColumnLabel,
        string FieldName,
        string ControlType,
        string TargetPage,
        object? Value);

    public static IReadOnlyList<FieldDef> ParseDefinitions(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<FieldDef[]>(configJson, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static Dictionary<string, string> ParseValuesByFieldId(string? valuesJson)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(valuesJson))
            return map;

        try
        {
            using var doc = JsonDocument.Parse(valuesJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return map;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                map[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Array => string.Join(",",
                        prop.Value.EnumerateArray()
                            .Where(e => e.ValueKind == JsonValueKind.String)
                            .Select(e => e.GetString())
                            .Where(s => !string.IsNullOrWhiteSpace(s))),
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    _ => prop.Value.GetRawText()
                };
            }
        }
        catch (JsonException)
        {
            // ignore malformed values
        }

        return map;
    }

    /// <summary>Flatten enabled custom fields into ES <c>attributes</c> keyed by property (search/filter).</summary>
    public static Dictionary<string, string> ToIndexAttributes(IReadOnlyList<FieldDef> defs, string? valuesJson)
    {
        var values = ParseValuesByFieldId(valuesJson);
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in defs.Where(static f => f.Enabled && !string.IsNullOrWhiteSpace(f.Property)))
        {
            if (!values.TryGetValue(field.Id, out var raw) || string.IsNullOrWhiteSpace(raw))
                continue;
            attrs[field.Property.Trim().ToLowerInvariant()] = raw.Trim();
        }

        return attrs;
    }

    /// <summary>Resolve public storefront fields (labels + typed values).</summary>
    public static IReadOnlyList<PublicField> ToPublicFields(
        IReadOnlyList<FieldDef> defs,
        string? valuesJson,
        Func<FieldDef, bool>? includeField = null)
    {
        includeField ??= static _ => true;
        var values = ParseValuesByFieldId(valuesJson);
        var list = new List<PublicField>();

        foreach (var field in defs.Where(f => f.Enabled && includeField(f)))
        {
            values.TryGetValue(field.Id, out var raw);
            list.Add(new PublicField(
                field.Property.Trim().ToLowerInvariant(),
                field.ColumnLabel,
                field.FieldName,
                field.ControlType,
                field.TargetPage,
                ToPublicValue(field, raw)));
        }

        return list;
    }

    private static object? ToPublicValue(FieldDef field, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return field.ControlType is "Multiple Select" ? Array.Empty<string>() : null;

        if (field.ControlType == "Multiple Select")
        {
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (field.ControlType == "Checkbox")
            return raw.Equals("true", StringComparison.OrdinalIgnoreCase);

        return raw;
    }

    /// <summary>Storefront product detail: General + Product Page targets (excludes backoffice-only Recommendations).</summary>
    public static bool IsStorefrontDetailTarget(FieldDef field) =>
        field.TargetPage is "General" or "Product Page";

    /// <summary>Dropdown / multi-select properties eligible for storefront search facets.</summary>
    public static IReadOnlyList<string> FilterableFacetProperties(IReadOnlyList<FieldDef> defs) =>
        defs.Where(static f => f.Enabled && f.ControlType is "Dropdown List" or "Multiple Select")
            .Select(static f => f.Property.Trim().ToLowerInvariant())
            .Where(static p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
