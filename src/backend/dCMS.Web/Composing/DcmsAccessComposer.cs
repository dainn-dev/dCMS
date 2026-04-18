using dCMS.Web.Access.Migrations;
using dCMS.Web.Access.Services;
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

        var upgrader = new Upgrader(new AccessModuleMigrationPlan());
        await upgrader.ExecuteAsync(_migrationPlanExecutor, _scopeProvider, _keyValueService).ConfigureAwait(false);
    }
}
