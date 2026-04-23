-- DAI-599: Campaign management tables.

CREATE TABLE IF NOT EXISTS "Campaigns"
(
    "Id"                   VARCHAR(36)  NOT NULL PRIMARY KEY,  -- ULID
    "TenantId"             VARCHAR(64)  NOT NULL,
    "Code"                 VARCHAR(100) NOT NULL,
    "NameJson"             TEXT         NOT NULL DEFAULT '{}',  -- {en:"",zh:""}
    "EditorKind"           VARCHAR(30)  NOT NULL,               -- pwp-item|pwp-discount|mix-match|product-discount|after-sales
    "WorkflowState"        VARCHAR(30)  NOT NULL DEFAULT 'draft',
    "Channel"              VARCHAR(20)  NOT NULL DEFAULT 'Email',
    "StartDate"            TIMESTAMPTZ  NULL,
    "EndDate"              TIMESTAMPTZ  NULL,
    "ActiveDaysJson"       TEXT         NOT NULL DEFAULT '[]',  -- ["Mon","Tue",...]
    "ActiveMonthsJson"     TEXT         NOT NULL DEFAULT '[]',  -- ["Jan","Feb",...]
    "QualifiersJson"       JSONB        NOT NULL DEFAULT '{}',  -- {inclusion:{...}, exclusion:{...}}
    "MechanicsJson"        JSONB        NOT NULL DEFAULT '{}',  -- kind-specific fields
    "PromotionDetailsJson" JSONB        NOT NULL DEFAULT '{}',  -- message, priority, flags
    "Budget"               TEXT         NOT NULL DEFAULT '',
    "Audience"             TEXT         NOT NULL DEFAULT '',
    "Conversions"          INTEGER      NOT NULL DEFAULT 0,
    "CreatedAt"            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT "UX_Campaigns_Tenant_Code" UNIQUE ("TenantId", "Code")
);

CREATE INDEX IF NOT EXISTS "IX_Campaigns_Tenant_State"
    ON "Campaigns" ("TenantId", "WorkflowState");

CREATE INDEX IF NOT EXISTS "IX_Campaigns_Tenant_Channel"
    ON "Campaigns" ("TenantId", "Channel");

CREATE TABLE IF NOT EXISTS "CampaignWorkflowHistory"
(
    "Id"          SERIAL       PRIMARY KEY,
    "CampaignId"  VARCHAR(36)  NOT NULL,
    "TenantId"    VARCHAR(64)  NOT NULL,
    "ActorUserId" VARCHAR(64)  NOT NULL DEFAULT 'system',
    "FromState"   VARCHAR(30)  NOT NULL,
    "ToState"     VARCHAR(30)  NOT NULL,
    "Comment"     TEXT         NOT NULL DEFAULT '',
    "CreatedAt"   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_CampaignHistory_Campaign"
    ON "CampaignWorkflowHistory" ("CampaignId", "TenantId");

COMMENT ON COLUMN "Campaigns"."QualifiersJson"       IS '{"inclusion":{"brands":[],"categories":[],"pid1":[],"pid2":[],"products":[],"cartAllQualifiers":false,"minSpend":"","minSpendNet":false},"exclusion":{"brands":[],"categories":[],"pid1":[],"pid2":[],"products":[],"membershipTypes":[],"membershipTiers":[]}}';
COMMENT ON COLUMN "Campaigns"."MechanicsJson"        IS 'Kind-specific object. See CampaignRow.ValidEditorKinds for shape per kind.';
COMMENT ON COLUMN "Campaigns"."PromotionDetailsJson"  IS '{"orderDetailMessage":"","promotionDetails":"","showPromoOnProductPage":false,"promotionMessagePriority":"","campaignPriority":"","blockOtherPromos":false,"blockSamePriority":false}';
