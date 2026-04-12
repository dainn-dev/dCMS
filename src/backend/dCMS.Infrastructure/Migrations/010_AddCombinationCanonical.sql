-- US-10: storefront variant matrix keys (canonical attrId=valueId|... before hashing).
ALTER TABLE "ProductVariants"
    ADD COLUMN IF NOT EXISTS "CombinationCanonical" VARCHAR(256) NOT NULL DEFAULT '';
