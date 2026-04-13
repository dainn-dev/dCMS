using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using StackExchange.Redis;

namespace dCMS.Infrastructure.Middleware;

/// <summary>When Redis is configured and <see cref="IdempotencyOptions.PathSubstrings"/> matches the path, POST/PUT/PATCH require <c>Idempotency-Key</c> and replay identical responses (24h TTL).</summary>
public sealed class IdempotencyMiddleware(RequestDelegate next, IServiceProvider services, IOptions<IdempotencyOptions> options)
{
    public const string HeaderName = "Idempotency-Key";
    private const int MaxKeyLength = 128;
    private const int MaxCachedBodyChars = 512_000;
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public async Task InvokeAsync(HttpContext context)
    {
        var mux = services.GetService<IConnectionMultiplexer>();
        var opt = options.Value;
        if (mux is null || !opt.MatchesRequest(context.Request.Method, context.Request.Path.Value ?? ""))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out StringValues keyHeader) ||
            StringValues.IsNullOrEmpty(keyHeader))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            var missingBody = opt.UseStandardApiEnvelope
                ? """{"data":null,"meta":null,"error":{"code":"missing_idempotency_key","message":"Idempotency-Key header is required for this request."}}"""
                : """{"error":{"code":"MISSING_IDEMPOTENCY_KEY","message":"Idempotency-Key header is required."}}""";
            await context.Response.WriteAsync(missingBody, context.RequestAborted).ConfigureAwait(false);
            return;
        }

        var rawKey = keyHeader.ToString().Trim();
        if (rawKey.Length == 0 || rawKey.Length > MaxKeyLength)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            var invalidBody = opt.UseStandardApiEnvelope
                ? """{"data":null,"meta":null,"error":{"code":"invalid_idempotency_key","message":"Idempotency-Key must be 1–128 characters."}}"""
                : """{"error":{"code":"INVALID_IDEMPOTENCY_KEY","message":"Idempotency-Key must be 1–128 characters."}}""";
            await context.Response.WriteAsync(invalidBody, context.RequestAborted).ConfigureAwait(false);
            return;
        }

        var sub = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anon";
        var cacheKey = "dcms:idempotency:" + Sha256Hex($"{context.Request.Method}|{context.Request.Path}|{rawKey}|{sub}");

        var db = mux.GetDatabase();
        var cached = await db.StringGetAsync(cacheKey).ConfigureAwait(false);
        if (cached.HasValue)
        {
            try
            {
                using var doc = JsonDocument.Parse((string)cached!);
                var root = doc.RootElement;
                var status = root.GetProperty("statusCode").GetInt32();
                var contentType = root.GetProperty("contentType").GetString() ?? "application/json";
                var body = root.GetProperty("body").GetString() ?? "";
                if (body.Length > MaxCachedBodyChars)
                    throw new JsonException("too large");

                context.Response.StatusCode = status;
                context.Response.ContentType = contentType;
                await context.Response.WriteAsync(body, context.RequestAborted).ConfigureAwait(false);
                return;
            }
            catch
            {
                await db.KeyDeleteAsync(cacheKey).ConfigureAwait(false);
            }
        }

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context).ConfigureAwait(false);

            var bodyText = Encoding.UTF8.GetString(buffer.ToArray());
            if (bodyText.Length > MaxCachedBodyChars)
            {
                buffer.Position = 0;
                await buffer.CopyToAsync(originalBody, context.RequestAborted).ConfigureAwait(false);
                return;
            }

            if (context.Response.StatusCode < 500)
            {
                var payload = JsonSerializer.Serialize(new
                {
                    statusCode = context.Response.StatusCode,
                    contentType = context.Response.ContentType ?? "application/json",
                    body = bodyText
                });
                await db.StringSetAsync(cacheKey, payload, Ttl).ConfigureAwait(false);
            }

            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, context.RequestAborted).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static string Sha256Hex(string s)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
