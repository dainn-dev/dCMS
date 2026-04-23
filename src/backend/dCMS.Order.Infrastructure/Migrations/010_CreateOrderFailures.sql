-- DAI-631 — OrderFailures persistence for operational visibility + retries.
CREATE TABLE IF NOT EXISTS "OrderFailures"
(
    "OrderId"           UUID        NOT NULL,
    "TenantId"          VARCHAR(64) NOT NULL,
    "StoreId"           VARCHAR(64) NOT NULL,
    "FailureStatus"     VARCHAR(32) NOT NULL, -- PaymentFailed|AddressError|AuthFailed|StockError|SystemError
    "FailureReason"     TEXT        NOT NULL,
    "FailureErrorCode"  VARCHAR(64) NULL,
    "SourceEventId"     VARCHAR(128) NULL,
    "FailedAt"          TIMESTAMPTZ NOT NULL,
    "RetryCount"        INT         NOT NULL DEFAULT 0,
    "LastRetryAt"       TIMESTAMPTZ NULL,
    "ResolvedAt"        TIMESTAMPTZ NULL,
    "ResolvedBy"        VARCHAR(128) NULL,
    "LogJson"           JSONB       NOT NULL DEFAULT '[]',
    PRIMARY KEY ("OrderId")
);

CREATE INDEX IF NOT EXISTS "IX_OrderFailures_Tenant_Store_Status_FailedAt"
    ON "OrderFailures" ("TenantId", "StoreId", "FailureStatus", "FailedAt" DESC)
    WHERE "ResolvedAt" IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "UX_OrderFailures_SourceEventId"
    ON "OrderFailures" ("SourceEventId")
    WHERE "SourceEventId" IS NOT NULL;

