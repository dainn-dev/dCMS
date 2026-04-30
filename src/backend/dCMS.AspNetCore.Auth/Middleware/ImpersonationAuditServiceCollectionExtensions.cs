using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace dCMS.AspNetCore.Auth.Middleware;

/// <summary>
/// DAI-752 (US-5) — registers the JWT blacklist used by <see cref="ImpersonationAuditMiddleware"/>.
/// If <c>ConnectionStrings:Redis</c> is configured, uses <see cref="RedisJwtBlacklist"/>; otherwise
/// falls back to <see cref="InMemoryJwtBlacklist"/> (single-process, fine for tests / local dev).
/// </summary>
public static class ImpersonationAuditServiceCollectionExtensions
{
    public static IServiceCollection AddDcmsImpersonationAudit(this IServiceCollection services, IConfiguration configuration)
    {
        var redisCs = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisCs))
        {
            services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisCs));
            services.TryAddSingleton<IJwtBlacklist, RedisJwtBlacklist>();
        }
        else
        {
            services.TryAddSingleton<IJwtBlacklist, InMemoryJwtBlacklist>();
        }
        return services;
    }
}
