-- DAI-724: idempotency log for multi-tender payment orchestration.
-- Keyed by (OrderId, ComponentId, Action) so retries of ProcessPaymentV1
-- replay safely without double-reserving / double-capturing downstream tenders.
CREATE TABLE IF NOT EXISTS "PaymentComponentDispatchLog" (
    "Id"           BIGSERIAL    PRIMARY KEY,
    "OrderId"      UUID         NOT NULL,
    "ComponentId"  UUID         NOT NULL,
    "Action"       VARCHAR(20)  NOT NULL,
    "ExternalRef"  TEXT         NULL,
    "Status"       VARCHAR(20)  NOT NULL,
    "ErrorCode"    VARCHAR(64)  NULL,
    "ErrorMessage" TEXT         NULL,
    "OccurredAt"   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT "CK_PaymentComponentDispatchLog_Action"
        CHECK ("Action" IN ('RESERVE','CAPTURE','RELEASE','REFUND')),
    CONSTRAINT "CK_PaymentComponentDispatchLog_Status"
        CHECK ("Status" IN ('Success','Failed')),
    CONSTRAINT "UQ_PaymentComponentDispatchLog_Idem"
        UNIQUE ("OrderId","ComponentId","Action")
);

CREATE INDEX IF NOT EXISTS "IX_PaymentComponentDispatchLog_Order"
    ON "PaymentComponentDispatchLog" ("OrderId");
