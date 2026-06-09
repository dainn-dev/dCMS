-- DAI-687: admin-managed catalog of message template types (tenant-scoped).
-- Replaces the static appsettings catalog so operators can add/edit/remove
-- message types and their variables from the backoffice UI.
CREATE TABLE IF NOT EXISTS "TemplateDefinitions"
(
    "Id"             UUID         NOT NULL PRIMARY KEY,
    "TenantId"       VARCHAR(64)  NOT NULL,
    "Key"            TEXT         NOT NULL,
    "Channel"        TEXT         NOT NULL,
    "Name"           TEXT         NOT NULL,
    "Description"    TEXT         NOT NULL DEFAULT '',
    "Variables"      JSONB        NOT NULL DEFAULT '[]'::jsonb,
    "DefaultSubject" TEXT         NULL,
    "DefaultBody"    TEXT         NULL,
    "SortOrder"      INTEGER      NOT NULL DEFAULT 0,
    "UpdatedAt"      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    "UpdatedBy"      VARCHAR(64)  NULL
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_TemplateDefinitions_Channel') THEN
        ALTER TABLE "TemplateDefinitions"
            ADD CONSTRAINT "CK_TemplateDefinitions_Channel"
            CHECK ("Channel" IN ('email','sms','print','admin'));
    END IF;
END $$;

-- One definition per tenant/key/channel.
CREATE UNIQUE INDEX IF NOT EXISTS "UX_TemplateDefinitions_TenantKeyChannel"
    ON "TemplateDefinitions" ("TenantId", "Key", "Channel");

CREATE INDEX IF NOT EXISTS "IX_TemplateDefinitions_Tenant"
    ON "TemplateDefinitions" ("TenantId", "SortOrder");
