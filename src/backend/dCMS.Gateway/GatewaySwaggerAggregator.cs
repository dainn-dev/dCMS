using System.Text.Json;
using System.Text.Json.Nodes;

namespace dCMS.Gateway;

/// <summary>
/// Fetches OpenAPI JSON specs from upstream services, rewrites paths to the
/// gateway prefix, then merges them into a single spec served at the gateway.
///
/// The path-rewrite rules MUST mirror the YARP <c>Transforms.PathPattern</c> rules
/// in <c>appsettings.json</c>; if YARP rewrites <c>/gateway/v1/{x}/...</c> →
/// <c>/api/v1/...</c>, the aggregator must do the inverse so consumers see the
/// gateway-facing path, not the upstream path.
///
/// Backoffice spec  → /swagger/backoffice/swagger.json
///   For each service: upstream <c>/api/v1/...</c> (or <c>/api/...</c> for orders)
///   is rewritten to the gateway path documented per source below.
///
/// Storefront spec  → /swagger/storefront/swagger.json
///   Public + customer routes only (catalog reads, customer orders).
/// </summary>
public sealed class GatewaySwaggerAggregator(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<GatewaySwaggerAggregator> logger)
{
    private static readonly JsonSerializerOptions _pretty = new() { WriteIndented = true };

    /// <summary>
    /// Description of one upstream service feeding the merged spec.
    /// </summary>
    /// <param name="Service">Cluster id without the <c>-cluster</c> suffix; used to look up the upstream
    ///   address from <c>ReverseProxy:Clusters:{service}-cluster:Destinations:default:Address</c>.</param>
    /// <param name="GatewayPrefix">Prefix to prepend when rewriting paths (e.g. <c>/gateway/v1/orders</c>).</param>
    /// <param name="UpstreamPrefix">Prefix to strip from the upstream path before applying <see cref="GatewayPrefix"/>.
    ///   Must match the YARP transform exactly. Most services use <c>/api/v1</c>; <c>orders</c> uses <c>/api</c>.</param>
    /// <param name="Tag">Friendly tag prefix shown in Swagger UI (defaults to <see cref="Service"/>).</param>
    private sealed record SpecSource(string Service, string GatewayPrefix, string UpstreamPrefix, string? Tag = null);

    // ── Public entry points ────────────────────────────────────────────────────

    /// <summary>Merged backoffice spec (all 11 services under /gateway/v1/...).</summary>
    public async Task<string> GetBackofficeSpecAsync(CancellationToken ct = default)
    {
        // ── Source-of-truth: appsettings.json ReverseProxy:Routes ─────────────
        // Each entry below corresponds to one route + cluster pair. If you add or rename a route,
        // update this list AND the YARP config in lockstep.
        var sources = new[]
        {
            // Catalog: products, brands, categories, channels, attributes (admin views)
            new SpecSource("catalog",      "/gateway/v1/catalog",      "/api/v1"),
            // Orders: orders, refund cases, payment orchestration, order reports
            new SpecSource("orders",       "/gateway/v1/orders",       "/api"),
            // Returns: alias route hitting orders-cluster (RMA flow)
            new SpecSource("orders",       "/gateway/v1/returns",      "/api/returns",  Tag: "returns"),
            // Inventory: stock levels, stock movements, restock subscriptions
            new SpecSource("inventory",    "/gateway/v1/inventory",    "/api/v1"),
            // Promotions: campaigns, promo codes, eligibility evaluation
            new SpecSource("promotions",   "/gateway/v1/promotions",   "/api/v1"),
            // Fulfillment: shipments, delivery slots, fulfillment status
            new SpecSource("fulfillment",  "/gateway/v1/fulfillment",  "/api/v1"),
            // Reports: analytics aggregates (sales, abandon-cart, restock subs)
            new SpecSource("reports",      "/gateway/v1/reports",      "/api/v1/reports"),
            // Notifications: outbound messaging templates + delivery log
            new SpecSource("notification", "/gateway/v1/notifications", "/api/v1",      Tag: "notifications"),
            // Approvals: product approval workflow, approval subjects
            new SpecSource("approvals",    "/gateway/v1/approvals",    "/api/v1"),
            // Vouchers: voucher issuance + redemption (multi-tender component)
            new SpecSource("vouchers",     "/gateway/v1/vouchers",     "/api/v1"),
            // Voucher holds: temporary holds during multi-tender payment saga
            new SpecSource("vouchers",     "/gateway/v1/voucher-holds", "/api/v1",     Tag: "voucher-holds"),
            // Loyalty: ledger entries, points balance, membership lookup
            new SpecSource("loyalty",      "/gateway/v1/loyalty",      "/api/v1"),
            // Loyalty holds: temporary holds during multi-tender payment saga
            new SpecSource("loyalty",      "/gateway/v1/loyalty-holds", "/api/v1",     Tag: "loyalty-holds"),
        };

        const string description = """
            **Backoffice + internal routes for all dCMS microservices.**

            Every endpoint requires a Bearer JWT issued by the Auth service. The gateway validates the
            token and forwards a short-lived internal JWT to the upstream service.

            ### Authoritative tenant context

            The gateway derives `tenant_id` from the JWT claim. Upstream services trust this header and do
            not re-validate; bypassing the gateway is not supported in production.

            ### Pagination

            List endpoints accept `cursor` + `limit` (default 50, max 100). Responses return
            `meta.nextCursor`; pass it back as `cursor` to fetch the next page.

            ### Common headers

            - `Authorization: Bearer <jwt>` — required.
            - `X-Tenant-Id`, `X-Store-Id` — informational; gateway always trusts the JWT claim instead.
            - `X-Forwarded-By: dcms-gateway` — added by the gateway; reject upstream traffic without it
              if you've enabled gateway-only mode.
            - `X-Idempotency-Key` — accepted on POST/PATCH where supported (orders, payments, vouchers).
            """;

        return await MergeSpecsAsync(sources, "dCMS Backoffice API", description, ct);
    }

    /// <summary>Merged storefront spec (public + customer routes under /storefront/v1/...).</summary>
    public async Task<string> GetStorefrontSpecAsync(CancellationToken ct = default)
    {
        // Storefront only exposes catalog (products + brands/categories) and orders.
        // NOTE: storefront prefix routes are not yet defined in YARP appsettings.
        // This spec assumes future storefront-specific routes will land under /storefront/v1/.
        var sources = new[]
        {
            new SpecSource("catalog", "/storefront/v1",        "/api/v1"),
            new SpecSource("orders",  "/storefront/v1/orders", "/api"),
        };

        const string description = """
            **Public product browsing and customer-facing order endpoints.**

            Anonymous endpoints (product search, brand/category reads) require only a tenant context
            (resolved from the host header or `X-Tenant-Id`). Customer endpoints (cart, checkout, order
            history) require a customer-scoped Bearer token issued after customer login.

            ### Caching

            Public catalog reads are CDN-cacheable; cart/checkout/order endpoints are not.

            ### Currency & language

            All responses include the active `currency` and `locale` fields. Storefront clients should
            never assume a default — currency and locale are resolved per request from the customer
            session and the channel's configured defaults.
            """;

        return await MergeSpecsAsync(sources, "dCMS Storefront API", description, ct);
    }

    // ── Private ────────────────────────────────────────────────────────────────

    private async Task<string> MergeSpecsAsync(
        SpecSource[] sources,
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
                ["contact"]     = new JsonObject
                {
                    ["name"]  = "dCMS Platform Team",
                    ["email"] = "platform@dcms.local",
                },
                ["license"] = new JsonObject { ["name"] = "Proprietary — internal use only" },
            },
            ["servers"] = new JsonArray(
                new JsonObject { ["url"] = "/", ["description"] = "Same-origin (current host)" },
                new JsonObject { ["url"] = "http://localhost:5100", ["description"] = "Local docker-compose gateway" }),
            ["paths"]   = new JsonObject(),
            ["components"] = new JsonObject
            {
                ["schemas"]         = new JsonObject(),
                ["securitySchemes"] = new JsonObject
                {
                    ["Bearer"] = JsonNode.Parse("""
                        {
                          "type": "http",
                          "scheme": "bearer",
                          "bearerFormat": "JWT",
                          "description": "dCMS JWT token. Obtain from the Auth service after backoffice or customer login. The gateway validates this token, then mints a short-lived internal JWT it forwards to upstream services. Send as 'Authorization: Bearer <token>'."
                        }
                        """)!,
                },
                ["responses"] = new JsonObject
                {
                    ["Unauthorized"]  = ErrorResponse("401", "Missing or invalid Bearer token.",                 "unauthorized"),
                    ["Forbidden"]     = ErrorResponse("403", "Token valid but caller lacks required role.",      "forbidden"),
                    ["NotFound"]      = ErrorResponse("404", "Resource not found in the active tenant scope.",   "not_found"),
                    ["Validation"]    = ErrorResponse("422", "Request body or query parameters failed validation.", "validation_failed"),
                    ["RateLimited"]   = ErrorResponse("429", "Rate limit exceeded for this tenant or IP.",       "rate_limit_exceeded"),
                    ["ServerError"]   = ErrorResponse("500", "Unexpected error from gateway or upstream service.", "internal_error"),
                },
            },
            ["security"] = new JsonArray(new JsonObject { ["Bearer"] = new JsonArray() }),
            ["tags"]     = new JsonArray(),
        };

        foreach (var source in sources)
        {
            var spec = await FetchSpecAsync(source.Service, ct);
            if (spec is null) continue;
            MergePaths(merged, spec, source);
            MergeSchemas(merged, spec);
            MergeTags(merged, spec, source.Tag ?? source.Service);
        }

        return JsonSerializer.Serialize(merged, _pretty);
    }

    private static JsonNode ErrorResponse(string status, string description, string code) =>
        JsonNode.Parse($$"""
            {
              "description": "{{description}}",
              "content": {
                "application/json": {
                  "schema": {
                    "type": "object",
                    "properties": {
                      "data":  { "type": "object", "nullable": true },
                      "meta":  { "type": "object", "nullable": true },
                      "error": {
                        "type": "object",
                        "properties": {
                          "code":    { "type": "string", "example": "{{code}}" },
                          "message": { "type": "string" }
                        }
                      }
                    }
                  }
                }
              }
            }
            """)!;

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

    private static void MergePaths(JsonObject merged, JsonObject spec, SpecSource source)
    {
        var paths = spec["paths"] as JsonObject;
        if (paths is null) return;

        var mergedPaths = merged["paths"] as JsonObject ?? new JsonObject();
        var serviceTag = source.Tag ?? source.Service;

        foreach (var (originalPath, pathItem) in paths)
        {
            if (pathItem is null) continue;

            // For an "alias" route (Tag != null) only include paths actually under the upstream prefix —
            // the same upstream service typically also serves a different gateway prefix that has its own
            // SpecSource entry, and we don't want to duplicate every operation under both prefixes.
            if (source.Tag is not null && !originalPath.StartsWith(source.UpstreamPrefix, StringComparison.Ordinal))
                continue;

            // Strip the upstream prefix exactly matching the YARP transform; fall back to original path
            // when an upstream operation doesn't sit under the expected prefix.
            var stripped = originalPath.StartsWith(source.UpstreamPrefix, StringComparison.Ordinal)
                ? originalPath[source.UpstreamPrefix.Length..]
                : originalPath;

            // Ensure exactly one slash between gateway prefix and the stripped tail
            if (stripped.Length > 0 && stripped[0] != '/') stripped = "/" + stripped;
            var gatewayPath = source.GatewayPrefix + stripped;

            // Clone and inject x-service tag into each operation
            var cloned = JsonNode.Parse(pathItem.ToJsonString()) as JsonObject;
            if (cloned is null) continue;

            foreach (var method in new[] { "get", "post", "put", "patch", "delete" })
            {
                if (cloned[method] is JsonObject op)
                {
                    var tags = op["tags"] as JsonArray ?? new JsonArray();
                    var prefixed = new JsonArray();
                    foreach (var t in tags)
                    {
                        var raw = t?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(raw))
                            prefixed.Add($"{serviceTag}/{raw}");
                    }
                    if (prefixed.Count == 0)
                        prefixed.Add(serviceTag);
                    op["tags"] = prefixed;

                    // Wire the standard error responses without overriding upstream-defined success responses.
                    if (op["responses"] is not JsonObject responses)
                    {
                        responses = new JsonObject();
                        op["responses"] = responses;
                    }
                    EnsureRef(responses, "401", "#/components/responses/Unauthorized");
                    EnsureRef(responses, "403", "#/components/responses/Forbidden");
                    EnsureRef(responses, "404", "#/components/responses/NotFound");
                    EnsureRef(responses, "422", "#/components/responses/Validation");
                    EnsureRef(responses, "429", "#/components/responses/RateLimited");
                    EnsureRef(responses, "500", "#/components/responses/ServerError");
                }
            }

            mergedPaths[gatewayPath] = cloned;
        }

        merged["paths"] = mergedPaths;
    }

    private static void EnsureRef(JsonObject responses, string status, string refPath)
    {
        if (responses.ContainsKey(status)) return;
        responses[status] = new JsonObject { ["$ref"] = refPath };
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
