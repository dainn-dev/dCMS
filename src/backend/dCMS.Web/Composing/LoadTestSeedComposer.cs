using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;

namespace dCMS.Web.Composing;

/// <summary>
/// DAI-705 — env-gated composer that seeds <c>dcms_role_module_permissions</c> + links
/// Umbraco user 1 to the <c>dcmsOperations</c> group so the permission cache load test
/// exercises a real "allowed=true" path.
/// Enabled only when <c>Dcms:Access:LoadTestSeedEnabled=true</c>. Idempotent.
/// </summary>
public sealed class LoadTestSeedComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, RunLoadTestSeedHandler>();
    }
}

internal sealed class RunLoadTestSeedHandler
    : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    private readonly IUmbracoDatabaseFactory _dbFactory;
    private readonly IConfiguration _configuration;
    private readonly IRuntimeState _runtimeState;
    private readonly ILogger<RunLoadTestSeedHandler> _logger;

    public RunLoadTestSeedHandler(
        IUmbracoDatabaseFactory dbFactory,
        IConfiguration configuration,
        IRuntimeState runtimeState,
        ILogger<RunLoadTestSeedHandler> logger)
    {
        _dbFactory = dbFactory;
        _configuration = configuration;
        _runtimeState = runtimeState;
        _logger = logger;
    }

    public async Task HandleAsync(
        UmbracoApplicationStartingNotification notification,
        CancellationToken cancellationToken)
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
            return;

        var enabled = _configuration["Dcms:Access:LoadTestSeedEnabled"];
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            using var db = _dbFactory.CreateDatabase();

            // Permission grants for dcmsOperations on the orders module.
            await db.ExecuteAsync("""
                IF NOT EXISTS (SELECT 1 FROM dcms_role_module_permissions
                               WHERE role_alias = N'dcmsOperations' AND module = N'orders' AND action = N'view')
                    INSERT INTO dcms_role_module_permissions (role_alias, module, action, granted)
                    VALUES (N'dcmsOperations', N'orders', N'view', 1);

                IF NOT EXISTS (SELECT 1 FROM dcms_role_module_permissions
                               WHERE role_alias = N'dcmsOperations' AND module = N'orders' AND action = N'create')
                    INSERT INTO dcms_role_module_permissions (role_alias, module, action, granted)
                    VALUES (N'dcmsOperations', N'orders', N'create', 1);

                IF NOT EXISTS (SELECT 1 FROM dcms_role_module_permissions
                               WHERE role_alias = N'dcmsOperations' AND module = N'orders' AND action = N'update')
                    INSERT INTO dcms_role_module_permissions (role_alias, module, action, granted)
                    VALUES (N'dcmsOperations', N'orders', N'update', 1);
                """).ConfigureAwait(false);

            // Link user 1 to a 'dcmsOperations' group if such a group exists in Umbraco.
            await db.ExecuteAsync("""
                DECLARE @groupId INT = (SELECT TOP 1 id FROM umbracoUserGroup WHERE userGroupAlias = N'dcmsOperations');
                IF @groupId IS NOT NULL AND EXISTS (SELECT 1 FROM umbracoUser WHERE id = 1)
                   AND NOT EXISTS (SELECT 1 FROM umbracoUser2UserGroup WHERE userId = 1 AND userGroupId = @groupId)
                    INSERT INTO umbracoUser2UserGroup (userId, userGroupId) VALUES (1, @groupId);
                """).ConfigureAwait(false);

            _logger.LogInformation("DAI-705 load test seed applied (Dcms:Access:LoadTestSeedEnabled=true).");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DAI-705 load test seed failed — continuing startup.");
        }
    }
}
