-- US-23 / DAI-338 — PaymentTransactions read model (PostgreSQL).
CREATE TABLE IF NOT EXISTS "PaymentTransactions"
(
    "Id"              UUID         NOT NULL PRIMARY KEY DEFAULT gen_random_uuid(),
    "OrderId"          UUID         NOT NULL,
    "PaymentIntentId"  VARCHAR(128) NOT NULL DEFAULT '',
    "Amount"           NUMERIC(18, 4) NOT NULL DEFAULT 0,
    "Currency"         VARCHAR(8)   NOT NULL DEFAULT 'USD',
    "Status"           VARCHAR(32)  NOT NULL DEFAULT 'Pending',
    "Provider"         VARCHAR(64)  NOT NULL DEFAULT '',
    "CreatedAt"        TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_PaymentTransactions_OrderId" ON "PaymentTransactions" ("OrderId");
CREATE UNIQUE INDEX IF NOT EXISTS "UQ_PaymentTransactions_PaymentIntentId" ON "PaymentTransactions" ("PaymentIntentId");
