using Umbraco.Cms.Infrastructure.Migrations;

namespace dCMS.Web.Access.Migrations;

/// <summary>Creates dCMS Access module tables in the Umbraco SQL Server database on first run.</summary>
public sealed class AccessModuleMigration : AsyncMigrationBase
{
    public AccessModuleMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        // Use OBJECT_ID guard so re-runs are idempotent.
        Execute.Sql("""
            IF OBJECT_ID(N'dcms_role_module_permissions', N'U') IS NULL
            BEGIN
                CREATE TABLE dcms_role_module_permissions (
                    role_alias  NVARCHAR(100) NOT NULL,
                    module      NVARCHAR(100) NOT NULL,
                    action      NVARCHAR(100) NOT NULL,
                    granted     BIT           NOT NULL CONSTRAINT df_rmp_granted DEFAULT 0,
                    CONSTRAINT pk_role_module_permissions PRIMARY KEY (role_alias, module, action)
                );
                CREATE INDEX ix_rmp_role_alias ON dcms_role_module_permissions (role_alias);
            END
            """).Do();

        Execute.Sql("""
            IF OBJECT_ID(N'dcms_user_notification_prefs', N'U') IS NULL
            BEGIN
                CREATE TABLE dcms_user_notification_prefs (
                    id                INT           NOT NULL IDENTITY(1,1) CONSTRAINT pk_user_notification_prefs PRIMARY KEY,
                    user_id           NVARCHAR(64)  NOT NULL,
                    notification_type NVARCHAR(100) NOT NULL,
                    enabled           BIT           NOT NULL CONSTRAINT df_unp_enabled DEFAULT 0,
                    tenant_ids        NVARCHAR(MAX) NULL,
                    CONSTRAINT uq_user_notification_prefs UNIQUE (user_id, notification_type)
                );
                CREATE INDEX ix_unp_user_id ON dcms_user_notification_prefs (user_id);
            END
            """).Do();

        return Task.CompletedTask;
    }
}
