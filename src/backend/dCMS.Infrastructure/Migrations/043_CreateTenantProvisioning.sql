-- Migration 043: Tenant Provisioning Lifecycle & Onboarding Status
-- Creates state machine tables for first-paying-tenant provisioning workflow
-- with idempotent retry/rollback support and operator audit trail

-- ============================================================================
-- TenantProvisioning — Source of truth for provisioning state
-- ============================================================================
CREATE TABLE IF NOT EXISTS "TenantProvisioning" (
    "TenantId" TEXT PRIMARY KEY,
    "TenantCode" TEXT NOT NULL UNIQUE,
    "Status" TEXT NOT NULL CHECK ("Status" IN (
        'requested', 'provisioning', 'failing', 'retrying',
        'active', 'suspended', 'rollback', 'deprovisioned'
    )),
    "PlanTier" TEXT NOT NULL CHECK ("PlanTier" IN (
        'starter', 'pro', 'enterprise'
    )) DEFAULT 'starter',

    -- Infrastructure details
    "UmbracoDbName" TEXT NULL,
    "EnvFilePath" TEXT NULL,
    "PrimaryDomain" TEXT NULL,

    -- Current run tracking
    "CurrentRunId" UUID NULL,
    "LastSuccessfulRunId" UUID NULL,

    -- Onboarding completion
    "OnboardingComplete" BOOLEAN NOT NULL DEFAULT FALSE,
    "OnboardingCompletedAt" TIMESTAMPTZ NULL,

    -- State transition timestamps
    "RequestedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "RequestedBy" TEXT NULL,
    "ProvisioningStartedAt" TIMESTAMPTZ NULL,
    "ProvisionedAt" TIMESTAMPTZ NULL,
    "SuspendedAt" TIMESTAMPTZ NULL,
    "DeprovisionedAt" TIMESTAMPTZ NULL,
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Failure tracking
    "LastFailureMessage" TEXT NULL,
    "FailureCount" INTEGER NOT NULL DEFAULT 0,

    CONSTRAINT "TenantProvisioning_ValidOnboardingCompletion"
        CHECK ("OnboardingComplete" = FALSE OR "OnboardingCompletedAt" IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS "IX_TenantProvisioning_Status"
    ON "TenantProvisioning" ("Status");
CREATE INDEX IF NOT EXISTS "IX_TenantProvisioning_PlanTier"
    ON "TenantProvisioning" ("PlanTier");
CREATE INDEX IF NOT EXISTS "IX_TenantProvisioning_CurrentRunId"
    ON "TenantProvisioning" ("CurrentRunId") WHERE "CurrentRunId" IS NOT NULL;

-- ============================================================================
-- ProvisioningSteps — Per-run step tracking with retry/rollback state
-- ============================================================================
CREATE TABLE IF NOT EXISTS "ProvisioningSteps" (
    "Id" BIGSERIAL PRIMARY KEY,
    "TenantId" TEXT NOT NULL,
    "RunId" UUID NOT NULL,
    "StepOrder" INTEGER NOT NULL,
    "StepName" TEXT NOT NULL,

    -- Step execution state
    "Status" TEXT NOT NULL CHECK ("Status" IN (
        'pending', 'running', 'succeeded', 'failed', 'skipped', 'rolled_back'
    )),
    "AttemptCount" INTEGER NOT NULL DEFAULT 0,
    "MaxRetries" INTEGER NOT NULL DEFAULT 2,

    -- Failure tracking
    "ErrorMessage" TEXT NULL,
    "LastAttemptAt" TIMESTAMPTZ NULL,

    -- Idempotency checkpoint (JSONB for flexibility)
    -- Example: {"db_created": true, "db_name": "umbraco_t_acme"}
    "Checkpoint" JSONB NOT NULL DEFAULT '{}',

    -- Rollback tracking
    "RollbackStatus" TEXT NULL CHECK ("RollbackStatus" IS NULL OR "RollbackStatus" IN (
        'pending', 'running', 'succeeded', 'failed'
    )),
    "RollbackAttemptedAt" TIMESTAMPTZ NULL,
    "RollbackErrorMessage" TEXT NULL,

    -- Timestamps
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "CompletedAt" TIMESTAMPTZ NULL,

    CONSTRAINT "ProvisioningSteps_FK_TenantProvisioning"
        FOREIGN KEY ("TenantId") REFERENCES "TenantProvisioning" ("TenantId") ON DELETE CASCADE,
    CONSTRAINT "ProvisioningSteps_UniqueRunStep"
        UNIQUE ("TenantId", "RunId", "StepOrder")
);

CREATE INDEX IF NOT EXISTS "IX_ProvisioningSteps_TenantRun"
    ON "ProvisioningSteps" ("TenantId", "RunId");
CREATE INDEX IF NOT EXISTS "IX_ProvisioningSteps_Status"
    ON "ProvisioningSteps" ("Status");
CREATE INDEX IF NOT EXISTS "IX_ProvisioningSteps_StepName"
    ON "ProvisioningSteps" ("StepName");

-- ============================================================================
-- TenantDomainBindings — Domain → Tenant/Store mapping with Redis keys
-- ============================================================================
CREATE TABLE IF NOT EXISTS "TenantDomainBindings" (
    "Domain" TEXT PRIMARY KEY,
    "TenantId" TEXT NOT NULL,
    "StoreId" TEXT NOT NULL DEFAULT 'default',
    "IsPrimary" BOOLEAN NOT NULL DEFAULT FALSE,

    -- Binding state
    "Status" TEXT NOT NULL CHECK ("Status" IN (
        'pending', 'active', 'suspended', 'removed'
    )) DEFAULT 'pending',

    -- Redis key tracking (for rollback)
    "RedisHostKey" TEXT NULL, -- dcms:host:{domain}
    "RedisKeysWritten" JSONB NULL DEFAULT '[]',

    -- Timestamps
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "ActivatedAt" TIMESTAMPTZ NULL,
    "RemovedAt" TIMESTAMPTZ NULL,

    CONSTRAINT "TenantDomainBindings_FK_TenantProvisioning"
        FOREIGN KEY ("TenantId") REFERENCES "TenantProvisioning" ("TenantId") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_TenantDomainBindings_TenantId"
    ON "TenantDomainBindings" ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_TenantDomainBindings_Status"
    ON "TenantDomainBindings" ("Status");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_TenantDomainBindings_PrimaryPerTenant"
    ON "TenantDomainBindings" ("TenantId")
    WHERE "IsPrimary" = TRUE AND "Status" != 'removed';

-- ============================================================================
-- TenantOnboarding — Checklist items for first-paying-tenant completion
-- ============================================================================
CREATE TABLE IF NOT EXISTS "TenantOnboarding" (
    "Id" BIGSERIAL PRIMARY KEY,
    "TenantId" TEXT NOT NULL,
    "CheckItem" TEXT NOT NULL,

    -- Checklist state
    "Status" TEXT NOT NULL CHECK ("Status" IN (
        'pending', 'completed', 'skipped'
    )) DEFAULT 'pending',
    "IsRequired" BOOLEAN NOT NULL DEFAULT TRUE,

    -- Verification tracking
    "CompletedAt" TIMESTAMPTZ NULL,
    "VerifiedAt" TIMESTAMPTZ NULL,
    "VerifiedBy" TEXT NULL,
    "Notes" TEXT NULL,

    -- Timestamps
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT "TenantOnboarding_FK_TenantProvisioning"
        FOREIGN KEY ("TenantId") REFERENCES "TenantProvisioning" ("TenantId") ON DELETE CASCADE,
    CONSTRAINT "TenantOnboarding_UniqueCheckItem"
        UNIQUE ("TenantId", "CheckItem")
);

CREATE INDEX IF NOT EXISTS "IX_TenantOnboarding_TenantId"
    ON "TenantOnboarding" ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_TenantOnboarding_Status"
    ON "TenantOnboarding" ("Status");
CREATE INDEX IF NOT EXISTS "IX_TenantOnboarding_Required_Pending"
    ON "TenantOnboarding" ("TenantId", "IsRequired", "Status")
    WHERE "IsRequired" = TRUE AND "Status" = 'pending';

-- ============================================================================
-- ProvisioningAuditLog — Immutable audit trail for every state change
-- ============================================================================
CREATE TABLE IF NOT EXISTS "ProvisioningAuditLog" (
    "Id" BIGSERIAL PRIMARY KEY,
    "TenantId" TEXT NOT NULL,
    "RunId" UUID NULL,

    -- Operation tracking
    "Operation" TEXT NOT NULL, -- provision_start, step_succeeded, rollback_start, etc.
    "FromStatus" TEXT NULL,
    "ToStatus" TEXT NULL,

    -- Actor and context
    "Actor" TEXT NULL, -- cli_operator, api_user_{id}, system
    "Details" JSONB NOT NULL DEFAULT '{}', -- PII-safe context (NO passwords/tokens)

    -- Timestamp (immutable)
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT "ProvisioningAuditLog_FK_TenantProvisioning"
        FOREIGN KEY ("TenantId") REFERENCES "TenantProvisioning" ("TenantId") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_ProvisioningAuditLog_TenantId_CreatedAt"
    ON "ProvisioningAuditLog" ("TenantId", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS "IX_ProvisioningAuditLog_RunId"
    ON "ProvisioningAuditLog" ("RunId") WHERE "RunId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_ProvisioningAuditLog_Operation"
    ON "ProvisioningAuditLog" ("Operation");

-- ============================================================================
-- Seed onboarding checklist template
-- ============================================================================
-- Note: This is a reference comment — actual checklist seeding happens in
-- ProvisioningOrchestrator when a new tenant provisioning record is created.
-- Items:
--   - admin_login_verified (required)
--   - umbraco_content_synced (required)
--   - first_brand_created (required)
--   - first_store_created (required)
--   - domain_configured (required)
--   - smoke_test_passed (required)
--   - first_product_created (optional)
--   - payment_gateway_configured (optional)
--   - inventory_warehouse_created (optional)
--   - smtp_configured (optional)
--   - first_order_placed (optional)
