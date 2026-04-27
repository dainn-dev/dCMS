-- DAI-717: delivery trace for outbound email notifications.
CREATE TABLE IF NOT EXISTS "EmailDeliveries"
(
    "Id"             UUID         NOT NULL PRIMARY KEY,
    "TenantId"       VARCHAR(64)  NOT NULL,
    "StoreId"        VARCHAR(64)  NULL,
    "IdempotencyKey" TEXT         NOT NULL,
    "TemplateKey"    TEXT         NOT NULL,
    "Locale"         TEXT         NOT NULL,
    "ToAddress"      TEXT         NOT NULL,
    "Status"         TEXT         NOT NULL, -- queued|sent|failed
    "Error"          TEXT         NULL,
    "CreatedAt"      TIMESTAMPTZ  NOT NULL,
    "SentAt"         TIMESTAMPTZ  NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_EmailDeliveries_Idempotency"
    ON "EmailDeliveries" ("TenantId", "IdempotencyKey");

CREATE INDEX IF NOT EXISTS "IX_EmailDeliveries_TenantCreated"
    ON "EmailDeliveries" ("TenantId", "CreatedAt" DESC);

