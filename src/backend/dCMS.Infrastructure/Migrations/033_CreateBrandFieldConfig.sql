-- Tenant-scoped configuration of the dynamic "additional fields" shown on the
-- Add / Edit Brand form (eStore → Brand Configuration page).
--
-- Previously this config lived only in the browser's localStorage
-- (dcms.estore.brandAdditionalFields.v1), so it was per-device and not
-- tenant-isolated. This table makes it a first-class, tenant-scoped server
-- record shared across all admins of a Siêu thị (Tenant).
--
-- The field definitions are stored as a single JSONB document ("Fields") rather
-- than one row per field: the UI always reads and writes the whole ordered list,
-- so a document keeps reads/writes atomic and preserves ordering trivially.
CREATE TABLE IF NOT EXISTS "BrandFieldConfig"
(
    "TenantId"  VARCHAR(64) NOT NULL,
    "Fields"    JSONB       NOT NULL DEFAULT '[]'::jsonb,
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT "PK_BrandFieldConfig" PRIMARY KEY ("TenantId")
);

COMMENT ON TABLE  "BrandFieldConfig"            IS 'Tenant-scoped dynamic field definitions for the Add/Edit Brand form. One row per Siêu thị (Tenant).';
COMMENT ON COLUMN "BrandFieldConfig"."TenantId" IS 'Siêu thị (Tenant) identifier — multi-tenant isolation key.';
COMMENT ON COLUMN "BrandFieldConfig"."Fields"   IS 'Ordered JSON array of field definitions: [{id,enabled,required,property,columnLabel,fieldName,controlType,section,options:[{name,value}]}].';
