-- DAI-640 — Failure audit context columns on Orders (nullable; no separate table).
ALTER TABLE "Orders"
  ADD COLUMN IF NOT EXISTS "FailureReason"    TEXT        NULL,
  ADD COLUMN IF NOT EXISTS "FailureErrorCode" VARCHAR(64) NULL,
  ADD COLUMN IF NOT EXISTS "FailedAt"         TIMESTAMPTZ NULL,
  ADD COLUMN IF NOT EXISTS "RetryCount"       INT         NOT NULL DEFAULT 0;

CREATE INDEX IF NOT EXISTS "IX_Orders_Tenant_Store_FailedAt"
  ON "Orders" ("TenantId", "StoreId", "FailedAt" DESC)
  WHERE "FailedAt" IS NOT NULL;

