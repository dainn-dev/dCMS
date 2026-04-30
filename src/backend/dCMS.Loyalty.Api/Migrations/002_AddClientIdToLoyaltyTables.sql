-- DAI-749 / US-2: ClientId rollout on Loyalty DB.
ALTER TABLE "LoyaltyLedger" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
DROP INDEX IF EXISTS "IX_LoyaltyLedger_Tenant_Customer";
CREATE INDEX IF NOT EXISTS "IX_LoyaltyLedger_Client_Tenant_Customer"
    ON "LoyaltyLedger" ("ClientId", "TenantId", "CustomerId", "OccurredAt" DESC);

ALTER TABLE "LoyaltyHolds" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
DROP INDEX IF EXISTS "IX_LoyaltyHolds_Customer_Status";
CREATE INDEX IF NOT EXISTS "IX_LoyaltyHolds_Client_Tenant_Customer_Status"
    ON "LoyaltyHolds" ("ClientId", "TenantId", "CustomerId", "Status");
