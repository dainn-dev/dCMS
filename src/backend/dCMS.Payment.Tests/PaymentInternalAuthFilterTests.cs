using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace dCMS.Payment.Tests;

/// <summary>DAI-31 — unit tests for PaymentInternalAuthEndpointFilter: anonymous rejection and API-key acceptance.</summary>
public sealed class PaymentInternalAuthFilterTests
{
    private static IConfiguration ConfigWith(string? apiKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payment:InternalApiKey"] = apiKey,
            })
            .Build();

    private static DefaultHttpContext AnonymousContext() => new();

    private static DefaultHttpContext AuthenticatedContext()
    {
        var ctx = new DefaultHttpContext();
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "svc-1"));
        ctx.User = new ClaimsPrincipal(identity);
        return ctx;
    }

    private static DefaultHttpContext ApiKeyContext(string key)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Internal-Api-Key"] = key;
        return ctx;
    }

    [Fact]
    public void CheckAuth_anonymous_no_key_configured_returns_401()
    {
        var ctx = AnonymousContext();
        var result = PaymentInternalAuthEndpointFilter.CheckAuth(ctx, ConfigWith(null));

        // Result is non-null = auth denied (a 401 JSON result)
        Assert.NotNull(result);
        Assert.False(ctx.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public void CheckAuth_anonymous_wrong_key_returns_401()
    {
        var ctx = ApiKeyContext("wrong-key");
        var result = PaymentInternalAuthEndpointFilter.CheckAuth(ctx, ConfigWith("correct-key"));

        Assert.NotNull(result);
        Assert.False(ctx.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public void CheckAuth_correct_api_key_returns_null_and_sets_principal()
    {
        var ctx = ApiKeyContext("secret-key");
        var result = PaymentInternalAuthEndpointFilter.CheckAuth(ctx, ConfigWith("secret-key"));

        Assert.Null(result);
        Assert.True(ctx.User.Identity?.IsAuthenticated);
        Assert.Equal("payment-internal-api-key", ctx.User.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public void CheckAuth_x_api_key_header_also_accepted()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Api-Key"] = "alt-key";
        var result = PaymentInternalAuthEndpointFilter.CheckAuth(ctx, ConfigWith("alt-key"));

        Assert.Null(result);
        Assert.True(ctx.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public void CheckAuth_authenticated_user_returns_null_without_api_key()
    {
        var ctx = AuthenticatedContext();
        var result = PaymentInternalAuthEndpointFilter.CheckAuth(ctx, ConfigWith("secret-key"));

        Assert.Null(result);
    }

    [Fact]
    public void CheckAuth_empty_api_key_configured_rejects_empty_header()
    {
        var ctx = ApiKeyContext("");
        var result = PaymentInternalAuthEndpointFilter.CheckAuth(ctx, ConfigWith(""));

        // Both expected and provided are whitespace — expected is treated as not configured,
        // so it falls through to 401 (no valid key to compare against).
        Assert.NotNull(result);
    }
}
