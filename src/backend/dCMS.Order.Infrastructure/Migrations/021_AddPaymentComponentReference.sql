-- DAI-689: split PaymentComponents.ExternalRef (output, holdId/chargeRef)
-- from a new "Reference" column (input, voucher code / customer id / etc).
ALTER TABLE "PaymentComponents"
    ADD COLUMN IF NOT EXISTS "Reference" VARCHAR(128) NULL;

-- Backfill: rows still in Pending state with a non-null ExternalRef are
-- pre-authorize voucher codes (see PaymentOrchestrator legacy comment).
-- Move them to the new Reference column and clear ExternalRef.
UPDATE "PaymentComponents"
   SET "Reference" = "ExternalRef",
       "ExternalRef" = NULL
 WHERE "State" = 'Pending'
   AND "ExternalRef" IS NOT NULL;
