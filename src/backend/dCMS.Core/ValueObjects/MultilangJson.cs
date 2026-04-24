using System.Text.Json;

namespace dCMS.Core.ValueObjects;

/// <summary>JSON object map locale → text (e.g. {"vi":"...","en":"..."}). VI is required for product names.</summary>
public static class MultilangJson
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static void ValidateNameRequiredVi(string nameJson)
    {
        if (string.IsNullOrWhiteSpace(nameJson))
            throw new ArgumentException("Name JSON is required.", nameof(nameJson));

        Dictionary<string, string>? map;
        try
        {
            map = JsonSerializer.Deserialize<Dictionary<string, string>>(nameJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Name must be valid JSON object of locale → string.", nameof(nameJson), ex);
        }

        if (map is null || map.Count == 0)
            throw new ArgumentException("Name JSON must contain at least the 'vi' entry.", nameof(nameJson));

        foreach (var kvp in map)
        {
            if (!string.Equals(kvp.Key, "vi", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.IsNullOrWhiteSpace(kvp.Value))
                throw new ArgumentException("Name JSON must include non-empty Vietnamese ('vi') text.", nameof(nameJson));
            return;
        }

        throw new ArgumentException("Name JSON must include non-empty Vietnamese ('vi') text.", nameof(nameJson));
    }

    public static void ValidateDescriptionOptional(string? descriptionJson)
    {
        if (string.IsNullOrWhiteSpace(descriptionJson))
            return;

        try
        {
            _ = JsonSerializer.Deserialize<Dictionary<string, string>>(descriptionJson!, JsonOptions)
                ?? throw new ArgumentException("Description must be a JSON object.", nameof(descriptionJson));
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Description must be valid JSON object of locale → string.", nameof(descriptionJson), ex);
        }
    }

    /// <summary>Pick a display string from name JSON (vi → en → first non-empty).</summary>
    public static string PickDisplayName(string nameJson)
    {
        if (string.IsNullOrWhiteSpace(nameJson))
            return "";

        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(nameJson, JsonOptions);
            if (map is null || map.Count == 0)
                return nameJson;

            foreach (var key in new[] { "vi", "en" })
            {
                if (map.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                    return v;
            }

            foreach (var v in map.Values)
            {
                if (!string.IsNullOrWhiteSpace(v))
                    return v;
            }
        }
        catch (JsonException)
        {
            // not JSON — return raw
        }

        return nameJson;
    }
}
