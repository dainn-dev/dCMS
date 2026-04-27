-- DAI-649 — denormalized customer snapshot on Order.
-- Captured at order creation so OrderDetailPage can show real name/email/phone
-- without a cross-service Customer API lookup. Existing orders stay NULL.
ALTER TABLE "Orders"
    ADD COLUMN IF NOT EXISTS "CustomerName" VARCHAR(200) NULL;

ALTER TABLE "Orders"
    ADD COLUMN IF NOT EXISTS "CustomerEmail" VARCHAR(320) NULL;

ALTER TABLE "Orders"
    ADD COLUMN IF NOT EXISTS "CustomerPhone" VARCHAR(64) NULL;
