using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using dCMS.AspNetCore.Auth;
using dCMS.Order.Core.Domain;
using dCMS.Order.Core.Ordering;
using dCMS.Order.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace dCMS.Order.Api.Routes;

/// <summary>DAI-632: Failed Orders operational endpoints (list/detail/retry/resolve + bulk).</summary>
public static class OrderFailedRoutes
{
    private static readonly JsonSerializerOptions JsonCamel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void MapOrderFailedRoutes(this WebApplication app)
    {
        var g = app.MapGroup("/api/orders/failed")
            .WithTags("orders-failed")
            .RequireAuthorization(DcmsPolicies.OrderFailureManage);

        g.MapGet("", ListFailed)
            .WithName("ListFailedOrders")
            .WithTenantStoreHeaderAccess(app.Configuration);

        g.MapGet("{orderId}", GetFailedDetail)
            .WithName("GetFailedOrderDetail")
            .WithTenantStoreHeaderAccess(app.Configuration);

        g.MapPost("{orderId}/retry", RetryFailed)
            .WithName("RetryFailedOrder")
            .WithTenantStoreHeaderAccess(app.Configuration);

        g.MapPost("{orderId}/resolve", ResolveFailed)
            .WithName("ResolveFailedOrder")
            .WithTenantStoreHeaderAccess(app.Configuration);

        g.MapPost("bulk-retry", BulkRetry)
            .WithName("BulkRetryFailedOrders")
            .WithTenantStoreHeaderAccess(app.Configuration);

        g.MapPost("bulk-resolve", BulkResolve)
            .WithName("BulkResolveFailedOrders")
            .WithTenantStoreHeaderAccess(app.Configuration);
    }

    private static bool TryGetTenantStore(HttpContext http, out string tenantId, out string storeId)
    {
        tenantId = http.Request.Headers["X-Tenant-Id"].FirstOrDefault()?.Trim() ?? "";
        storeId = http.Request.Headers["X-Store-Id"].FirstOrDefault()?.Trim() ?? "";
        return !string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(storeId);
    }

    private static IResult MissingTenantStore() =>
        Results.Json(
            new
            {
                data = (object?)null,
                error = new { code = "MISSING_TENANT_OR_STORE", message = "X-Tenant-Id and X-Store-Id headers are required." }
            },
            statusCode: StatusCodes.Status400BadRequest);

    private static bool IsValidStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        return Enum.TryParse<OrderFailureStatus>(raw.Trim(), ignoreCase: true, out _);
    }

    private static bool TryParseOrderId(string raw, out Guid orderId)
    {
        return Guid.TryParse(raw, out orderId);
    }

    private static string ActorId(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.Identity?.Name ?? "unknown";

    private static async Task<IResult> ListFailed(
        HttpContext http,
        [FromServices] IOrderFailureRepository failures,
        [FromQuery] string? status,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return MissingTenantStore();

        if (!IsValidStatus(status))
        {
            return Results.Json(
                new { data = (object?)null, error = new { code = "INVALID_STATUS", message = "Invalid status." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var lim = !limit.HasValue || limit.Value < 1 ? 20 : Math.Clamp(limit.Value, 1, 200);
        IReadOnlyList<OrderFailureRow> rows;
        try
        {
            rows = await failures.ListAsync(tenantId, storeId, status, cursor, lim, ct).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return Results.Json(
                new { data = (object?)null, error = new { code = "INVALID_CURSOR", message = ex.Message } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var items = rows.Select(r => new
        {
            orderId = r.OrderId,
            failureStatus = r.FailureStatus,
            failureReason = r.FailureReason,
            failureErrorCode = r.FailureErrorCode,
            failedAt = r.FailedAt,
            retryCount = r.RetryCount,
            lastRetryAt = r.LastRetryAt
        }).ToList();

        var nextCursor = rows.Count == lim
            ? PgOrderFailureRepository.EncodeCursor(rows[^1].FailedAt, rows[^1].OrderId)
            : null;

        return Results.Json(new { data = new { items }, meta = new { nextCursor }, error = (object?)null }, JsonCamel);
    }

    private static async Task<IResult> GetFailedDetail(
        string orderId,
        HttpContext http,
        [FromServices] IOrderFailureRepository failures,
        [FromServices] IOrderService orders,
        CancellationToken ct)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return MissingTenantStore();
        if (!TryParseOrderId(orderId, out var oid))
        {
            return Results.Json(
                new { data = (object?)null, error = new { code = "INVALID_ORDER_ID", message = "orderId must be a UUID." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var failure = await failures.GetAsync(tenantId, storeId, oid, ct).ConfigureAwait(false);
        if (failure is null)
        {
            return Results.Json(
                new { data = (object?)null, error = new { code = "NOT_FOUND", message = "Failed order not found." } },
                statusCode: StatusCodes.Status404NotFound);
        }

        var timed = await orders.GetTimedByIdAsync(tenantId, storeId, oid.ToString("D"), ct).ConfigureAwait(false);

        return Results.Json(
            new
            {
                data = new
                {
                    failure = new
                    {
                        orderId = failure.OrderId,
                        tenantId = failure.TenantId,
                        storeId = failure.StoreId,
                        failureStatus = failure.FailureStatus,
                        failureReason = failure.FailureReason,
                        failureErrorCode = failure.FailureErrorCode,
                        sourceEventId = failure.SourceEventId,
                        failedAt = failure.FailedAt,
                        retryCount = failure.RetryCount,
                        lastRetryAt = failure.LastRetryAt,
                        resolvedAt = failure.ResolvedAt,
                        resolvedBy = failure.ResolvedBy,
                        log = SafeParseJsonArray(failure.LogJson)
                    },
                    order = timed is null
                        ? null
                        : new
                        {
                            orderId = timed.Order.Id,
                            customerId = timed.Order.CustomerId,
                            status = timed.Order.Status.ToString(),
                            createdAt = timed.CreatedAt,
                            lines = timed.Order.Items.Select(i => new
                            {
                                lineId = i.Id,
                                productId = i.ProductId,
                                variantId = i.VariantId,
                                quantity = i.Quantity,
                                productNameSnapshot = i.ProductNameSnapshot
                            })
                        }
                },
                error = (object?)null
            },
            JsonCamel);
    }

    private static JsonElement SafeParseJsonArray(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "[]" : raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                return doc.RootElement.Clone();
        }
        catch
        {
            // ignore
        }
        using var fallback = JsonDocument.Parse("[]");
        return fallback.RootElement.Clone();
    }

    public sealed record RetryBody(
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("payload")] string? Payload,
        [property: JsonPropertyName("idempotencyKey")] string? IdempotencyKey);

    public sealed record ResolveBody([property: JsonPropertyName("note")] string? Note);

    public sealed record BulkRetryBody(
        [property: JsonPropertyName("orderIds")] List<string> OrderIds,
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("payload")] string? Payload,
        [property: JsonPropertyName("idempotencyKey")] string? IdempotencyKey);

    public sealed record BulkResolveBody(
        [property: JsonPropertyName("orderIds")] List<string> OrderIds,
        [property: JsonPropertyName("note")] string? Note);

    private static async Task<IResult> RetryFailed(
        string orderId,
        HttpContext http,
        [FromBody] RetryBody body,
        [FromServices] IOrderFailureRepository failures,
        [FromServices] IConnectionMultiplexer? redisMux,
        [FromServices] IConfiguration configuration,
        CancellationToken ct)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return MissingTenantStore();
        if (!TryParseOrderId(orderId, out var oid))
        {
            return Results.Json(
                new { data = (object?)null, error = new { code = "INVALID_ORDER_ID", message = "orderId must be a UUID." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(body.Kind))
            return Results.Json(new { data = (object?)null, error = new { code = "VALIDATION_ERROR", message = "kind is required." } },
                statusCode: StatusCodes.Status400BadRequest);

        // Idempotency (optional) — uses Redis 24h TTL to avoid double-charge etc.
        if (!string.IsNullOrWhiteSpace(body.IdempotencyKey))
        {
            var key = $"dcms:order-failure:retry:{tenantId}:{storeId}:{oid:D}:{body.IdempotencyKey.Trim()}";
            var db = GetRedisDb(redisMux, configuration);
            if (db is null)
                goto SkipIdem;
            var existing = await db.StringGetAsync(key).ConfigureAwait(false);
            if (existing.HasValue)
            {
                // Already processed — skip side effects
                return Results.Json(new
                {
                    data = new { ok = true, newStatus = "skipped", retryCount = (int?)null, threeDsUrl = (string?)null },
                    error = (object?)null
                }, JsonCamel);
            }

            // Best-effort marker (accept rare race; avoids double charge in normal use).
            await db.StringSetAsync(key, "1", expiry: TimeSpan.FromHours(24)).ConfigureAwait(false);
        }
        SkipIdem: ;

        var ok = await failures.IncrementRetryAsync(tenantId, storeId, oid, ct).ConfigureAwait(false);
        if (!ok)
        {
            return Results.Json(
                new { data = (object?)null, error = new { code = "NOT_FOUND", message = "Failed order not found or already resolved." } },
                statusCode: StatusCodes.Status404NotFound);
        }

        // Retry handler switch (placeholder hooks; domain adapters live elsewhere)
        _ = body.Kind.Trim() switch
        {
            "PaymentFailed" => "payment",
            "AddressError" => "address",
            "AuthFailed" => "auth",
            "StockError" => "stock",
            "SystemError" => "system",
            _ => "unknown",
        };

        var updated = await failures.GetAsync(tenantId, storeId, oid, ct).ConfigureAwait(false);
        return Results.Json(new
        {
            data = new
            {
                ok = true,
                newStatus = "retry_accepted",
                retryCount = updated?.RetryCount ?? 0,
                threeDsUrl = (string?)null
            },
            error = (object?)null
        }, JsonCamel);
    }

    private static async Task<IResult> ResolveFailed(
        string orderId,
        HttpContext http,
        [FromBody] ResolveBody? body,
        [FromServices] IOrderFailureRepository failures,
        CancellationToken ct)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return MissingTenantStore();
        if (!TryParseOrderId(orderId, out var oid))
        {
            return Results.Json(
                new { data = (object?)null, error = new { code = "INVALID_ORDER_ID", message = "orderId must be a UUID." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var resolvedBy = ActorId(http.User);
        var ok = await failures.MarkResolvedAsync(tenantId, storeId, oid, resolvedBy, ct).ConfigureAwait(false);
        if (!ok)
        {
            return Results.Json(
                new { data = (object?)null, error = new { code = "NOT_FOUND", message = "Failed order not found or already resolved." } },
                statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Json(new { data = new { ok = true, note = body?.Note }, error = (object?)null }, JsonCamel);
    }

    private static async Task<IResult> BulkRetry(
        HttpContext http,
        [FromBody] BulkRetryBody body,
        [FromServices] IOrderFailureRepository failures,
        [FromServices] IConnectionMultiplexer? redisMux,
        [FromServices] IConfiguration configuration,
        CancellationToken ct)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return MissingTenantStore();
        if (body.OrderIds is null || body.OrderIds.Count == 0)
            return Results.Json(new { data = (object?)null, error = new { code = "VALIDATION_ERROR", message = "orderIds[] required." } },
                statusCode: StatusCodes.Status400BadRequest);

        var succeeded = 0;
        var failed = 0;
        foreach (var id in body.OrderIds.Distinct())
        {
            if (!TryParseOrderId(id, out var oid))
            {
                failed++;
                continue;
            }

            // Per-order idempotency (optional)
            if (!string.IsNullOrWhiteSpace(body.IdempotencyKey))
            {
                var key = $"dcms:order-failure:retry:{tenantId}:{storeId}:{oid:D}:{body.IdempotencyKey.Trim()}";
                var db = GetRedisDb(redisMux, configuration);
                if (db is null)
                    goto DoIncrement;
                var existing = await db.StringGetAsync(key).ConfigureAwait(false);
                if (existing.HasValue)
                    continue;

                await db.StringSetAsync(key, "1", expiry: TimeSpan.FromHours(24)).ConfigureAwait(false);
            }

            DoIncrement:
            if (await failures.IncrementRetryAsync(tenantId, storeId, oid, ct).ConfigureAwait(false))
                succeeded++;
            else
                failed++;
        }

        return Results.Json(new { data = new { succeeded, failed }, error = (object?)null }, JsonCamel);
    }

    private static async Task<IResult> BulkResolve(
        HttpContext http,
        [FromBody] BulkResolveBody body,
        [FromServices] IOrderFailureRepository failures,
        CancellationToken ct)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return MissingTenantStore();
        if (body.OrderIds is null || body.OrderIds.Count == 0)
            return Results.Json(new { data = (object?)null, error = new { code = "VALIDATION_ERROR", message = "orderIds[] required." } },
                statusCode: StatusCodes.Status400BadRequest);

        var succeeded = 0;
        var failed = 0;
        var resolvedBy = ActorId(http.User);
        foreach (var id in body.OrderIds.Distinct())
        {
            if (!TryParseOrderId(id, out var oid))
            {
                failed++;
                continue;
            }

            if (await failures.MarkResolvedAsync(tenantId, storeId, oid, resolvedBy, ct).ConfigureAwait(false))
                succeeded++;
            else
                failed++;
        }

        return Results.Json(new { data = new { succeeded, failed }, error = (object?)null }, JsonCamel);
    }

    private static IDatabase? GetRedisDb(IConnectionMultiplexer? mux, IConfiguration configuration)
    {
        if (mux is not null)
            return mux.GetDatabase();
        var cs = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(cs))
            return null;
        try
        {
            // Fallback (mainly for tests) when DI didn't wire mux.
            var tmp = ConnectionMultiplexer.Connect(cs);
            return tmp.GetDatabase();
        }
        catch
        {
            return null;
        }
    }
}

