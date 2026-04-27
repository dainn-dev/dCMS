CREATE TABLE IF NOT EXISTS "AuditOutbox"
(
    "Id"          BIGSERIAL    PRIMARY KEY,
    "Payload"     TEXT         NOT NULL,
    "CreatedAt"   TIMESTAMPTZ  NOT NULL,
    "ProcessedAt" TIMESTAMPTZ  NULL,
    "RetryCount"  INTEGER      NOT NULL DEFAULT 0,
    "Error"       TEXT         NULL
);

CREATE INDEX IF NOT EXISTS "IX_AuditOutbox_Pending"
    ON "AuditOutbox" ("Id")
    WHERE "ProcessedAt" IS NULL;
