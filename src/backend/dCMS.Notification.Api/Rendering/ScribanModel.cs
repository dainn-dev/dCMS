using System.Text.Json;
using Scriban.Runtime;

namespace dCMS.Notification.Api.Rendering;

internal static class ScribanModel
{
    public static ScriptObject FromJsonElement(JsonElement el)
    {
        var root = new ScriptObject();
        root.Add("model", ConvertValue(el));
        return root;
    }

    private static object? ConvertValue(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(el),
            JsonValueKind.Array => el.EnumerateArray().Select(ConvertValue).ToList(),
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var i) ? i : el.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => el.ToString(),
        };
    }

    private static ScriptObject ConvertObject(JsonElement el)
    {
        var o = new ScriptObject();
        foreach (var p in el.EnumerateObject())
            o.Add(p.Name, ConvertValue(p.Value));
        return o;
    }
}

