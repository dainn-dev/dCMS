-- Per-store Best Seller widget configuration (eStore → Products → Best Seller Settings).
-- Previously persisted only in browser localStorage; this table makes settings tenant/store-scoped
-- and shared across all admins of a Siêu thị.
CREATE TABLE IF NOT EXISTS "StoreBestSellerSettings"
(
    "TenantId"  VARCHAR(64) NOT NULL,
    "StoreId"   VARCHAR(64) NOT NULL,
    "Settings"  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT "PK_StoreBestSellerSettings" PRIMARY KEY ("TenantId", "StoreId")
);

COMMENT ON TABLE  "StoreBestSellerSettings"            IS 'Store-scoped Best Seller widget rules (display, ranking logic, category/brand/product filters).';
COMMENT ON COLUMN "StoreBestSellerSettings"."Settings" IS 'JSON document: displayList, popularityDurationDays, genderBased, recommendationLogic, maxItems, whitelistedCategoryIds[], blacklistedCategoryIds[], whitelistedBrandIds[], blacklistedBrandIds[], includedProductIds[], excludedProductIds[], manualProductIds[].';
