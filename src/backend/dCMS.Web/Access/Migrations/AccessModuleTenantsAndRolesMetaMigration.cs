using Umbraco.Cms.Infrastructure.Migrations;

namespace dCMS.Web.Access.Migrations;

/// <summary>DAI-668: <c>dcms_tenants</c> + <c>dcms_roles_meta</c> (isTenantRole / description) in Umbraco DB.</summary>
public sealed class AccessModuleTenantsAndRolesMetaMigration : AsyncMigrationBase
{
    public AccessModuleTenantsAndRolesMetaMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        Execute.Sql("""
            IF OBJECT_ID(N'dcms_tenants', N'U') IS NULL
            BEGIN
                CREATE TABLE dcms_tenants (
                    id             NVARCHAR(36)  NOT NULL CONSTRAINT pk_dcms_tenants PRIMARY KEY,
                    code           NVARCHAR(20)  NOT NULL,
                    name           NVARCHAR(256) NOT NULL,
                    contact_name   NVARCHAR(128) NOT NULL CONSTRAINT df_dcms_tenants_contact_name DEFAULT '',
                    contact_email  NVARCHAR(256) NOT NULL CONSTRAINT df_dcms_tenants_contact_email DEFAULT '',
                    brand_count    INT           NOT NULL CONSTRAINT df_dcms_tenants_brand_count DEFAULT 0,
                    active         BIT           NOT NULL CONSTRAINT df_dcms_tenants_active DEFAULT 1,
                    created_at     DATETIME2     NOT NULL CONSTRAINT df_dcms_tenants_created DEFAULT GETUTCDATE(),
                    updated_at     DATETIME2     NOT NULL CONSTRAINT df_dcms_tenants_updated DEFAULT GETUTCDATE(),
                    CONSTRAINT uq_dcms_tenants_code UNIQUE (code)
                );
                CREATE INDEX ix_dcms_tenants_active ON dcms_tenants (active);
            END
            """).Do();

        Execute.Sql("""
            IF OBJECT_ID(N'dcms_roles_meta', N'U') IS NULL
            BEGIN
                CREATE TABLE dcms_roles_meta (
                    role_alias      NVARCHAR(100) NOT NULL CONSTRAINT pk_dcms_roles_meta PRIMARY KEY,
                    is_tenant_role  BIT           NOT NULL CONSTRAINT df_dcms_roles_meta_tenant DEFAULT 0,
                    description     NVARCHAR(512) NOT NULL CONSTRAINT df_dcms_roles_meta_desc DEFAULT ''
                );
            END
            """).Do();

        // Seed defaults for EnsureDefaultUserGroups aliases (idempotent).
        Execute.Sql("""
            INSERT INTO dcms_roles_meta (role_alias, is_tenant_role, description)
            SELECT v.role_alias, v.is_tenant_role, v.description
            FROM (VALUES
                (N'dcmsItAdministrator',         0, N'Full system access including infrastructure and deployment.'),
                (N'dcmsSysAdministrator',        0, N'Platform-wide configuration and user management.'),
                (N'dcmsEcommerceManager',        0, N'Manages products, campaigns and promotions across all brands.'),
                (N'dcmsTenantProductManager',    1, N'Product catalogue management scoped to assigned tenants.'),
                (N'dcmsTenantInventoryManager',  1, N'Inventory and stock management for tenant-owned products.'),
                (N'dcmsOperations',              0, N'Order fulfilment, logistics and dispatch operations.'),
                (N'dcmsFinance',                 0, N'Financial reporting, invoicing and payment reconciliation.'),
                (N'dcmsBrandManager',            1, N'Brand profile management and marketing content publishing.'),
                (N'dcmsProductUpload',           1, N'Bulk product data import and catalogue upload operations.'),
                (N'dcmsGuest',                   0, N'Read-only access for external reviewers and auditors.')
            ) AS v(role_alias, is_tenant_role, description)
            WHERE NOT EXISTS (SELECT 1 FROM dcms_roles_meta m WHERE m.role_alias = v.role_alias);
            """).Do();

        return Task.CompletedTask;
    }
}
