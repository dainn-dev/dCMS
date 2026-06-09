using Umbraco.Cms.Infrastructure.Migrations;

namespace dCMS.Web.Access.Migrations;

/// <summary>DAI-29: platform billing plans + tenant subscriptions (manual invoicing MVP).</summary>
public sealed class AccessModuleBillingMigration : AsyncMigrationBase
{
    public AccessModuleBillingMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        Execute.Sql("""
            IF OBJECT_ID(N'dcms_plans', N'U') IS NULL
            BEGIN
                CREATE TABLE dcms_plans (
                    id                   NVARCHAR(36)   NOT NULL CONSTRAINT pk_dcms_plans PRIMARY KEY,
                    code                 NVARCHAR(32)   NOT NULL,
                    name                 NVARCHAR(128)  NOT NULL,
                    max_brands           INT            NOT NULL,
                    max_active_products  INT            NOT NULL,
                    features_json        NVARCHAR(MAX)  NOT NULL CONSTRAINT df_dcms_plans_features DEFAULT '[]',
                    is_active            BIT            NOT NULL CONSTRAINT df_dcms_plans_active DEFAULT 1,
                    created_at           DATETIME2      NOT NULL CONSTRAINT df_dcms_plans_created DEFAULT GETUTCDATE(),
                    updated_at           DATETIME2      NOT NULL CONSTRAINT df_dcms_plans_updated DEFAULT GETUTCDATE(),
                    CONSTRAINT uq_dcms_plans_code UNIQUE (code)
                );
            END
            """).Do();

        Execute.Sql("""
            IF OBJECT_ID(N'dcms_tenant_subscriptions', N'U') IS NULL
            BEGIN
                CREATE TABLE dcms_tenant_subscriptions (
                    tenant_id              NVARCHAR(36)  NOT NULL CONSTRAINT pk_dcms_tenant_subscriptions PRIMARY KEY,
                    plan_id                NVARCHAR(36)  NOT NULL,
                    subscription_state     NVARCHAR(32)  NOT NULL,
                    manual_invoice_status  NVARCHAR(32)  NOT NULL CONSTRAINT df_dcms_tsub_invoice DEFAULT 'none',
                    trial_ends_at          DATETIME2     NULL,
                    current_period_start   DATETIME2     NULL,
                    current_period_end     DATETIME2     NULL,
                    pending_plan_id        NVARCHAR(36)  NULL,
                    suspended_at           DATETIME2     NULL,
                    cancelled_at           DATETIME2     NULL,
                    cancellation_reason    NVARCHAR(512) NOT NULL CONSTRAINT df_dcms_tsub_cancel_reason DEFAULT '',
                    invoice_reference      NVARCHAR(128) NOT NULL CONSTRAINT df_dcms_tsub_invoice_ref DEFAULT '',
                    invoice_notes          NVARCHAR(1024) NOT NULL CONSTRAINT df_dcms_tsub_invoice_notes DEFAULT '',
                    created_at             DATETIME2     NOT NULL CONSTRAINT df_dcms_tsub_created DEFAULT GETUTCDATE(),
                    updated_at             DATETIME2     NOT NULL CONSTRAINT df_dcms_tsub_updated DEFAULT GETUTCDATE(),
                    CONSTRAINT fk_dcms_tsub_tenant FOREIGN KEY (tenant_id) REFERENCES dcms_tenants (id),
                    CONSTRAINT fk_dcms_tsub_plan FOREIGN KEY (plan_id) REFERENCES dcms_plans (id),
                    CONSTRAINT fk_dcms_tsub_pending_plan FOREIGN KEY (pending_plan_id) REFERENCES dcms_plans (id)
                );
                CREATE INDEX ix_dcms_tsub_state ON dcms_tenant_subscriptions (subscription_state);
            END
            """).Do();

        Execute.Sql("""
            INSERT INTO dcms_plans (id, code, name, max_brands, max_active_products, features_json, is_active)
            SELECT v.id, v.code, v.name, v.max_brands, v.max_active_products, v.features_json, 1
            FROM (VALUES
                (N'a1000000000000000000000000000001', N'starter', N'Starter', 2, 500,
                 N'["catalog.read","catalog.write","orders.read","orders.write"]'),
                (N'a1000000000000000000000000000002', N'pro', N'Pro', 10, 5000,
                 N'["catalog.read","catalog.write","orders.read","orders.write","promotions.write"]'),
                (N'a1000000000000000000000000000003', N'enterprise', N'Enterprise', 100, 50000,
                 N'["catalog.read","catalog.write","orders.read","orders.write","promotions.write","reports.read","fulfillment.write"]')
            ) AS v(id, code, name, max_brands, max_active_products, features_json)
            WHERE NOT EXISTS (SELECT 1 FROM dcms_plans p WHERE p.code = v.code);
            """).Do();

        return Task.CompletedTask;
    }
}
