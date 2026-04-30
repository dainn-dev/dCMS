-- DAI-749 / US-2: Multi-branch chain platform schema foundation.
-- Adds Clients master table (top of hierarchy) and back-fills ClientId on every
-- tenant-scoped Catalog table. Default 'aeon' represents the single chain that
-- exists today; new deployments must override via Dcms:Client.Id.

CREATE TABLE IF NOT EXISTS "Clients"
(
    "Id"        VARCHAR(64)  NOT NULL PRIMARY KEY,
    "Name"      VARCHAR(200) NOT NULL,
    "CreatedAt" TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

INSERT INTO "Clients" ("Id", "Name")
VALUES ('aeon', 'Aeon Mall')
ON CONFLICT ("Id") DO NOTHING;

COMMENT ON TABLE "Clients" IS 'Top-of-hierarchy chain identity. One row per deployment in US-2; multi-row support arrives with Identity service in US-5.';

-- Categories
ALTER TABLE "Categories" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
DROP INDEX IF EXISTS "IX_Categories_Tenant";
CREATE INDEX IF NOT EXISTS "IX_Categories_Client_Tenant" ON "Categories" ("ClientId", "TenantId");

-- Products
ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
DROP INDEX IF EXISTS "IX_Products_Tenant_Store_Status";
CREATE INDEX IF NOT EXISTS "IX_Products_Client_Tenant_Store_Status" ON "Products" ("ClientId", "TenantId", "StoreId", "Status");

-- AuditLogs (009)
ALTER TABLE "AuditLogs" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
CREATE INDEX IF NOT EXISTS "IX_AuditLogs_Client_Tenant" ON "AuditLogs" ("ClientId", "TenantId");

-- NotificationEvents (009)
ALTER TABLE "NotificationEvents" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
CREATE INDEX IF NOT EXISTS "IX_NotificationEvents_Client_Tenant" ON "NotificationEvents" ("ClientId", "TenantId");

-- CatalogAttributes (011 / 019)
ALTER TABLE "CatalogAttributes" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
CREATE INDEX IF NOT EXISTS "IX_CatalogAttributes_Client_Tenant" ON "CatalogAttributes" ("ClientId", "TenantId");

-- StoreCatalogAttributeValues (011)
ALTER TABLE "StoreCatalogAttributeValues" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
CREATE INDEX IF NOT EXISTS "IX_StoreCatalogAttributeValues_Client_Tenant" ON "StoreCatalogAttributeValues" ("ClientId", "TenantId");

-- StoreCatalogSettings (013)
ALTER TABLE "StoreCatalogSettings" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
CREATE INDEX IF NOT EXISTS "IX_StoreCatalogSettings_Client_Tenant" ON "StoreCatalogSettings" ("ClientId", "TenantId");

-- Brands (016)
ALTER TABLE "Brands" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
DROP INDEX IF EXISTS "ix_brands_tenant";
CREATE INDEX IF NOT EXISTS "IX_Brands_Client_Tenant" ON "Brands" ("ClientId", "TenantId");

-- ImportJobs (025)
ALTER TABLE "ImportJobs" ADD COLUMN IF NOT EXISTS "ClientId" VARCHAR(64) NOT NULL DEFAULT 'aeon';
CREATE INDEX IF NOT EXISTS "IX_ImportJobs_Client_Tenant" ON "ImportJobs" ("ClientId", "TenantId");
