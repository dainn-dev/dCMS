-- Dead letter for Inventory outbox failures.
CREATE TABLE IF NOT EXISTS "DeadLetterEvents"
(
    "Id"               BIGSERIAL PRIMARY KEY,
    "SourceOutboxId"   BIGINT       NULL,
    "EventType"        VARCHAR(128) NOT NULL,
    "Payload"          TEXT         NOT NULL,
    "FailureReason"    TEXT         NOT NULL,
    "FailedAt"         TIMESTAMPTZ  NOT NULL,
    "ReprocessedAt"    TIMESTAMPTZ  NULL
);

CREATE INDEX IF NOT EXISTS "IX_DeadLetter_FailedAt" ON "DeadLetterEvents" ("FailedAt" DESC);
