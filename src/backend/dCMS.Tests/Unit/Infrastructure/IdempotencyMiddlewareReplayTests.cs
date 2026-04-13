using System.Security.Claims;
using System.Text;
using dCMS.Infrastructure.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace dCMS.Tests.Unit.Infrastructure;

/// <summary>
/// DAI-327 — duplicate POST with same Idempotency-Key replays cached status + body from Redis
/// (<c>dcms:idempotency:…</c>, 24h TTL). Uses Testcontainers Redis so StackExchange.Redis overloads are exercised for real.
/// </summary>
public sealed class IdempotencyMiddlewareReplayTests : IAsyncLifetime
{
    private RedisContainer? _redis;

    public async Task InitializeAsync()
    {
        _redis = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();
        await _redis.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_redis is not null)
            await _redis.DisposeAsync();
    }

    [Fact]
    public async Task Second_identical_post_replays_cached_response_without_calling_downstream()
    {
        await using var mux = await ConnectionMultiplexer.ConnectAsync(_redis!.GetConnectionString());
        var idemKey = $"idem-post-{Guid.NewGuid():N}";

        var services = new ServiceCollection();
        services.AddSingleton<IConnectionMultiplexer>(mux);
        services.Configure<IdempotencyOptions>(o =>
        {
            o.RequireApiV1Prefix = false;
            o.UseStandardApiEnvelope = false;
            o.PathSubstrings = ["/api/orders"];
        });
        await using var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<IdempotencyOptions>>();
        var downstreamCalls = 0;

        async Task Next(HttpContext ctx)
        {
            downstreamCalls++;
            ctx.Response.StatusCode = StatusCodes.Status201Created;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            await ctx.Response.WriteAsync("""{"orderId":"a","status":"payment_pending"}""", ctx.RequestAborted);
        }

        var middleware = new IdempotencyMiddleware(Next, sp, options);

        async Task<HttpContext> RunOnceAsync()
        {
            var ctx = new DefaultHttpContext { RequestServices = sp };
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")]));
            ctx.Request.Method = HttpMethods.Post;
            ctx.Request.Path = "/api/orders";
            ctx.Request.Headers[IdempotencyMiddleware.HeaderName] = idemKey;
            ctx.Response.Body = new MemoryStream();
            await middleware.InvokeAsync(ctx);
            return ctx;
        }

        var first = await RunOnceAsync();
        downstreamCalls.Should().Be(1);
        first.Response.StatusCode.Should().Be(StatusCodes.Status201Created);
        first.Response.Body.Position = 0;
        var firstBody = await new StreamReader(first.Response.Body, Encoding.UTF8).ReadToEndAsync();
        firstBody.Should().Be("""{"orderId":"a","status":"payment_pending"}""");

        var second = await RunOnceAsync();
        downstreamCalls.Should().Be(1);
        second.Response.StatusCode.Should().Be(StatusCodes.Status201Created);
        second.Response.Body.Position = 0;
        var secondBody = await new StreamReader(second.Response.Body, Encoding.UTF8).ReadToEndAsync();
        secondBody.Should().Be(firstBody);
    }

    [Fact]
    public async Task Second_identical_cancel_post_replays_cached_response_without_calling_downstream()
    {
        await using var mux = await ConnectionMultiplexer.ConnectAsync(_redis!.GetConnectionString());
        var idemKey = $"idem-cancel-{Guid.NewGuid():N}";

        var services = new ServiceCollection();
        services.AddSingleton<IConnectionMultiplexer>(mux);
        services.Configure<IdempotencyOptions>(o =>
        {
            o.RequireApiV1Prefix = false;
            o.UseStandardApiEnvelope = false;
            o.PathSubstrings = ["/api/orders"];
        });
        await using var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<IdempotencyOptions>>();
        var downstreamCalls = 0;

        async Task Next(HttpContext ctx)
        {
            downstreamCalls++;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            await ctx.Response.WriteAsync(
                """{"orderId":"550e8400-e29b-41d4-a716-446655440000","status":"cancelled"}""",
                ctx.RequestAborted);
        }

        var middleware = new IdempotencyMiddleware(Next, sp, options);

        async Task<HttpContext> RunOnceAsync()
        {
            var ctx = new DefaultHttpContext { RequestServices = sp };
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")]));
            ctx.Request.Method = HttpMethods.Post;
            ctx.Request.Path = "/api/orders/550e8400-e29b-41d4-a716-446655440000/cancel";
            ctx.Request.Headers[IdempotencyMiddleware.HeaderName] = idemKey;
            ctx.Response.Body = new MemoryStream();
            await middleware.InvokeAsync(ctx);
            return ctx;
        }

        var first = await RunOnceAsync();
        downstreamCalls.Should().Be(1);
        first.Response.StatusCode.Should().Be(StatusCodes.Status200OK);

        var second = await RunOnceAsync();
        downstreamCalls.Should().Be(1);
        second.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        second.Response.Body.Position = 0;
        var secondBody = await new StreamReader(second.Response.Body, Encoding.UTF8).ReadToEndAsync();
        secondBody.Should().Be("""{"orderId":"550e8400-e29b-41d4-a716-446655440000","status":"cancelled"}""");
    }
}
