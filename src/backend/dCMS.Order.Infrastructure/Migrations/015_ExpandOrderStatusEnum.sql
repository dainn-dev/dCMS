-- DAI-695 — expand Orders.Status to include item-derived states
-- (ReadyForDelivery, PickedUp, Returned, PartialFulfilled).
-- Existing CK_Orders_Status_NotEmpty allows any non-empty string, so no
-- enum constraint to drop. We add a permissive whitelist constraint that
-- now includes the new states. Idempotent.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'CK_Orders_Status_Whitelist'
    ) THEN
        ALTER TABLE "Orders"
            ADD CONSTRAINT "CK_Orders_Status_Whitelist"
            CHECK ("Status" IN (
                'PaymentPending','Confirmed','Processing','Shipped','Delivered','Cancelled',
                'PaymentFailed','AuthFailed','AddressError','StockError','SystemError',
                'ReadyForDelivery','PickedUp','Returned','PartialFulfilled',
                -- Tolerate historical alternates already in production data.
                'AdminCancelled','UserCancelled'
            ));
    END IF;
END $$;
