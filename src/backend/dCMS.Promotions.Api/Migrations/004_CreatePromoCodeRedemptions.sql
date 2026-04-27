-- DAI-692: PromoCode redemptions + binding/cap extensions to PromoCodes.

ALTER TABLE "PromoCodes"
    ADD COLUMN IF NOT EXISTS "CampaignId"          VARCHAR(64)  NULL,
    ADD COLUMN IF NOT EXISTS "CustomerId"          VARCHAR(64)  NULL,
    ADD COLUMN IF NOT EXISTS "GroupId"             VARCHAR(64)  NULL,
    ADD COLUMN IF NOT EXISTS "MaxUsesPerCustomer"  INTEGER      NULL,
    ADD COLUMN IF NOT EXISTS "MaxTotalUses"        INTEGER      NULL,
    ADD COLUMN IF NOT EXISTS "ExcludedProductsJson" TEXT        NOT NULL DEFAULT '[]';

CREATE TABLE IF NOT EXISTS "PromoCodeRedemptions"
(
    "Id"          VARCHAR(36)    NOT NULL PRIMARY KEY,
    "TenantId"    VARCHAR(64)    NOT NULL,
    "PromoCodeId" VARCHAR(36)    NOT NULL,
    "OrderId"     VARCHAR(64)    NOT NULL,
    "CustomerId"  VARCHAR(64)    NULL,
    "GroupId"     VARCHAR(64)    NULL,
    "Amount"      NUMERIC(18,2)  NOT NULL DEFAULT 0,
    "Currency"    VARCHAR(8)     NOT NULL DEFAULT '',
    "Status"      VARCHAR(16)    NOT NULL DEFAULT 'confirmed',
    "RedeemedAt"  TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    "ReleasedAt"  TIMESTAMPTZ    NULL,
    CONSTRAINT "CK_PromoCodeRedemptions_Status"
        CHECK ("Status" IN ('confirmed','released')),
    CONSTRAINT "UX_PromoCodeRedemptions_Tenant_Code_Order"
        UNIQUE ("TenantId", "PromoCodeId", "OrderId")
);

CREATE INDEX IF NOT EXISTS "IX_PromoCodeRedemptions_Customer"
    ON "PromoCodeRedemptions" ("TenantId", "CustomerId", "PromoCodeId")
    WHERE "Status" = 'confirmed';

CREATE INDEX IF NOT EXISTS "IX_PromoCodeRedemptions_Code"
    ON "PromoCodeRedemptions" ("TenantId", "PromoCodeId")
    WHERE "Status" = 'confirmed';

CREATE INDEX IF NOT EXISTS "IX_PromoCodeRedemptions_Group"
    ON "PromoCodeRedemptions" ("TenantId", "CustomerId", "GroupId")
    WHERE "Status" = 'confirmed' AND "GroupId" IS NOT NULL;
