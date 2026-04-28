-- DAI-722 / DAI-689: Multi-tender payment model (Order DB).
CREATE TABLE IF NOT EXISTS "OrderPayments"
(
    "Id"      UUID          NOT NULL PRIMARY KEY DEFAULT gen_random_uuid(),
    "OrderId" UUID          NOT NULL REFERENCES "Orders" ("Id") ON DELETE CASCADE,
    "Total"   NUMERIC(18,4) NOT NULL DEFAULT 0,
    "Status"  VARCHAR(32)   NOT NULL,
    CONSTRAINT "UQ_OrderPayments_OrderId" UNIQUE ("OrderId")
);

CREATE TABLE IF NOT EXISTS "PaymentComponents"
(
    "Id"             UUID          NOT NULL PRIMARY KEY DEFAULT gen_random_uuid(),
    "OrderPaymentId" UUID          NOT NULL REFERENCES "OrderPayments" ("Id") ON DELETE CASCADE,
    "Type"           VARCHAR(32)   NOT NULL, -- Gateway|Voucher|LoyaltyPoints|GiftCard
    "Amount"         NUMERIC(18,4) NOT NULL DEFAULT 0,
    "ExternalRef"    TEXT          NULL,
    "State"          VARCHAR(32)   NOT NULL, -- Pending|Authorized|Captured|Failed|Refunded|Cancelled
    "LastError"      TEXT          NULL,
    "Ordering"       INT           NOT NULL DEFAULT 0,
    "CreatedAt"      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "UpdatedAt"      TIMESTAMPTZ   NULL,
    CONSTRAINT "CK_PaymentComponents_Amount_NonNegative" CHECK ("Amount" >= 0)
);

CREATE INDEX IF NOT EXISTS "IX_PaymentComponents_OrderPaymentId"
    ON "PaymentComponents" ("OrderPaymentId");

CREATE INDEX IF NOT EXISTS "IX_PaymentComponents_OrderPaymentId_Ordering"
    ON "PaymentComponents" ("OrderPaymentId", "Ordering");

-- DAI-722 AC2: lightweight sum invariant (enforced on status transitions in code; DB constraint would need trigger).

