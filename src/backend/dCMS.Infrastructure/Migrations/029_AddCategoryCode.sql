-- Add explicit Code column (Internal Identifier) for categories.
-- Code preserves original casing/format from the bulk import (e.g. "FOOD-FROZEN-FOOD-FROZEN-SEAFOOD").
-- Slug remains the lowercase URL-safe derivative.

ALTER TABLE "Categories"
    ADD COLUMN IF NOT EXISTS "Code" VARCHAR(256) NOT NULL DEFAULT '';

-- Backfill from Slug uppercased so existing rows have a Code value.
UPDATE "Categories" SET "Code" = UPPER("Slug") WHERE "Code" = '';

CREATE UNIQUE INDEX IF NOT EXISTS "UX_Categories_Tenant_Code"
    ON "Categories" ("TenantId", "Code")
    WHERE "Code" <> '';

COMMENT ON COLUMN "Categories"."Code" IS 'Internal Identifier preserving original casing from import. Unique per tenant when non-empty.';
