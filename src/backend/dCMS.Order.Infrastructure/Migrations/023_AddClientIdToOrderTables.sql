-- DAI-749 / US-2: ClientId rollout on Order DB tenant-scoped tables.
-- DEFAULT 'aeon' back-fills the only chain that exists today; new deployments
-- override Dcms:Client.Id and seed their own Clients row.

-- Orders (001) — most read-hot table; the report query in OrderReportQueryStore
-- will filter on ClientId+TenantId+StoreId+CreatedAt once US-3 lands wiring.
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
DROP INDEX IF EXISTS "IX_Orders_Tenant_Store_Customer";
CREATE INDEX IF NOT EXISTS "IX_Orders_Client_Tenant_Store_Customer" ON "Orders" ("ClientId", "TenantId", "StoreId", "CustomerId");

-- OrderSagaState (005)
ALTER TABLE "OrderSagaState" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
CREATE INDEX IF NOT EXISTS "IX_OrderSagaState_Client_Tenant" ON "OrderSagaState" ("ClientId", "TenantId");

-- OrderFailures (010)
ALTER TABLE "OrderFailures" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
CREATE INDEX IF NOT EXISTS "IX_OrderFailures_Client_Tenant" ON "OrderFailures" ("ClientId", "TenantId");

-- Returns (017)
ALTER TABLE "Returns" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
CREATE INDEX IF NOT EXISTS "IX_Returns_Client_Tenant" ON "Returns" ("ClientId", "TenantId");
