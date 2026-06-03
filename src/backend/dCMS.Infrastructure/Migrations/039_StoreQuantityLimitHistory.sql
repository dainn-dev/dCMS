-- Quantity limit Phase 2: append-only change history for general settings and advance rules.

CREATE TABLE IF NOT EXISTS "StoreQuantityLimitSettingsHistory"
(
    "Id"        BIGSERIAL PRIMARY KEY,
    "TenantId"  VARCHAR(64) NOT NULL,
    "StoreId"   VARCHAR(64) NOT NULL,
    "UserId"    VARCHAR(64) NOT NULL DEFAULT 'unknown',
    "UserRole"  VARCHAR(64) NOT NULL DEFAULT 'unknown',
    "Action"    VARCHAR(64) NOT NULL,
    "Snapshot"  JSONB       NOT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS "IX_StoreQuantityLimitSettingsHistory_Store"
    ON "StoreQuantityLimitSettingsHistory" ("TenantId", "StoreId", "CreatedAt" DESC);

COMMENT ON TABLE "StoreQuantityLimitSettingsHistory" IS 'Audit trail when cart limit or advance quantity-limit rules change.';
