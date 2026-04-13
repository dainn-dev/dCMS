-- MassTransit saga correlation storage (DAI-311). PostgreSQL-friendly; extend with MT state columns in a follow-up when saga is wired.
-- "RowVersion" is an optimistic concurrency token (incremented by application / saga persistence).
CREATE TABLE IF NOT EXISTS "OrderSagaState"
(
    "CorrelationId" UUID         NOT NULL PRIMARY KEY,
    "CurrentState"  VARCHAR(128) NULL,
    "RowVersion"    BIGINT       NOT NULL DEFAULT 1,
    "CreatedAt"     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"     TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_OrderSagaState_CurrentState" ON "OrderSagaState" ("CurrentState");
