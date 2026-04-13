-- Shipping snapshot on orders + transactional outbox (DAI-313).
ALTER TABLE "Orders"
    ADD COLUMN IF NOT EXISTS "ShippingAddress" JSONB NOT NULL DEFAULT '{}'::jsonb;

CREATE TABLE IF NOT EXISTS "OutboxEvents"
(
    "Id"          BIGSERIAL PRIMARY KEY,
    "EventType"   VARCHAR(128) NOT NULL,
    "Payload"     TEXT         NOT NULL,
    "CreatedAt"   TIMESTAMPTZ  NOT NULL,
    "ProcessedAt" TIMESTAMPTZ  NULL,
    "RetryCount"  INTEGER      NOT NULL DEFAULT 0,
    "Error"       TEXT         NULL
);

CREATE INDEX IF NOT EXISTS "IX_Outbox_Unprocessed" ON "OutboxEvents" ("ProcessedAt", "CreatedAt")
    WHERE "ProcessedAt" IS NULL;
