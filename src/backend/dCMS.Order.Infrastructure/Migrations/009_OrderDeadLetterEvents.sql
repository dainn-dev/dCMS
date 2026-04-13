-- US-F4 / DAI-362 — outbox dead-letter persistence (matches SqlOutboxRelay insert + admin discard).
CREATE TABLE IF NOT EXISTS "DeadLetterEvents"
(
    "Id"               BIGSERIAL PRIMARY KEY,
    "SourceOutboxId"   BIGINT       NULL,
    "EventType"        VARCHAR(128) NOT NULL,
    "Payload"          TEXT         NOT NULL,
    "FailureReason"    TEXT         NOT NULL,
    "FailedAt"         TIMESTAMPTZ  NOT NULL,
    "ReprocessedAt"    TIMESTAMPTZ  NULL,
    "DiscardedAt"      TIMESTAMPTZ  NULL,
    "DiscardReason"    TEXT         NULL
);

CREATE INDEX IF NOT EXISTS "IX_OrderDeadLetter_FailedAt" ON "DeadLetterEvents" ("FailedAt" DESC);
