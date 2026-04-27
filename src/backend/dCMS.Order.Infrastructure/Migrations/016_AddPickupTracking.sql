-- DAI-696 — pickup tracking on OrderItems.
-- PickupPinHash: PBKDF2(SHA256) of customer PIN issued when item moves to ReadyForDelivery.
-- PickedUpAt / PickedUpBy: audit columns set when pickup is confirmed.
ALTER TABLE "OrderItems"
    ADD COLUMN IF NOT EXISTS "PickupPinHash" TEXT NULL;

ALTER TABLE "OrderItems"
    ADD COLUMN IF NOT EXISTS "PickedUpAt" TIMESTAMPTZ NULL;

ALTER TABLE "OrderItems"
    ADD COLUMN IF NOT EXISTS "PickedUpBy" VARCHAR(64) NULL;
