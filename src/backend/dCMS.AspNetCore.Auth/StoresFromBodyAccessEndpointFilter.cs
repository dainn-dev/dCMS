using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace dCMS.AspNetCore.Auth;

/// <summary>
/// DAI-700 (AC4) — Bulk endpoint scope check. Reads a JSON body containing a
/// <c>storeIds</c> array and ensures the JWT scope covers EVERY id.
/// SuperAdmin and ChainAdmin/BrandManager-with-no-store-restriction bypass; otherwise the
/// caller's <see cref="DcmsClaims.StoreIds"/> claim must be a superset of the body ids.
/// </summary>
/// <remarks>
/// Rebuffers the request body so the downstream handler can still bind it.
/// JSON parsing is intentionally lenient — only the <c>storeIds</c> array is read; other fields are ignored.
/// </remarks>
public sealed class StoresFromBodyAccessEndpointFilter : IEndpointFilter
{
    private const string FieldName = "storeIds";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var user = http.User;
        if (user.Identity?.IsAuthenticated != true)
            return Forbid("UNAUTHORIZED", "Authentication required.", StatusCodes.Status401Unauthorized);

        var requested = await ReadStoreIdsAsync(http).ConfigureAwait(false);
        if (requested.Count == 0)
            return Forbid("STORE_SCOPE_REQUIRED", $"Body must include a non-empty '{FieldName}' array.", StatusCodes.Status400BadRequest);

        if (user.IsInRole(DcmsRoles.SuperAdmin))
            return await next(context);

        var tid = user.FindFirst(DcmsClaims.TenantId)?.Value;
        if (string.IsNullOrWhiteSpace(tid))
            return Forbid("FORBIDDEN", "Token is missing tenant_id claim.");

#pragma warning disable CS0618 // Obsolete StoreIds — kept until US-5 (Users module) lands.
        var allowedStores = DcmsScopeClaimParser.ParseCsvClaim(user.FindFirst(DcmsClaims.StoreIds)?.Value);
#pragma warning restore CS0618

        // ChainAdmin / BrandManager: allowed across all stores within tenant unless their token narrows scope.
        if (user.IsInRole(DcmsRoles.ChainAdmin) || user.IsInRole(DcmsRoles.BrandManager))
        {
            if (allowedStores.Count == 0)
                return await next(context);
            // fall through to subset check
        }
        else if (allowedStores.Count == 0)
        {
            // Store-scoped role with single-store token: synthesize from store_id claim.
            var sid = user.FindFirst(DcmsClaims.StoreId)?.Value;
            if (string.IsNullOrWhiteSpace(sid))
                return Forbid("FORBIDDEN", "Token is missing store_id / store_ids claim.");
            allowedStores = new HashSet<string>(new[] { sid }, StringComparer.Ordinal);
        }

        foreach (var s in requested)
            if (!allowedStores.Contains(s, StringComparer.Ordinal))
                return Forbid("FORBIDDEN", $"Token does not grant access to store '{s}'.");

        return await next(context);
    }

    private static async Task<IReadOnlyList<string>> ReadStoreIdsAsync(HttpContext http)
    {
        if (!HttpMethods.IsPost(http.Request.Method) && !HttpMethods.IsPut(http.Request.Method) && !HttpMethods.IsPatch(http.Request.Method))
            return Array.Empty<string>();

        http.Request.EnableBuffering();
        http.Request.Body.Position = 0;
        try
        {
            using var doc = await JsonDocument.ParseAsync(http.Request.Body, default).ConfigureAwait(false);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return Array.Empty<string>();
            if (!doc.RootElement.TryGetProperty(FieldName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();

            var list = new List<string>(arr.GetArrayLength());
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(el.GetString()))
                    list.Add(el.GetString()!);
            }
            return list;
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
        finally
        {
            http.Request.Body.Position = 0;
        }
    }

    private static IResult Forbid(string code, string message, int status = StatusCodes.Status403Forbidden) =>
        Results.Json(
            new { data = (object?)null, meta = (object?)null, error = new { code, message } },
            statusCode: status);
}
