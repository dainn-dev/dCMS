-- Minimal audit + notification tables.
CREATE TABLE IF NOT EXISTS "AuditLogs"
(
    "Id"         BIGSERIAL PRIMARY KEY,
    "TenantId"   VARCHAR(64) NOT NULL,
    "StoreId"    VARCHAR(64) NOT NULL,
    "UserId"     VARCHAR(64) NOT NULL,
    "UserRole"   VARCHAR(64) NOT NULL,
    "Action"     VARCHAR(64) NOT NULL,
    "EntityType" VARCHAR(64) NOT NULL,
    "EntityId"   VARCHAR(64) NOT NULL,
    "Diff"       TEXT        NULL,
    "IpAddress"  VARCHAR(64) NOT NULL,
    "CreatedAt"  TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_AuditLogs_Entity" ON "AuditLogs" ("EntityType", "EntityId");
CREATE INDEX IF NOT EXISTS "IX_AuditLogs_User" ON "AuditLogs" ("UserId", "CreatedAt");

CREATE TABLE IF NOT EXISTS "ApprovalComments"
(
    "Id"        SERIAL PRIMARY KEY,
    "ProductId" VARCHAR(64) NOT NULL,
    "UserId"    VARCHAR(64) NOT NULL,
    "Role"      VARCHAR(64) NOT NULL,
    "Message"   TEXT        NOT NULL,
    "Type"      VARCHAR(32) NOT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_ApprovalComments_Product" ON "ApprovalComments" ("ProductId", "CreatedAt");

CREATE TABLE IF NOT EXISTS "NotificationEvents"
(
    "Id"        BIGSERIAL PRIMARY KEY,
    "TenantId"  VARCHAR(64) NOT NULL,
    "UserId"    VARCHAR(64) NOT NULL,
    "Type"      VARCHAR(64) NOT NULL,
    "EntityId"  VARCHAR(64) NOT NULL,
    "Message"   TEXT        NOT NULL,
    "ReadAt"    TIMESTAMPTZ NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_NotificationEvents_User" ON "NotificationEvents" ("UserId", "CreatedAt" DESC);
