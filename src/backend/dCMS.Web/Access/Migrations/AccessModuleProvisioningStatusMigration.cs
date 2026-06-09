using Umbraco.Cms.Infrastructure.Migrations;

namespace dCMS.Web.Access.Migrations;

/// <summary>
/// Adds provisioning lifecycle columns to <c>dcms_tenants</c> for backoffice UI display.
/// These are denormalized copies — source of truth lives in shared Catalog PostgreSQL.
/// </summary>
public sealed class AccessModuleProvisioningStatusMigration : AsyncMigrationBase
{
    public AccessModuleProvisioningStatusMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        Execute.Sql("""
            IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = N'dcms_tenants' AND COLUMN_NAME = N'provisioning_status'
            )
            BEGIN
                ALTER TABLE dcms_tenants
                    ADD provisioning_status NVARCHAR(24) NOT NULL
                        CONSTRAINT df_dcms_tenants_prov_status DEFAULT N'requested';
            END
            """).Do();

        Execute.Sql("""
            IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = N'dcms_tenants' AND COLUMN_NAME = N'provisioning_run_id'
            )
            BEGIN
                ALTER TABLE dcms_tenants
                    ADD provisioning_run_id NVARCHAR(36) NULL;
            END
            """).Do();

        Execute.Sql("""
            IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = N'dcms_tenants' AND COLUMN_NAME = N'plan_tier'
            )
            BEGIN
                ALTER TABLE dcms_tenants
                    ADD plan_tier NVARCHAR(24) NOT NULL
                        CONSTRAINT df_dcms_tenants_plan_tier DEFAULT N'starter';
            END
            """).Do();

        Execute.Sql("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_dcms_tenants_prov_status')
                CREATE INDEX ix_dcms_tenants_prov_status ON dcms_tenants (provisioning_status);
            """).Do();

        return Task.CompletedTask;
    }
}
