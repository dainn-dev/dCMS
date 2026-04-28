-- DAI-664: Extend PromoCodes with display / spend / validity fields.

ALTER TABLE "PromoCodes"
    ADD COLUMN IF NOT EXISTS "PromoTypeLabel" TEXT         NULL,
    ADD COLUMN IF NOT EXISTS "MinSpend"       TEXT         NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS "StartDate"      TIMESTAMPTZ  NULL,
    ADD COLUMN IF NOT EXISTS "EndDate"        TIMESTAMPTZ  NULL;
