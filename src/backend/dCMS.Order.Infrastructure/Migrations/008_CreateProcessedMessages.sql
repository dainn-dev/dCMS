-- US-F1 / DAI-344 — idempotent message deduplication (PostgreSQL).
CREATE TABLE IF NOT EXISTS "ProcessedMessages"
(
    "MessageId"   VARCHAR(128) NOT NULL PRIMARY KEY,
    "ProcessedAt" TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_ProcessedMessages_ProcessedAt" ON "ProcessedMessages" ("ProcessedAt");
