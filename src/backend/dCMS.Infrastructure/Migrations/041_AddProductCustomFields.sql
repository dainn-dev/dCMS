-- Per-product values for store-scoped custom fields (Product Configuration → Edit Product).
ALTER TABLE "Products"
    ADD COLUMN IF NOT EXISTS "CustomFields" JSONB NOT NULL DEFAULT '{}'::jsonb;

COMMENT ON COLUMN "Products"."CustomFields" IS 'Values keyed by field id from StoreProductFieldConfig: { "pfld-xxx": "value", "pfld-yyy": ["a","b"] }.';
