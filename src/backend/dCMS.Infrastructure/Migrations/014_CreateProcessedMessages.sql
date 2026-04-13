-- US-F1 — Catalog DB: idempotent message deduplication for Catalog.Worker consumers (PostgreSQL).
CREATE TABLE IF NOT EXISTS "ProcessedMessages"
(
    "MessageId"   VARCHAR(128) NOT NULL PRIMARY KEY,
    "ProcessedAt" TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_ProcessedMessages_ProcessedAt" ON "ProcessedMessages" ("ProcessedAt");
