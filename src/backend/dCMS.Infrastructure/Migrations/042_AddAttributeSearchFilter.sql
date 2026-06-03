-- DAI-597: Persist attribute advanced search-filter settings on CatalogAttributes.

ALTER TABLE "CatalogAttributes"
    ADD COLUMN IF NOT EXISTS "UseAsSearchFilter"       BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "SearchFilterCategoryIds" JSONB   NOT NULL DEFAULT '[]',
    ADD COLUMN IF NOT EXISTS "SearchFilterBrandCodes"  JSONB   NOT NULL DEFAULT '[]';

COMMENT ON COLUMN "CatalogAttributes"."UseAsSearchFilter" IS 'When true, attribute is exposed as a storefront listing search filter.';
COMMENT ON COLUMN "CatalogAttributes"."SearchFilterCategoryIds" IS 'JSON array of category Ids (integers) scoped for search filter.';
COMMENT ON COLUMN "CatalogAttributes"."SearchFilterBrandCodes" IS 'JSON array of brand codes scoped for search filter.';
