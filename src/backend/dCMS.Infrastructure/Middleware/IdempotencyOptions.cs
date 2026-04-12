using Microsoft.AspNetCore.Http;

namespace dCMS.Infrastructure.Middleware;

/// <summary>When <see cref="PathSubstrings"/> is non-empty and Redis is configured, matching POST/PUT/PATCH requests require <c>Idempotency-Key</c>.</summary>
public sealed class IdempotencyOptions
{
    public string[] PathSubstrings { get; set; } = [];

    public bool MatchesRequest(string method, string pathValue)
    {
        if (PathSubstrings is null || PathSubstrings.Length == 0)
            return false;

        if (!HttpMethods.IsPost(method) && !HttpMethods.IsPut(method) && !HttpMethods.IsPatch(method))
            return false;

        if (string.IsNullOrEmpty(pathValue) || !pathValue.Contains("/api/v1/", StringComparison.Ordinal))
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
