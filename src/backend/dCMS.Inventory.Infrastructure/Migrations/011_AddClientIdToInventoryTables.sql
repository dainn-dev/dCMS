-- DAI-749 / US-2: ClientId rollout on Inventory DB.
-- Only Warehouses carries TenantId today; VariantStock/StockMovements are scoped via WarehouseId FK.

ALTER TABLE "Warehouses" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
DROP INDEX IF EXISTS "IX_Warehouses_Tenant_Store";
CREATE INDEX IF NOT EXISTS "IX_Warehouses_Client_Tenant_Store" ON "Warehouses" ("ClientId", "TenantId", "StoreId");
