-- DAI-685 / Restock Notifications Subscriptions BRD §3:
-- the report needs per-subscription detail (UPC, SKU, Product Name, Email) which the original
-- aggregate-only projection didn't carry. Denormalise those onto the projection (populated by the
-- Phase-2 storefront consumer that emits the subscribe/notify events) so the report can display
-- and filter on them without a cross-DB Catalog lookup per row.
ALTER TABLE analytics.restock_subscriptions ADD COLUMN IF NOT EXISTS upc          text NULL;
ALTER TABLE analytics.restock_subscriptions ADD COLUMN IF NOT EXISTS sku          text NULL;
ALTER TABLE analytics.restock_subscriptions ADD COLUMN IF NOT EXISTS product_name text NULL;
ALTER TABLE analytics.restock_subscriptions ADD COLUMN IF NOT EXISTS email        text NULL;

-- Supports the report's primary filter: tenant + store scoped, ordered/filtered by subscription date.
CREATE INDEX IF NOT EXISTS ix_restock_subscriptions_tenant_store_created
    ON analytics.restock_subscriptions (tenant_id, store_id, created_at DESC);
