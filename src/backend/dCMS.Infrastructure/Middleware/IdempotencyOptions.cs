using Microsoft.AspNetCore.Http;

namespace dCMS.Infrastructure.Middleware;

/// <summary>When <see cref="PathSubstrings"/> is non-empty and Redis is configured, matching POST/PUT/PATCH requests require <c>Idempotency-Key</c>.</summary>
public sealed class IdempotencyOptions
{
    public string[] PathSubstrings { get; set; } = [];

    /// <summary>When true (default), path must contain <c>/api/v1/</c>. Set false for services that use another prefix (e.g. Order <c>/api/orders</c>, DAI-327).</summary>
    public bool RequireApiV1Prefix { get; set; } = true;

    /// <summary>
    /// When true (default), missing/invalid key JSON uses the standard <c>data</c>/<c>meta</c>/<c>error</c> envelope.
    /// When false, uses Order-style <c>{{ "error": {{ "code", "message" }} }}</c>.
    /// </summary>
    public bool UseStandardApiEnvelope { get; set; } = true;

    public bool MatchesRequest(string method, string pathValue)
    {
        if (PathSubstrings is null || PathSubstrings.Length == 0)
            return false;

        if (!HttpMethods.IsPost(method) && !HttpMethods.IsPut(method) && !HttpMethods.IsPatch(method))
            return false;

        if (string.IsNullOrEmpty(pathValue))
            return false;

        if (RequireApiV1Prefix && !pathValue.Contains("/api/v1/", StringComparison.Ordinal))
            return false;

        foreach (var s in PathSubstrings)
        {
            if (string.IsNullOrEmpty(s))
                continue;
            if (pathValue.Contains(s, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
