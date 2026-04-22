-- §Brand: tenant-scoped brand master data (DAI-574 / DAI-573).
CREATE TABLE IF NOT EXISTS "Brands"
(
    "TenantId"  VARCHAR(64)  NOT NULL,
    "Code"      VARCHAR(20)  NOT NULL,
    "Name"      VARCHAR(200) NOT NULL,
    "Active"    BOOLEAN      NOT NULL DEFAULT TRUE,
    "ImageUrl"  TEXT         NOT NULL DEFAULT '',
    "ImageAlt"  TEXT         NOT NULL DEFAULT '',
    "CreatedAt" TIMESTAMPTZ  NOT NULL,
    "UpdatedAt" TIMESTAMPTZ  NOT NULL,
    CONSTRAINT "PK_Brands" PRIMARY KEY ("TenantId", "Code")
);

CREATE INDEX IF NOT EXISTS "ix_brands_tenant"
    ON "Brands" ("TenantId");

COMMENT ON TABLE  "Brands"             IS 'Tenant-scoped brand master data. Code is unique within a tenant.';
COMMENT ON COLUMN "Brands"."TenantId"  IS 'Siêu thị (Tenant) identifier — multi-tenant isolation key.';
COMMENT ON COLUMN "Brands"."Code"      IS 'Brand code, e.g. CAS-7721. Unique per tenant. Format: 2–5 uppercase letters, dash, 1–6 digits.';
COMMENT ON COLUMN "Brands"."Active"    IS 'When false the brand is hidden from storefronts and API consumers.';
