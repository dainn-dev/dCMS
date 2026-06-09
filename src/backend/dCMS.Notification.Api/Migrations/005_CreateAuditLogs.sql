-- DAI-687: audit trail for template upserts. The Notification service owns its own
-- AuditLogs table (service-isolated) rather than reaching into the Catalog database.
-- Schema mirrors Catalog's "AuditLogs" so downstream tooling can read both uniformly.
CREATE TABLE IF NOT EXISTS "AuditLogs"
(
    "Id"         BIGINT       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "TenantId"   VARCHAR(64)  NOT NULL,
    "StoreId"    VARCHAR(64)  NOT NULL,
    "UserId"     VARCHAR(64)  NOT NULL,
    "UserRole"   VARCHAR(64)  NOT NULL,
    "Action"     VARCHAR(64)  NOT NULL,
    "EntityType" VARCHAR(64)  NOT NULL,
    "EntityId"   TEXT         NOT NULL,
    "Diff"       TEXT         NULL,
    "IpAddress"  VARCHAR(64)  NOT NULL,
    "CreatedAt"  TIMESTAMPTZ  NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS "IX_AuditLogs_Entity"
    ON "AuditLogs" ("EntityType", "EntityId");

CREATE INDEX IF NOT EXISTS "IX_AuditLogs_User"
    ON "AuditLogs" ("UserId", "CreatedAt");

CREATE INDEX IF NOT EXISTS "IX_AuditLogs_Tenant"
    ON "AuditLogs" ("TenantId", "CreatedAt");
