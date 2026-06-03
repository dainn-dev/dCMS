-- Product Page / SEO metadata + visibility flags + manual recommendations.
--
-- Backs the "Product Page" and "Recommendations" tabs of the backoffice product
-- editor. Previously these tabs were UI-only; this migration gives them
-- first-class, tenant/store-scoped storage on the existing Products row (for the
-- scalar/jsonb metadata) plus a dedicated join table for manual recommendations.

-- ── Product Page settings & SEO (multi-language values stored as JSONB text) ──
ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "PageTitle"        JSONB       NOT NULL DEFAULT '{}'::jsonb;
ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "MetaKeywords"     JSONB       NOT NULL DEFAULT '{}'::jsonb;
ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "MetaDescription"  JSONB       NOT NULL DEFAULT '{}'::jsonb;

-- ── Publish window (storefront scheduling). NULL = no bound. ──
ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "PublishFrom"      TIMESTAMPTZ NULL;
ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "PublishUntil"     TIMESTAMPTZ NULL;

-- ── Visibility / recommendation behaviour flags ──
ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "RecommendSimilar"     BOOLEAN     NOT NULL DEFAULT TRUE;
ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "RecommendationsMode"  VARCHAR(16) NOT NULL DEFAULT 'auto';
ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "RestockNotification"  BOOLEAN     NOT NULL DEFAULT FALSE;

COMMENT ON COLUMN "Products"."PageTitle"           IS 'Multi-language storefront <title> / page title. JSON map keyed by language (e.g. {"vi":"…","en":"…"}).';
COMMENT ON COLUMN "Products"."MetaKeywords"        IS 'Multi-language SEO meta keywords (comma-separated per locale). JSON map keyed by language.';
COMMENT ON COLUMN "Products"."MetaDescription"     IS 'Multi-language SEO meta description. JSON map keyed by language.';
COMMENT ON COLUMN "Products"."PublishFrom"         IS 'Storefront publish window start (UTC). NULL = publish immediately.';
COMMENT ON COLUMN "Products"."PublishUntil"        IS 'Storefront publish window end (UTC). NULL = no expiry.';
COMMENT ON COLUMN "Products"."RecommendSimilar"    IS 'When true the storefront may auto-recommend similar products on this PDP.';
COMMENT ON COLUMN "Products"."RecommendationsMode" IS 'Recommendation strategy: auto | manual | disabled.';
COMMENT ON COLUMN "Products"."RestockNotification" IS 'When true, customers are notified automatically when this product is restocked.';

-- ── Manual product recommendations (related products) ──
-- One row per (product → recommended product) edge, ordered by SortOrder. Tenant/
-- store are denormalised here so list/delete queries stay single-table and the
-- isolation key is always present without joining back to Products.
CREATE TABLE IF NOT EXISTS "ProductRecommendations"
(
    "ProductId"            VARCHAR(64) NOT NULL,
    "RecommendedProductId" VARCHAR(64) NOT NULL,
    "TenantId"             VARCHAR(64) NOT NULL,
    "StoreId"              VARCHAR(64) NOT NULL,
    "SortOrder"            INT         NOT NULL DEFAULT 0,
    "CreatedAt"            TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT "PK_ProductRecommendations" PRIMARY KEY ("ProductId", "RecommendedProductId"),
    CONSTRAINT "CK_ProductRecommendations_NotSelf" CHECK ("ProductId" <> "RecommendedProductId")
);

CREATE INDEX IF NOT EXISTS "IX_ProductRecommendations_Product"
    ON "ProductRecommendations" ("ProductId", "SortOrder");

COMMENT ON TABLE "ProductRecommendations" IS 'Manually curated "related products" edges for a product''s storefront PDP (Recommendations tab).';
