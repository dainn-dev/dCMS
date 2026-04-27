-- DAI-694 — per-item fulfillment status to support partial shipment / cancellation /
-- pickup / returns. Default 'Open' for existing rows. Idempotent.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'OrderItems' AND column_name = 'FulfillmentStatus'
    ) THEN
        ALTER TABLE "OrderItems"
            ADD COLUMN "FulfillmentStatus" VARCHAR(32) NOT NULL DEFAULT 'Open';

        ALTER TABLE "OrderItems"
            ADD CONSTRAINT "CK_OrderItems_FulfillmentStatus"
            CHECK ("FulfillmentStatus" IN
                ('Open','Allocated','ReadyForDelivery','Shipped','Delivered','PickedUp','Returned','Cancelled'));

        CREATE INDEX IF NOT EXISTS "IX_OrderItems_FulfillmentStatus"
            ON "OrderItems" ("FulfillmentStatus");
    END IF;
END $$;
