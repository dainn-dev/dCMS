-- Migration 044: DAI-52 enterprise platform scale (webhooks, usage, integrations, TLS, region)

ALTER TABLE "TenantProvisioning"
    ADD COLUMN IF NOT EXISTS "Region" TEXT NOT NULL DEFAULT 'default';

ALTER TABLE "TenantDomainBindings"
    ADD COLUMN IF NOT EXISTS "TlsStatus" TEXT NOT NULL DEFAULT 'pending'
        CHECK ("TlsStatus" IN ('pending', 'provisioning', 'active', 'failed', 'expired')),
    ADD COLUMN IF NOT EXISTS "CertExpiresAt" TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS "AcmeChallengeToken" TEXT NULL;

CREATE TABLE IF NOT EXISTS "TenantWebhookSubscriptions" (
    "Id" TEXT PRIMARY KEY,
    "TenantId" TEXT NOT NULL,
    "Url" TEXT NOT NULL,
    "Secret" TEXT NOT NULL,
    "Events" JSONB NOT NULL DEFAULT '[]',
    "Status" TEXT NOT NULL CHECK ("Status" IN ('active', 'disabled', 'failed')) DEFAULT 'active',
    "FailureCount" INTEGER NOT NULL DEFAULT 0,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "TenantWebhookSubscriptions_FK_TenantProvisioning"
        FOREIGN KEY ("TenantId") REFERENCES "TenantProvisioning" ("TenantId") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_TenantWebhookSubscriptions_TenantId"
    ON "TenantWebhookSubscriptions" ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_TenantWebhookSubscriptions_Status"
    ON "TenantWebhookSubscriptions" ("Status");

CREATE TABLE IF NOT EXISTS "TenantWebhookDeliveries" (
    "Id" BIGSERIAL PRIMARY KEY,
    "SubscriptionId" TEXT NOT NULL,
    "TenantId" TEXT NOT NULL,
    "EventType" TEXT NOT NULL,
    "PayloadJson" JSONB NOT NULL,
    "IdempotencyKey" TEXT NOT NULL,
    "Status" TEXT NOT NULL CHECK ("Status" IN ('pending', 'delivered', 'failed', 'dead_letter')) DEFAULT 'pending',
    "AttemptCount" INTEGER NOT NULL DEFAULT 0,
    "LastHttpStatus" INTEGER NULL,
    "LastError" TEXT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "DeliveredAt" TIMESTAMPTZ NULL,
    CONSTRAINT "TenantWebhookDeliveries_FK_Subscription"
        FOREIGN KEY ("SubscriptionId") REFERENCES "TenantWebhookSubscriptions" ("Id") ON DELETE CASCADE,
    CONSTRAINT "TenantWebhookDeliveries_UniqueIdempotency"
        UNIQUE ("SubscriptionId", "IdempotencyKey")
);

CREATE INDEX IF NOT EXISTS "IX_TenantWebhookDeliveries_TenantId_CreatedAt"
    ON "TenantWebhookDeliveries" ("TenantId", "CreatedAt" DESC);

CREATE TABLE IF NOT EXISTS "IntegrationApps" (
    "Id" TEXT PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Description" TEXT NULL,
    "Scopes" JSONB NOT NULL DEFAULT '[]',
    "EventTypes" JSONB NOT NULL DEFAULT '[]',
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "TenantIntegrations" (
    "Id" TEXT PRIMARY KEY,
    "TenantId" TEXT NOT NULL,
    "AppId" TEXT NOT NULL,
    "ClientId" TEXT NOT NULL,
    "ClientSecretHash" TEXT NOT NULL,
    "Status" TEXT NOT NULL CHECK ("Status" IN ('active', 'revoked')) DEFAULT 'active',
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "TenantIntegrations_FK_TenantProvisioning"
        FOREIGN KEY ("TenantId") REFERENCES "TenantProvisioning" ("TenantId") ON DELETE CASCADE,
    CONSTRAINT "TenantIntegrations_FK_App"
        FOREIGN KEY ("AppId") REFERENCES "IntegrationApps" ("Id") ON DELETE CASCADE,
    CONSTRAINT "TenantIntegrations_UniqueClient"
        UNIQUE ("ClientId")
);

CREATE INDEX IF NOT EXISTS "IX_TenantIntegrations_TenantId"
    ON "TenantIntegrations" ("TenantId");

CREATE TABLE IF NOT EXISTS "TenantUsageDaily" (
    "TenantId" TEXT NOT NULL,
    "UsageDate" DATE NOT NULL,
    "OrdersCount" BIGINT NOT NULL DEFAULT 0,
    "ApiCallsCount" BIGINT NOT NULL DEFAULT 0,
    "WebhookDeliveriesCount" BIGINT NOT NULL DEFAULT 0,
    "ActiveProductsCount" BIGINT NOT NULL DEFAULT 0,
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY ("TenantId", "UsageDate"),
    CONSTRAINT "TenantUsageDaily_FK_TenantProvisioning"
        FOREIGN KEY ("TenantId") REFERENCES "TenantProvisioning" ("TenantId") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "TenantFeatureOverrides" (
    "TenantId" TEXT NOT NULL,
    "Feature" TEXT NOT NULL,
    "Enabled" BOOLEAN NOT NULL,
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY ("TenantId", "Feature"),
    CONSTRAINT "TenantFeatureOverrides_FK_TenantProvisioning"
        FOREIGN KEY ("TenantId") REFERENCES "TenantProvisioning" ("TenantId") ON DELETE CASCADE
);
