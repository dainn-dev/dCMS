-- §Categories extended: add management fields (DAI-587 / DAI-586).
-- Existing columns: Id, TenantId, ParentId, Path, Depth, Name, Slug, SortOrder.

ALTER TABLE "Categories"
    ADD COLUMN IF NOT EXISTS "Active"            BOOLEAN      NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS "PublishFrom"        TIMESTAMPTZ  NULL,
    ADD COLUMN IF NOT EXISTS "PublishUntil"       TIMESTAMPTZ  NULL,
    ADD COLUMN IF NOT EXISTS "ImageMenuUrl"       TEXT         NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "ImagePageUrl"       TEXT         NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "ImageThumbUrl"      TEXT         NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "ShowInNav"          BOOLEAN      NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS "ShowInBrands"       BOOLEAN      NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "CustomNavUrl"       TEXT         NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "NavSortPriority"    INTEGER      NOT NULL DEFAULT 10,
    ADD COLUMN IF NOT EXISTS "BreakNavColumn"     BOOLEAN      NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "DefaultSort"        VARCHAR(30)  NOT NULL DEFAULT 'bestseller',
    ADD COLUMN IF NOT EXISTS "NoRecommendations"  BOOLEAN      NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "MetaTitleJson"      TEXT         NOT NULL DEFAULT '{}',
    ADD COLUMN IF NOT EXISTS "MetaKeywordsJson"   TEXT         NOT NULL DEFAULT '{}',
    ADD COLUMN IF NOT EXISTS "MetaDescJson"       TEXT         NOT NULL DEFAULT '{}',
    ADD COLUMN IF NOT EXISTS "RestrictAccess"     BOOLEAN      NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "AccessApp"          VARCHAR(30)  NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "AccessMemberType"   VARCHAR(30)  NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "AccessMemberTier"   VARCHAR(30)  NOT NULL DEFAULT '';

-- Unique slug within tenant
CREATE UNIQUE INDEX IF NOT EXISTS "UX_Categories_Tenant_Slug"
    ON "Categories" ("TenantId", "Slug");

-- Fast lookup by parent for tree traversal
CREATE INDEX IF NOT EXISTS "IX_Categories_Parent"
    ON "Categories" ("TenantId", "ParentId");

COMMENT ON COLUMN "Categories"."Active"           IS 'When false the category is hidden from all storefronts.';
COMMENT ON COLUMN "Categories"."PublishFrom"       IS 'UTC timestamp from which the category is visible. NULL = no lower bound.';
COMMENT ON COLUMN "Categories"."PublishUntil"      IS 'UTC timestamp after which the category expires. NULL = no expiry.';
COMMENT ON COLUMN "Categories"."MetaTitleJson"     IS 'Multi-language JSON object {en:"",zh:""}. Empty object = inherit.';
COMMENT ON COLUMN "Categories"."MetaKeywordsJson"  IS 'Multi-language JSON object.';
COMMENT ON COLUMN "Categories"."MetaDescJson"      IS 'Multi-language JSON object.';
COMMENT ON COLUMN "Categories"."RestrictAccess"    IS 'When true, visibility restricted to AccessApp / AccessMemberType / AccessMemberTier.';
