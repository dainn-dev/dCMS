-- Best Seller Phase 2: page-view metric for ranking + settings change history audit trail.

ALTER TABLE "Products"
    ADD COLUMN IF NOT EXISTS "PageViews30d" INTEGER NOT NULL DEFAULT 0;

COMMENT ON COLUMN "Products"."PageViews30d" IS 'Rolling 30-day product page views (storefront analytics feed). Used by best-seller views ranking.';

CREATE TABLE IF NOT EXISTS "StoreBestSellerSettingsHistory"
(
    "Id"        BIGSERIAL PRIMARY KEY,
    "TenantId"  VARCHAR(64) NOT NULL,
    "StoreId"   VARCHAR(64) NOT NULL,
    "UserId"    VARCHAR(64) NOT NULL DEFAULT 'unknown',
    "UserRole"  VARCHAR(64) NOT NULL DEFAULT 'unknown',
    "Settings"  JSONB       NOT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE INDEX IF NOT EXISTS "IX_StoreBestSellerSettingsHistory_Store"
    ON "StoreBestSellerSettingsHistory" ("TenantId", "StoreId", "CreatedAt" DESC);

COMMENT ON TABLE "StoreBestSellerSettingsHistory" IS 'Append-only snapshots whenever store best-seller settings are saved.';
