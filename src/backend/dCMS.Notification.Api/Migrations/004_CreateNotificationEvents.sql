-- P2 #6: NotificationEvents migrated out of dcms_catalog into dcms_notification.
-- In-app user notifications (product_submitted, product_approved, product_rejected, product_request_changes,
-- and any future cross-domain user-targeted alerts).
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
CREATE INDEX IF NOT EXISTS "IX_NotificationEvents_Tenant_User" ON "NotificationEvents" ("TenantId", "UserId", "CreatedAt" DESC);
