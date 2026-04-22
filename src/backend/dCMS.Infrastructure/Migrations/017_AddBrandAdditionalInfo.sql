-- §Brand AdditionalInfo: persist dynamic additional fields per brand (DAI-573 follow-up).
-- Stored as JSONB — schema-less key/value pairs driven by BrandConfigPage field definitions.
ALTER TABLE "Brands"
    ADD COLUMN IF NOT EXISTS "AdditionalInfo" JSONB NOT NULL DEFAULT '{}';

COMMENT ON COLUMN "Brands"."AdditionalInfo" IS
    'Dynamic additional fields configured in BrandConfigPage. '
    'Keys = field.id from BrandAdditionalField, values = user-entered data. '
    'Schema-less; validated at SPA layer.';
