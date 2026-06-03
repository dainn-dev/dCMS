-- US: Brand-scoped catalog. Adds an optional brand reference to products so the
-- backoffice product list can filter by Brand and the search index can store brandId.
ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "BrandId" VARCHAR(64) NULL;

CREATE INDEX IF NOT EXISTS "IX_Products_Tenant_Store_Brand" ON "Products" ("TenantId", "StoreId", "BrandId");
