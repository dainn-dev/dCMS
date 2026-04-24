using Umbraco.Cms.Infrastructure.Migrations;

namespace dCMS.Web.Access.Migrations;

/// <summary>Umbraco migration plan for the dCMS Access module (§8).</summary>
public sealed class AccessModuleMigrationPlan : MigrationPlan
{
    public const string PlanName = "dCMS.Access";

    public AccessModuleMigrationPlan() : base(PlanName)
    {
        From(string.Empty)
            .To<AccessModuleMigration>("access-v1.0")
            .To<AccessModuleTenantsAndRolesMetaMigration>("access-v1.1");
    }
}
