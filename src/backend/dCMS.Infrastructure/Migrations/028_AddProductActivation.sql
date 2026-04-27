-- DAI-720 / DAI-688: product approval activation flag.
-- Adds a separate IsActive column so Approval engine can flip activation independently of Status
-- (Status reflects lifecycle: draft/active/archived/etc; IsActive gates whether approved products
-- are surfaced in storefront search). Default false so existing rows stay inactive until reviewed.
ALTER TABLE "Products"
    ADD COLUMN IF NOT EXISTS "IsActive" BOOLEAN NOT NULL DEFAULT FALSE;

CREATE INDEX IF NOT EXISTS "IX_Products_Tenant_IsActive"
    ON "Products" ("TenantId", "IsActive");
