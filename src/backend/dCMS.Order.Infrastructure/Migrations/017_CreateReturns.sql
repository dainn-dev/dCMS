-- DAI-697 — RMA / returns workflow.
-- Returns lifecycle: Pending → Approved → Completed (or → Rejected from Pending).
-- ReturnedQuantity is tracked per OrderItem so partial returns work; FulfillmentStatus
-- only flips to 'Returned' when ReturnedQuantity = Quantity.
ALTER TABLE "OrderItems"
    ADD COLUMN IF NOT EXISTS "ReturnedQuantity" INTEGER NOT NULL DEFAULT 0;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'CK_OrderItems_ReturnedQuantity_Bounds'
    ) THEN
        ALTER TABLE "OrderItems"
            ADD CONSTRAINT "CK_OrderItems_ReturnedQuantity_Bounds"
            CHECK ("ReturnedQuantity" >= 0 AND "ReturnedQuantity" <= "Quantity");
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS "Returns"
(
    "Id"            UUID         NOT NULL PRIMARY KEY,
    "OrderId"       UUID         NOT NULL REFERENCES "Orders" ("Id") ON DELETE CASCADE,
    "TenantId"      VARCHAR(64)  NOT NULL,
    "StoreId"       VARCHAR(64)  NOT NULL,
    "Status"        VARCHAR(32)  NOT NULL,
    "Reason"        VARCHAR(64)  NOT NULL,
    "Notes"         TEXT         NULL,
    "RefundCaseId"  UUID         NULL,
    "CreatedAt"     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "ApprovedAt"    TIMESTAMPTZ  NULL,
    "ApprovedBy"    VARCHAR(64)  NULL,
    "CompletedAt"   TIMESTAMPTZ  NULL,
    CONSTRAINT "CK_Returns_Status" CHECK ("Status" IN ('Pending','Approved','Rejected','Completed')),
    CONSTRAINT "CK_Returns_Reason" CHECK ("Reason" IN
        ('WrongItem','Defective','NotAsDescribed','ChangedMind','DamagedInTransit','Other'))
);

CREATE INDEX IF NOT EXISTS "IX_Returns_OrderId" ON "Returns" ("OrderId");
CREATE INDEX IF NOT EXISTS "IX_Returns_Tenant_Store_Status"
    ON "Returns" ("TenantId", "StoreId", "Status");

CREATE TABLE IF NOT EXISTS "ReturnItems"
(
    "Id"           UUID        NOT NULL PRIMARY KEY,
    "ReturnId"     UUID        NOT NULL REFERENCES "Returns" ("Id") ON DELETE CASCADE,
    "OrderItemId"  VARCHAR(64) NOT NULL,
    "Quantity"     INTEGER     NOT NULL,
    "Reason"       VARCHAR(64) NULL,
    CONSTRAINT "CK_ReturnItems_Quantity_Positive" CHECK ("Quantity" > 0)
);

CREATE INDEX IF NOT EXISTS "IX_ReturnItems_ReturnId" ON "ReturnItems" ("ReturnId");
CREATE INDEX IF NOT EXISTS "IX_ReturnItems_OrderItemId" ON "ReturnItems" ("OrderItemId");
