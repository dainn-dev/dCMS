-- DAI-659: Promo codes + workflow history (Promotions API).

CREATE TABLE IF NOT EXISTS "PromoCodes"
(
    "Id"             VARCHAR(36)  NOT NULL PRIMARY KEY,  -- promo_{ulid}
    "TenantId"       VARCHAR(64)  NOT NULL,
    "Code"           VARCHAR(100) NOT NULL,               -- WELCOME15 (unique per tenant)
    "NameJson"       TEXT         NOT NULL DEFAULT '{}',  -- {vi:"...", en:"..."}
    "DiscountType"   VARCHAR(30)  NOT NULL,               -- percentage|fixed|free_shipping
    "DiscountValue"  TEXT         NOT NULL DEFAULT '',    -- "15%" | "$10" | "cart>=50"
    "WorkflowState"  VARCHAR(30)  NOT NULL DEFAULT 'draft', -- draft|pending_approval|approved|rejected|archived
    "CreatedAt"      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT "UX_PromoCodes_Tenant_Code" UNIQUE ("TenantId", "Code")
);

CREATE INDEX IF NOT EXISTS "IX_PromoCodes_Tenant_State"
    ON "PromoCodes" ("TenantId", "WorkflowState");

CREATE TABLE IF NOT EXISTS "PromoCodeWorkflowHistory"
(
    "Id"           SERIAL       PRIMARY KEY,
    "PromoCodeId"  VARCHAR(36)  NOT NULL,
    "TenantId"     VARCHAR(64)  NOT NULL,
    "ActorUserId"  VARCHAR(64)  NOT NULL DEFAULT 'system',
    "FromState"    VARCHAR(30)  NOT NULL,
    "ToState"      VARCHAR(30)  NOT NULL,
    "Comment"      TEXT         NOT NULL DEFAULT '',
    "CreatedAt"    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_PromoCodeHistory_PromoCode"
    ON "PromoCodeWorkflowHistory" ("PromoCodeId", "TenantId");
