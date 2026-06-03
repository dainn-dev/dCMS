-- Store-scoped configuration of dynamic custom fields on Add/Edit Product (eStore → Product Configuration).
-- Previously persisted only in browser localStorage (dcms.estore.productConfigFields.v1).
CREATE TABLE IF NOT EXISTS "StoreProductFieldConfig"
(
    "TenantId"  VARCHAR(64) NOT NULL,
    "StoreId"   VARCHAR(64) NOT NULL,
    "Fields"    JSONB       NOT NULL DEFAULT '[]'::jsonb,
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT "PK_StoreProductFieldConfig" PRIMARY KEY ("TenantId", "StoreId")
);

COMMENT ON TABLE  "StoreProductFieldConfig"  IS 'Per-store dynamic field definitions for Add/Edit Product tabs.';
COMMENT ON COLUMN "StoreProductFieldConfig"."Fields" IS 'Ordered JSON array: [{id,enabled,required,property,columnLabel,fieldName,controlType,targetPage,options:[{name,value}]}].';
