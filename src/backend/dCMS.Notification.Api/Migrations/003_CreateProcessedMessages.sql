-- Phase C: ProcessedMessages idempotency table moved into dcms_notification so Notification.Worker
-- no longer needs ConnectionStrings:Catalog. Catalog.Worker still maintains its own copy in dcms_catalog.
CREATE TABLE IF NOT EXISTS "ProcessedMessages"
(
    "MessageId"   VARCHAR(128) NOT NULL PRIMARY KEY,
    "ProcessedAt" TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_ProcessedMessages_ProcessedAt" ON "ProcessedMessages" ("ProcessedAt");
