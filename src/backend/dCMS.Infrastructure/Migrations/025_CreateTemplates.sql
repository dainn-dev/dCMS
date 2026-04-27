-- DAI-687 / DAI-715: notification/email/print templates (tenant override + locale variants).
CREATE TABLE IF NOT EXISTS "Templates"
(
    "Id"           UUID         NOT NULL PRIMARY KEY,
    "TenantId"     VARCHAR(64)  NULL, -- NULL = global default
    "Key"          TEXT         NOT NULL,
    "Locale"       TEXT         NOT NULL DEFAULT 'en-US',
    "Channel"      TEXT         NOT NULL,
    "Subject"      TEXT         NULL,
    "Body"         TEXT         NOT NULL,
    "ModelVersion" INTEGER      NOT NULL DEFAULT 1,
    "UpdatedAt"    TIMESTAMPTZ  NOT NULL DEFAULT now(),
    "UpdatedBy"    VARCHAR(64)  NULL
);

-- Ensure channel is within known set.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'CK_Templates_Channel'
    ) THEN
        ALTER TABLE "Templates"
            ADD CONSTRAINT "CK_Templates_Channel"
            CHECK ("Channel" IN ('email','sms','print','admin'));
    END IF;
END $$;

-- Unique per scope/key/locale/channel (NULL tenant treated as global scope).
CREATE UNIQUE INDEX IF NOT EXISTS "UX_Templates_ScopeKeyLocaleChannel"
    ON "Templates" (COALESCE("TenantId", ''), "Key", "Locale", "Channel");

CREATE INDEX IF NOT EXISTS "IX_Templates_KeyChannel"
    ON "Templates" ("Key", "Channel");

CREATE INDEX IF NOT EXISTS "IX_Templates_TenantKeyChannel"
    ON "Templates" ("TenantId", "Key", "Channel");

