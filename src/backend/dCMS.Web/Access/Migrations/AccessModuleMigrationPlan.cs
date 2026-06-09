using Umbraco.Cms.Infrastructure.Migrations;

namespace dCMS.Web.Access.Migrations;

/// <summary>Umbraco migration plan for the dCMS Access module (§8).</summary>
public sealed class AccessModuleMigrationPlan : MigrationPlan
{
    public const string PlanName = "dCMS.Access";
    public const string CurrentFinalState = "access-v1.4";

    public AccessModuleMigrationPlan() : base(PlanName)
    {
        From(string.Empty)
            .To<AccessModuleMigration>("access-v1.0")
            .To<AccessModuleTenantsAndRolesMetaMigration>("access-v1.1")
            .To<AccessModuleBulkJobsMigration>("access-v1.2")
            .To<AccessModuleBillingMigration>("access-v1.3")
            .To<AccessModuleProvisioningStatusMigration>(CurrentFinalState);
    }
}
