-- DAI-749 / US-2: ClientId rollout on Voucher DB.
ALTER TABLE "Vouchers" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
DROP INDEX IF EXISTS "UX_Vouchers_Tenant_Code";
CREATE UNIQUE INDEX IF NOT EXISTS "UX_Vouchers_Client_Tenant_Code"
    ON "Vouchers" ("ClientId", "TenantId", "Code");
DROP INDEX IF EXISTS "IX_Vouchers_Tenant_Status";
CREATE INDEX IF NOT EXISTS "IX_Vouchers_Client_Tenant_Status"
    ON "Vouchers" ("ClientId", "TenantId", "Status");

ALTER TABLE "VoucherHolds" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
CREATE INDEX IF NOT EXISTS "IX_VoucherHolds_Client_Tenant" ON "VoucherHolds" ("ClientId", "TenantId");

ALTER TABLE "VoucherLedger" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
CREATE INDEX IF NOT EXISTS "IX_VoucherLedger_Client_Tenant_Order"
    ON "VoucherLedger" ("ClientId", "TenantId", "OrderId") WHERE "OrderId" IS NOT NULL;
