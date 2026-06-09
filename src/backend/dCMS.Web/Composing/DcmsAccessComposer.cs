using dCMS.Billing.Domain;
using dCMS.Web.Access.Migrations;
using dCMS.Web.Access.Caching;
using dCMS.Web.Access.Services;
using dCMS.Web.Billing;
using dCMS.Infrastructure.Billing;
using dCMS.Infrastructure.Platform;
using dCMS.Infrastructure.Provisioning;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;

namespace dCMS.Web.Composing;

/// <summary>Registers dCMS Access module services and runs the DB migration plan on startup.</summary>
public sealed class DcmsAccessComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddMemoryCache();
        builder.Services.AddOptions<PermissionCacheOptions>()
            .Bind(builder.Config.GetSection(PermissionCacheOptions.SectionName));

        // Cluster-safe permission cache: Redis if configured, otherwise in-memory only.
        var redisCs = builder.Config.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisCs))
        {
            builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ =>
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisCs));
        }

        builder.Services.AddSingleton<IPermissionCache, PermissionCache>();

        builder.Services.AddDcmsTenantEntitlements(builder.Config);
        if (!string.IsNullOrWhiteSpace(builder.Config.GetConnectionString("Catalog")))
        {
            builder.Services.AddDcmsTenantProvisioning(builder.Config);
            builder.Services.AddDcmsPlatformScale(builder.Config);
            builder.Services.AddHttpClient("tenant-webhooks")
                .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));
        }
        builder.Services.AddSingleton<ITenantEntitlementRepository, SqlTenantEntitlementRepository>();
        builder.Services.AddSingleton<ITenantEntitlementPublisher, TenantEntitlementPublisher>();

        // Register the permission service as scoped (one-per-request).
        builder.Services.AddScoped<IPermissionService, PermissionService>();

        // Run the Umbraco DB migration plan to create Access module tables on startup.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, RunAccessMigrationHandler>();
    }
}

/// <summary>Runs <see cref="AccessModuleMigrationPlan"/> on startup to ensure Access tables exist in the Umbraco DB.</summary>
internal sealed class RunAccessMigrationHandler
    : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    private readonly IMigrationPlanExecutor _migrationPlanExecutor;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IKeyValueService _keyValueService;
    private readonly IRuntimeState _runtimeState;

    public RunAccessMigrationHandler(
        IMigrationPlanExecutor migrationPlanExecutor,
        ICoreScopeProvider scopeProvider,
        IKeyValueService keyValueService,
        IRuntimeState runtimeState)
    {
        _migrationPlanExecutor = migrationPlanExecutor;
        _scopeProvider = scopeProvider;
        _keyValueService = keyValueService;
        _runtimeState = runtimeState;
    }

    public async Task HandleAsync(
        UmbracoApplicationStartingNotification notification,
        CancellationToken cancellationToken)
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
            return;

        var currentState = _keyValueService.GetValue($"Umbraco.Migrations.{AccessModuleMigrationPlan.PlanName}");
        if (currentState == AccessModuleMigrationPlan.CurrentFinalState)
            return;

        var upgrader = new Upgrader(new AccessModuleMigrationPlan());
        await upgrader.ExecuteAsync(_migrationPlanExecutor, _scopeProvider, _keyValueService).ConfigureAwait(false);
    }
}
