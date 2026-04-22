using System.Text.Json;
using System.Text.Json.Nodes;

namespace dCMS.Gateway;

/// <summary>
/// Fetches OpenAPI JSON specs from upstream services, rewrites paths to the
/// gateway prefix, then merges them into a single spec served at the gateway.
///
/// Backoffice spec  → /swagger/gateway/swagger.json
///   /api/v1/...    rewritten to  /gateway/v1/{service}/...
///
/// Storefront spec  → /swagger/storefront/swagger.json
///   /api/v1/products/** → /storefront/v1/products/**
///   /api/v1/...        → /storefront/v1/catalog/**
/// </summary>
public sealed class GatewaySwaggerAggregator(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<GatewaySwaggerAggregator> logger)
{
    private static readonly JsonSerializerOptions _pretty = new() { WriteIndented = true };

    // ── Public entry points ────────────────────────────────────────────────────

    /// <summary>Merged backoffice spec (all services under /gateway/v1/...).</summary>
    public async Task<string> GetBackofficeSpecAsync(CancellationToken ct = default)
    {
        var sources = new[]
        {
            (Service: "catalog",   PathPrefix: "/gateway/v1/catalog"),
            (Service: "orders",    PathPrefix: "/gateway/v1/orders"),
            (Service: "inventory", PathPrefix: "/gateway/v1/inventory"),
        };
        return await MergeSpecsAsync(sources, "dCMS Gateway API", "Backoffice + internal routes for all dCMS microservices.", ct);
    }

    /// <summary>Merged storefront spec (public + customer routes under /storefront/v1/...).</summary>
    public async Task<string> GetStorefrontSpecAsync(CancellationToken ct = default)
    {
        // Storefront only exposes catalog (products + brands/categories) and orders
        var sources = new[]
        {
            (Service: "catalog",   PathPrefix: "/storefront/v1"),
            (Service: "orders",    PathPrefix: "/storefront/v1/orders"),
        };
        return await MergeSpecsAsync(sources, "dCMS Storefront API", "Public product search, brand/category reads, and customer order endpoints.", ct);
    }

    // ── Private ────────────────────────────────────────────────────────────────

    private async Task<string> MergeSpecsAsync(
        (string Service, string PathPrefix)[] sources,
        string title,
        string description,
        CancellationToken ct)
    {
        var merged = new JsonObject
        {
            ["openapi"] = "3.0.3",
            ["info"]    = new JsonObject
            {
                ["title"]       = title,
                ["version"]     = "v1",
                ["description"] = description,
            },
            ["servers"] = new JsonArray(new JsonObject { ["url"] = "/" }),
            ["paths"]   = new JsonObject(),
            ["components"] = new JsonObject
            {
                ["schemas"]         = new JsonObject(),
                ["securitySchemes"] = new JsonObject
                {
                    ["Bearer"] = JsonNode.Parse("""
                        { "type": "http", "scheme": "bearer", "bearerFormat": "JWT",
                          "description": "dCMS JWT token. Obtain from Auth service." }
                        """)!,
                },
            },
            ["security"] = new JsonArray(new JsonObject { ["Bearer"] = new JsonArray() }),
            ["tags"]     = new JsonArray(),
        };

        foreach (var (service, pathPrefix) in sources)
        {
            var spec = await FetchSpecAsync(service, ct);
            if (spec is null) continue;
            MergePaths(merged, spec, pathPrefix, service);
            MergeSchemas(merged, spec);
            MergeTags(merged, spec, service);
        }

        return JsonSerializer.Serialize(merged, _pretty);
    }

    private async Task<JsonObject?> FetchSpecAsync(string service, CancellationToken ct)
    {
        var address = configuration[$"ReverseProxy:Clusters:{service}-cluster:Destinations:default:Address"];
        if (string.IsNullOrWhiteSpace(address))
        {
            logger.LogWarning("Swagger aggregator: no address configured for cluster '{Service}-cluster'", service);
            return null;
        }

        var url = address.TrimEnd('/') + "/swagger/v1/swagger.json";
        try
        {
            var client = httpClientFactory.CreateClient("swagger-aggregator");
            var json = await client.GetStringAsync(url, ct);
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Swagger aggregator: failed to fetch spec from {Url}", url);
            return null;
        }
    }

    private static void MergePaths(JsonObject merged, JsonObject spec, string pathPrefix, string serviceTag)
    {
        var paths = spec["paths"] as JsonObject;
        if (paths is null) return;

        var mergedPaths = merged["paths"] as JsonObject ?? new JsonObject();

        foreach (var (originalPath, pathItem) in paths)
        {
            if (pathItem is null) continue;

            // Strip /api/v1 prefix from upstream path; apply gateway prefix
            var stripped = originalPath.StartsWith("/api/v1")
                ? originalPath["/api/v1".Length..]
                : originalPath;

            var gatewayPath = pathPrefix + stripped;

            // Clone and inject x-service tag into each operation
            var cloned = JsonNode.Parse(pathItem.ToJsonString()) as JsonObject;
            if (cloned is null) continue;

            foreach (var method in new[] { "get", "post", "put", "patch", "delete" })
            {
                if (cloned[method] is JsonObject op)
                {
                    var tags = op["tags"] as JsonArray ?? new JsonArray();
                    // Prefix tag with service name to visually group in Swagger UI
                    var prefixed = new JsonArray();
                    foreach (var t in tags)
                        prefixed.Add($"{serviceTag}/{t?.GetValue<string>()}");
                    if (prefixed.Count == 0)
                        prefixed.Add(serviceTag);
                    op["tags"] = prefixed;
                }
            }

            mergedPaths[gatewayPath] = cloned;
        }

        merged["paths"] = mergedPaths;
    }

    private static void MergeSchemas(JsonObject merged, JsonObject spec)
    {
        var schemas = spec["components"]?["schemas"] as JsonObject;
        if (schemas is null) return;

        var mergedSchemas = merged["components"]!["schemas"] as JsonObject ?? new JsonObject();
        foreach (var (key, val) in schemas)
        {
            if (val is null) continue;
            // Avoid collision — prefix with first 3 chars of discriminator if key already exists
            var safeKey = mergedSchemas.ContainsKey(key) ? $"{key}_" : key;
            mergedSchemas[safeKey] = JsonNode.Parse(val.ToJsonString());
        }
        (merged["components"] as JsonObject)!["schemas"] = mergedSchemas;
    }

    private static void MergeTags(JsonObject merged, JsonObject spec, string service)
    {
        var tags = spec["tags"] as JsonArray;
        if (tags is null) return;

        var mergedTags = merged["tags"] as JsonArray ?? new JsonArray();
        foreach (var t in tags)
        {
            if (t is not JsonObject tagObj) continue;
            var prefixedTag = JsonNode.Parse(tagObj.ToJsonString()) as JsonObject;
            if (prefixedTag?["name"] is JsonValue nameVal)
                prefixedTag["name"] = $"{service}/{nameVal.GetValue<string>()}";
            if (prefixedTag is not null)
                mergedTags.Add(prefixedTag);
        }
        merged["tags"] = mergedTags;
    }
}
