using Umbraco.Cms.Infrastructure.Migrations;

namespace dCMS.Web.Access.Migrations;

/// <summary>DAI-684: <c>dcms_bulk_jobs</c> in Umbraco SQL Server (Hangfire job metadata).</summary>
public sealed class AccessModuleBulkJobsMigration : AsyncMigrationBase
{
    public AccessModuleBulkJobsMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        Execute.Sql("""
            IF OBJECT_ID(N'dcms_bulk_jobs', N'U') IS NULL
            BEGIN
                CREATE TABLE dcms_bulk_jobs (
                    id                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_dcms_bulk_jobs PRIMARY KEY,
                    tenant_id          NVARCHAR(64)  NOT NULL,
                    store_id           NVARCHAR(64)  NULL,
                    job_kind           NVARCHAR(32)  NOT NULL,
                    requested_by_user  INT            NOT NULL,
                    hangfire_job_id    NVARCHAR(128) NULL,
                    status             NVARCHAR(32)  NOT NULL,
                    progress_processed INT            NOT NULL CONSTRAINT df_dcms_bj_progress_proc DEFAULT 0,
                    progress_total     INT            NOT NULL CONSTRAINT df_dcms_bj_progress_tot DEFAULT 0,
                    progress_percent   INT            NOT NULL CONSTRAINT df_dcms_bj_progress_pct DEFAULT 0,
                    input_blob_ref     NVARCHAR(1024) NULL,
                    output_blob_ref    NVARCHAR(1024) NULL,
                    error_message      NVARCHAR(4000) NULL,
                    created_at         DATETIMEOFFSET NOT NULL,
                    started_at         DATETIMEOFFSET NULL,
                    finished_at        DATETIMEOFFSET NULL,
                    cancel_requested_at DATETIMEOFFSET NULL
                );
                CREATE INDEX ix_dcms_bulk_jobs_tenant_user_created
                    ON dcms_bulk_jobs (tenant_id, requested_by_user, created_at DESC);
                CREATE INDEX ix_dcms_bulk_jobs_tenant_kind_status
                    ON dcms_bulk_jobs (tenant_id, job_kind, status);
            END
            """).Do();

        return Task.CompletedTask;
    }
}
