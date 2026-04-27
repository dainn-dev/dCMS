-- DAI-693: Snapshot promotion adjustments on the order.

ALTER TABLE "OrderItems"
    ADD COLUMN IF NOT EXISTS "LineDiscount" NUMERIC(18,2) NOT NULL DEFAULT 0;

ALTER TABLE "Orders"
    ADD COLUMN IF NOT EXISTS "OrderDiscount" NUMERIC(18,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "PromoCode"     VARCHAR(100)  NULL,
    ADD COLUMN IF NOT EXISTS "PromoCodeId"   VARCHAR(36)   NULL;

CREATE TABLE IF NOT EXISTS "OrderPromotions"
(
    "Id"          VARCHAR(36)   NOT NULL PRIMARY KEY,
    "TenantId"    VARCHAR(64)   NOT NULL,
    "OrderId"     VARCHAR(64)   NOT NULL,
    "CampaignId"  VARCHAR(64)   NOT NULL,
    "EditorKind"  VARCHAR(32)   NOT NULL,
    "Name"        VARCHAR(200)  NOT NULL DEFAULT '',
    "Amount"      NUMERIC(18,2) NOT NULL DEFAULT 0,
    "PromoCode"   VARCHAR(100)  NULL,
    "AppliedAt"   TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_OrderPromotions_Order"
    ON "OrderPromotions" ("TenantId", "OrderId");
