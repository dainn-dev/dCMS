-- DAI-723 (DAI-689 epic): Voucher.Api persistence.
-- Vouchers are balance-bound tender instruments (RemainingValue can be partially consumed).
-- Holds reserve a portion of RemainingValue against an order for a TTL window (default 15 min).
-- Ledger captures every state change for audit (RESERVE / CAPTURE / RELEASE / REFUND).

CREATE TABLE IF NOT EXISTS "Vouchers"
(
    "Id"             UUID         NOT NULL PRIMARY KEY,
    "TenantId"       VARCHAR(64)  NOT NULL,
    "Code"           VARCHAR(64)  NOT NULL,
    "FaceValue"      NUMERIC(18,4) NOT NULL CHECK ("FaceValue" >= 0),
    "RemainingValue" NUMERIC(18,4) NOT NULL CHECK ("RemainingValue" >= 0),
    "Currency"       CHAR(3)      NOT NULL DEFAULT 'VND',
    "Status"         VARCHAR(20)  NOT NULL DEFAULT 'Active' CHECK ("Status" IN ('Active','Disabled','Expired','Consumed')),
    "ExpiresAt"      TIMESTAMPTZ  NULL,
    "CreatedAt"      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    "UpdatedAt"      TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS "UX_Vouchers_Tenant_Code" ON "Vouchers" ("TenantId", "Code");
CREATE INDEX IF NOT EXISTS "IX_Vouchers_Tenant_Status" ON "Vouchers" ("TenantId", "Status");

CREATE TABLE IF NOT EXISTS "VoucherHolds"
(
    "Id"          UUID          NOT NULL PRIMARY KEY,
    "TenantId"    VARCHAR(64)   NOT NULL,
    "VoucherId"   UUID          NOT NULL REFERENCES "Vouchers"("Id") ON DELETE CASCADE,
    "OrderId"     UUID          NOT NULL,
    "Amount"      NUMERIC(18,4) NOT NULL CHECK ("Amount" > 0),
    "Status"      VARCHAR(20)   NOT NULL DEFAULT 'Held' CHECK ("Status" IN ('Held','Captured','Released','Refunded')),
    "ExpiresAt"   TIMESTAMPTZ   NOT NULL,
    "CreatedAt"   TIMESTAMPTZ   NOT NULL DEFAULT now(),
    "UpdatedAt"   TIMESTAMPTZ   NOT NULL DEFAULT now()
);
-- One active hold per (tenant, order, voucher) — keeps reserve idempotent on retry.
CREATE UNIQUE INDEX IF NOT EXISTS "UX_VoucherHolds_Tenant_Order_Voucher_Active"
    ON "VoucherHolds" ("TenantId", "OrderId", "VoucherId")
    WHERE "Status" = 'Held';
CREATE INDEX IF NOT EXISTS "IX_VoucherHolds_Voucher_Status" ON "VoucherHolds" ("VoucherId", "Status");
CREATE INDEX IF NOT EXISTS "IX_VoucherHolds_Expires" ON "VoucherHolds" ("ExpiresAt") WHERE "Status" = 'Held';

CREATE TABLE IF NOT EXISTS "VoucherLedger"
(
    "Id"         BIGSERIAL    PRIMARY KEY,
    "TenantId"   VARCHAR(64)  NOT NULL,
    "VoucherId"  UUID         NOT NULL REFERENCES "Vouchers"("Id") ON DELETE CASCADE,
    "HoldId"     UUID         NULL REFERENCES "VoucherHolds"("Id") ON DELETE SET NULL,
    "OrderId"    UUID         NULL,
    "Action"     VARCHAR(20)  NOT NULL CHECK ("Action" IN ('RESERVE','CAPTURE','RELEASE','REFUND','ADJUST')),
    "Delta"      NUMERIC(18,4) NOT NULL,    -- negative = consumed, positive = restored
    "Reason"     TEXT         NULL,
    "OccurredAt" TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS "IX_VoucherLedger_Voucher_Time" ON "VoucherLedger" ("VoucherId", "OccurredAt" DESC);
CREATE INDEX IF NOT EXISTS "IX_VoucherLedger_Tenant_Order" ON "VoucherLedger" ("TenantId", "OrderId") WHERE "OrderId" IS NOT NULL;
