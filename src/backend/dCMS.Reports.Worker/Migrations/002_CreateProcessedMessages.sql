-- Message idempotency store for the analytics projection consumer.
-- The shared MessageIdempotencyConsumeFilter (AddDcmsConsumerEndpointDefaults) requires an
-- IIdempotencyService backed by this table; without it every OrderPlacedV1 consume faulted to
-- the dead-letter queue and the analytics tables stayed empty.
CREATE TABLE IF NOT EXISTS "ProcessedMessages"
(
    "MessageId"   VARCHAR(128) NOT NULL PRIMARY KEY,
    "ProcessedAt" TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_ProcessedMessages_ProcessedAt" ON "ProcessedMessages" ("ProcessedAt");
