-- DAI-749 / US-2: ClientId rollout on Payment DB.
-- Note: PaymentTransactions.TenantId is UUID here (legacy from migration 002),
-- not VARCHAR(64) like other services. ClientId stays VARCHAR(64) — it does not
-- correlate with TenantId's type, only namespaces it.
ALTER TABLE "PaymentTransactions" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
CREATE INDEX IF NOT EXISTS "IX_PaymentTransactions_Client_Tenant_Store"
    ON "PaymentTransactions" ("ClientId", "TenantId", "StoreId");
