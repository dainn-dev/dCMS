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

        // DAI-748 — every dCMS service is bound to a single client; missing config = misconfiguration.
        var clientId = configuration.GetSection("Dcms:Client")["Id"]?.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException(
                "Auth:Enabled is true but Dcms:Client.Id is missing. Set \"Dcms\": { \"Client\": { \"Id\": \"<chain>\" } } in appsettings.");

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

            o.AddPolicy(DcmsPolicies.ApprovalManage, p => p.RequireAuthenticatedUser().RequireRole(
                DcmsRoles.BrandManager,
                DcmsRoles.ChainAdmin,
                DcmsRoles.SuperAdmin));

            // DAI-684: bulk import — same scope as CatalogWrite (manager-level).
            o.AddPolicy(DcmsPolicies.CatalogImport, p => p.RequireAuthenticatedUser().RequireRole(
                DcmsRoles.StoreManager,
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

            // DAI-752 (US-5) — HQ ClientAdmin (or SuperAdmin) only.
            o.AddPolicy(DcmsPolicies.ClientAdminOnly, p => p.RequireAuthenticatedUser().RequireRole(
                DcmsRoles.ClientAdmin,
                DcmsRoles.SuperAdmin));

            // DAI-752 (US-5) — TenantAdmin or anyone broader. ChainAdmin retained for back-compat.
            o.AddPolicy(DcmsPolicies.TenantAdminOrAbove, p => p.RequireAuthenticatedUser().RequireRole(
                DcmsRoles.TenantAdmin,
                DcmsRoles.ChainAdmin,
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

    /// <summary>DAI-700 (AC3) — applies brand scope match (route/query brandId vs JWT brand claim) when auth is enabled.</summary>
    public static RouteGroupBuilder WithBrandAccess(this RouteGroupBuilder group, IConfiguration configuration)
    {
        if (configuration.IsDcmsAuthEnabled())
            group.AddEndpointFilter<BrandScopeAccessEndpointFilter>();
        return group;
    }

    /// <summary>DAI-700 (AC3) — per-endpoint variant of brand scope match for routes outside a dedicated group.</summary>
    public static RouteHandlerBuilder WithBrandAccess(this RouteHandlerBuilder builder, IConfiguration configuration)
    {
        if (configuration.IsDcmsAuthEnabled())
            builder.AddEndpointFilter<BrandScopeAccessEndpointFilter>();
        return builder;
    }

    /// <summary>DAI-700 (AC4) — applies bulk store scope match by reading <c>storeIds</c> from the JSON body.</summary>
    public static RouteHandlerBuilder WithStoresFromBodyAccess(this RouteHandlerBuilder builder, IConfiguration configuration)
    {
        if (configuration.IsDcmsAuthEnabled())
            builder.AddEndpointFilter<StoresFromBodyAccessEndpointFilter>();
        return builder;
    }

    /// <summary>DAI-748 (US-1) — applies client_id check (token must belong to this deployment's client).</summary>
    public static RouteGroupBuilder WithClientAccess(this RouteGroupBuilder group, IConfiguration configuration)
    {
        if (configuration.IsDcmsAuthEnabled())
            group.AddEndpointFilter<ClientScopeAccessEndpointFilter>();
        return group;
    }

    /// <summary>DAI-748 (US-1) — per-endpoint variant of the client_id check.</summary>
    public static RouteHandlerBuilder WithClientAccess(this RouteHandlerBuilder builder, IConfiguration configuration)
    {
        if (configuration.IsDcmsAuthEnabled())
            builder.AddEndpointFilter<ClientScopeAccessEndpointFilter>();
        return builder;
    }
}
