using System.Security.Cryptography;
using System.Text;
using dCMS.Catalog.Api.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace dCMS.Catalog.Api.Internal;

public sealed class InternalCatalogApiKeyEndpointFilter : IEndpointFilter
{
    public const string HeaderName = "X-Internal-Api-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var opts = context.HttpContext.RequestServices.GetRequiredService<IOptions<InternalCatalogOptions>>().Value;
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
            return ApiEnvelope.Error("not_configured",
                "Internal catalog API is disabled (configure InternalCatalog:ApiKey).",
                StatusCodes.Status503ServiceUnavailable);

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out StringValues header) ||
            StringValues.IsNullOrEmpty(header))
            return ApiEnvelope.Error("unauthorized", "X-Internal-Api-Key header is required.",
                StatusCodes.Status401Unauthorized);

        if (!KeysMatch(opts.ApiKey, header.ToString()))
            return ApiEnvelope.Error("forbidden", "Invalid internal API key.", StatusCodes.Status403Forbidden);

        return await next(context).ConfigureAwait(false);
    }

    private static bool KeysMatch(string expected, string provided)
    {
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
