-- DAI-593: Extend CatalogAttributes + CatalogAttributeValues with management fields.
-- Existing: CatalogAttributes(Id, TenantId, Name, SortOrder)
--           CatalogAttributeValues(Id, AttributeId, Name, SortOrder)

ALTER TABLE "CatalogAttributes"
    ADD COLUMN IF NOT EXISTS "Code"        VARCHAR(100) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "Type"        VARCHAR(20)  NOT NULL DEFAULT 'TEXT',
    ADD COLUMN IF NOT EXISTS "Required"    BOOLEAN      NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "Description" TEXT         NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "CreatedAt"   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    ADD COLUMN IF NOT EXISTS "UpdatedAt"   TIMESTAMPTZ  NOT NULL DEFAULT NOW();

-- Unique code per tenant (partial: only non-empty codes)
CREATE UNIQUE INDEX IF NOT EXISTS "UX_CatalogAttributes_Tenant_Code"
    ON "CatalogAttributes" ("TenantId", "Code")
    WHERE "Code" <> '';

ALTER TABLE "CatalogAttributeValues"
    ADD COLUMN IF NOT EXISTS "Code"      VARCHAR(100) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "ColorHex"  VARCHAR(9)   NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "ImageUrl"  TEXT         NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "CreatedAt" TIMESTAMPTZ  NOT NULL DEFAULT NOW();

COMMENT ON COLUMN "CatalogAttributes"."Code"        IS 'Snake_case machine identifier, e.g. color_primary. Unique per tenant.';
COMMENT ON COLUMN "CatalogAttributes"."Type"        IS 'One of: TEXT, COLOR, IMAGE, SELECT, BOOLEAN.';
COMMENT ON COLUMN "CatalogAttributes"."Required"    IS 'When true, product must supply this attribute.';
COMMENT ON COLUMN "CatalogAttributeValues"."ColorHex" IS 'CSS hex color, e.g. #1a1a1a. Only for Type=COLOR.';
COMMENT ON COLUMN "CatalogAttributeValues"."ImageUrl" IS 'URL or path to swatch image. Only for Type=IMAGE.';
