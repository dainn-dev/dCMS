using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace dCMS.AspNetCore.Auth;

public static class DcmsJwtAuthExtensions
{
    public static bool IsDcmsAuthEnabled(this IConfiguration configuration) =>
        configuration.GetSection(DcmsAuthOptions.SectionName).GetValue<bool?>(nameof(DcmsAuthOptions.Enabled)) ?? true;

    public static IServiceCollection AddDcmsJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var opt = configuration.GetSection(DcmsAuthOptions.SectionName).Get<DcmsAuthOptions>() ?? new DcmsAuthOptions();
        if (!opt.Enabled)
            return services;

        if (string.IsNullOrWhiteSpace(opt.JwtSigningKey) || opt.JwtSigningKey.Length < 32)
            throw new InvalidOperationException(
                $"Auth:Enabled is true but Auth:{nameof(DcmsAuthOptions.JwtSigningKey)} is missing or shorter than 32 characters.");

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opt.JwtSigningKey));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = opt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = opt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role
                };
            });

        services.AddAuthorization(o =>
        {
            o.AddPolicy(DcmsPolicies.CatalogRead, p => p.RequireAuthenticatedUser().RequireRole(
                DcmsRoles.StoreStaff,
                DcmsRoles.StoreManager,
                DcmsRoles.BrandManager,
                DcmsRoles.ChainAdmin,
                DcmsRoles.SuperAdmin));

            o.AddPolicy(DcmsPolicies.CatalogWrite, p => p.RequireAuthenticatedUser().RequireRole(
                DcmsRoles.StoreManager,
                DcmsRoles.BrandManager,
                DcmsRoles.ChainAdmin,
                DcmsRoles.SuperAdmin));

            o.AddPolicy(DcmsPolicies.CatalogApproval, p => p.RequireAuthenticatedUser().RequireRole(
                DcmsRoles.BrandManager,
                DcmsRoles.ChainAdmin,
                DcmsRoles.SuperAdmin));

            o.AddPolicy(DcmsPolicies.InventoryRead, p => p.RequireAuthenticatedUser().RequireRole(
                DcmsRoles.StoreStaff,
                DcmsRoles.StoreManager,
                DcmsRoles.BrandManager,
                DcmsRoles.ChainAdmin,
                DcmsRoles.SuperAdmin));

            o.AddPolicy(DcmsPolicies.InventoryWrite, p => p.RequireAuthenticatedUser().RequireRole(
                DcmsRoles.StoreStaff,
                DcmsRoles.StoreManager,
                DcmsRoles.BrandManager,
                DcmsRoles.ChainAdmin,
                DcmsRoles.SuperAdmin));

            o.AddPolicy(DcmsPolicies.OrderAccess, p => p.RequireAuthenticatedUser());

            o.AddPolicy(DcmsPolicies.OrderDlqAdmin, p => p.RequireAuthenticatedUser().RequireRole(DcmsRoles.SuperAdmin));

            o.AddPolicy(DcmsPolicies.OrderFailureManage, p => p.RequireAuthenticatedUser().RequireRole(
                DcmsRoles.ChainAdmin,
                DcmsRoles.StoreManager,
                DcmsRoles.CustomerSupport,
                DcmsRoles.SuperAdmin));
        });

        return services;
    }

    public static IApplicationBuilder UseDcmsJwtAuthentication(this WebApplication app, IConfiguration configuration)
    {
        if (!configuration.IsDcmsAuthEnabled())
            return app;

        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    /// <summary>Applies tenant/store route match when auth is enabled.</summary>
    public static RouteGroupBuilder WithTenantStoreAccess(this RouteGroupBuilder group, IConfiguration configuration)
    {
        if (configuration.IsDcmsAuthEnabled())
            group.AddEndpointFilter<TenantStoreAccessEndpointFilter>();
        return group;
    }

    /// <summary>Applies tenant/store header match when auth is enabled.</summary>
    public static RouteHandlerBuilder WithTenantStoreHeaderAccess(this RouteHandlerBuilder builder, IConfiguration configuration)
    {
        if (configuration.IsDcmsAuthEnabled())
            builder.AddEndpointFilter<TenantStoreHeaderAccessEndpointFilter>();
        return builder;
    }

    /// <summary>Applies tenant-only route match (no store scope) when auth is enabled. Used for Brand API and similar tenant-level endpoints.</summary>
    public static RouteGroupBuilder WithTenantAccess(this RouteGroupBuilder group, IConfiguration configuration)
    {
        if (configuration.IsDcmsAuthEnabled())
            group.AddEndpointFilter<TenantOnlyAccessEndpointFilter>();
        return group;
    }
}
