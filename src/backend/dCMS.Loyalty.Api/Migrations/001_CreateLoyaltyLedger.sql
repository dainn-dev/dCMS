-- DAI-723 (DAI-689 epic): Loyalty.Api persistence.
-- Balance is computed as SUM("Delta") over LoyaltyLedger filtered by (TenantId, CustomerId).
-- Holds are NOT ledger entries — they reduce the available-to-spend balance until capture
-- (which writes a NEGATIVE ledger row) or release/expiry (which deletes the hold).

CREATE TABLE IF NOT EXISTS "LoyaltyLedger"
(
    "Id"         BIGSERIAL     PRIMARY KEY,
    "TenantId"   VARCHAR(64)   NOT NULL,
    "CustomerId" VARCHAR(64)   NOT NULL,
    "Delta"      NUMERIC(18,4) NOT NULL,            -- positive = earn, negative = spend/refund-of-earn
    "Reason"     VARCHAR(40)   NOT NULL,            -- 'EARN','SPEND','REFUND','ADJUST'
    "OrderId"    UUID          NULL,
    "HoldId"     UUID          NULL,
    "Notes"      TEXT          NULL,
    "OccurredAt" TIMESTAMPTZ   NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS "IX_LoyaltyLedger_Tenant_Customer" ON "LoyaltyLedger" ("TenantId", "CustomerId", "OccurredAt" DESC);
CREATE INDEX IF NOT EXISTS "IX_LoyaltyLedger_Tenant_Order" ON "LoyaltyLedger" ("TenantId", "OrderId") WHERE "OrderId" IS NOT NULL;

CREATE TABLE IF NOT EXISTS "LoyaltyHolds"
(
    "Id"         UUID          NOT NULL PRIMARY KEY,
    "TenantId"   VARCHAR(64)   NOT NULL,
    "CustomerId" VARCHAR(64)   NOT NULL,
    "OrderId"    UUID          NOT NULL,
    "Amount"     NUMERIC(18,4) NOT NULL CHECK ("Amount" > 0),
    "Status"     VARCHAR(20)   NOT NULL DEFAULT 'Held' CHECK ("Status" IN ('Held','Captured','Released','Refunded')),
    "ExpiresAt"  TIMESTAMPTZ   NOT NULL,
    "CreatedAt"  TIMESTAMPTZ   NOT NULL DEFAULT now(),
    "UpdatedAt"  TIMESTAMPTZ   NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS "UX_LoyaltyHolds_Tenant_Order_Customer_Active"
    ON "LoyaltyHolds" ("TenantId", "OrderId", "CustomerId")
    WHERE "Status" = 'Held';
CREATE INDEX IF NOT EXISTS "IX_LoyaltyHolds_Customer_Status" ON "LoyaltyHolds" ("TenantId", "CustomerId", "Status");
CREATE INDEX IF NOT EXISTS "IX_LoyaltyHolds_Expires" ON "LoyaltyHolds" ("ExpiresAt") WHERE "Status" = 'Held';
