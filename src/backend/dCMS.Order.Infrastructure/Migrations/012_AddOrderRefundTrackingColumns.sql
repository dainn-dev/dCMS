-- Refund case tracking on Orders (DAI-652 / DAI-651).
-- API: GET/PATCH /api/refund-cases — columns match RefundCasesApiIntegrationTests fixture.
ALTER TABLE "Orders"
    ADD COLUMN IF NOT EXISTS "RefundStatus" VARCHAR(32) NULL;

ALTER TABLE "Orders"
    ADD COLUMN IF NOT EXISTS "RefundRemark" VARCHAR(512) NOT NULL DEFAULT '';

ALTER TABLE "Orders"
    ADD COLUMN IF NOT EXISTS "RefundedAt" TIMESTAMPTZ NULL;
