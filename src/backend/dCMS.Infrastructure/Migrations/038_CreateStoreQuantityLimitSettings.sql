-- Per-store product quantity limit configuration (eStore → Products → Product Quantity Limit Settings).
-- General default applies store-wide; advance rules override for matching qualifiers.

CREATE TABLE IF NOT EXISTS "StoreQuantityLimitSettings"
(
    "TenantId"            VARCHAR(64) NOT NULL,
    "StoreId"             VARCHAR(64) NOT NULL,
    "CartLimitPerProduct" INTEGER     NOT NULL DEFAULT 1000,
    "UpdatedAt"           TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT "PK_StoreQuantityLimitSettings" PRIMARY KEY ("TenantId", "StoreId"),
    CONSTRAINT "CK_StoreQuantityLimitSettings_CartLimit" CHECK ("CartLimitPerProduct" > 0)
);

CREATE TABLE IF NOT EXISTS "StoreQuantityLimitRules"
(
    "Id"              VARCHAR(64)  NOT NULL PRIMARY KEY,
    "TenantId"        VARCHAR(64)  NOT NULL,
    "StoreId"         VARCHAR(64)  NOT NULL,
    "Name"            VARCHAR(256) NOT NULL,
    "LimitType"       VARCHAR(32)  NOT NULL,
    "PerProduct"      BOOLEAN      NOT NULL DEFAULT FALSE,
    "QuantityLimit"   INTEGER      NOT NULL,
    "StartDate"       DATE         NOT NULL,
    "EndDate"         DATE         NULL,
    "BrandId"         VARCHAR(64)  NULL,
    "CategoryIds"     JSONB        NOT NULL DEFAULT '[]'::jsonb,
    "ProductId"       VARCHAR(64)  NULL,
    "MembershipType"  VARCHAR(64)  NULL,
    "MembershipTier"  VARCHAR(64)  NULL,
    "ModifiedBy"      VARCHAR(64)  NOT NULL DEFAULT '',
    "CreatedAt"       TIMESTAMPTZ  NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    "UpdatedAt"       TIMESTAMPTZ  NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT "FK_StoreQuantityLimitRules_Store" UNIQUE ("TenantId", "StoreId", "Id"),
    CONSTRAINT "CK_StoreQuantityLimitRules_LimitType" CHECK ("LimitType" IN ('per_cart', 'per_user')),
    CONSTRAINT "CK_StoreQuantityLimitRules_QuantityLimit" CHECK ("QuantityLimit" > 0)
);

CREATE INDEX IF NOT EXISTS "IX_StoreQuantityLimitRules_Store"
    ON "StoreQuantityLimitRules" ("TenantId", "StoreId", "StartDate" DESC);

COMMENT ON TABLE  "StoreQuantityLimitSettings" IS 'Store-wide default max quantity per product in a cart.';
COMMENT ON TABLE  "StoreQuantityLimitRules"    IS 'Advanced quantity limit rules with brand/category/product/membership qualifiers.';
