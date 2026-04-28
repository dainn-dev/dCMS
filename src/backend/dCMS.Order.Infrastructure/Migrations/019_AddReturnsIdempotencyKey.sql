-- DAI-729 — idempotency for POST /api/orders/{orderId}/returns.
-- Allow clients to safely retry return creation without creating duplicates.

ALTER TABLE "Returns"
    ADD COLUMN IF NOT EXISTS "IdempotencyKey" UUID NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "UX_Returns_Tenant_Store_Order_IdempotencyKey"
    ON "Returns" ("TenantId", "StoreId", "OrderId", "IdempotencyKey")
    WHERE "IdempotencyKey" IS NOT NULL;

