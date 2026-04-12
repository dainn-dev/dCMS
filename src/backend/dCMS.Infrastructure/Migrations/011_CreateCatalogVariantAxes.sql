-- US-13 / DAI-284: tenant catalog attributes + values; optional per-store allowlist for wizard / variant generation.
CREATE TABLE IF NOT EXISTS "CatalogAttributes"
(
    "Id"          SERIAL PRIMARY KEY,
    "TenantId"    VARCHAR(64) NOT NULL,
    "Name"        TEXT         NOT NULL,
    "SortOrder"   INTEGER      NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS "IX_CatalogAttributes_Tenant" ON "CatalogAttributes" ("TenantId");

CREATE TABLE IF NOT EXISTS "CatalogAttributeValues"
(
    "Id"           SERIAL PRIMARY KEY,
    "AttributeId"  INTEGER      NOT NULL,
    "Name"         TEXT         NOT NULL,
    "SortOrder"    INTEGER      NOT NULL DEFAULT 0,
    CONSTRAINT "FK_CatalogAttributeValues_Attribute" FOREIGN KEY ("AttributeId") REFERENCES "CatalogAttributes" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_CatalogAttributeValues_Attribute" ON "CatalogAttributeValues" ("AttributeId");

-- When any row exists for (TenantId, StoreId), only those value IDs are exposed for the store (override).
-- When no rows exist for the store, all tenant attribute values are available.
CREATE TABLE IF NOT EXISTS "StoreCatalogAttributeValues"
(
    "TenantId"         VARCHAR(64) NOT NULL,
    "StoreId"          VARCHAR(64) NOT NULL,
    "AttributeValueId" INTEGER     NOT NULL,
    CONSTRAINT "FK_StoreCatalogAttributeValues_Value" FOREIGN KEY ("AttributeValueId") REFERENCES "CatalogAttributeValues" ("Id") ON DELETE CASCADE,
    CONSTRAINT "PK_StoreCatalogAttributeValues" PRIMARY KEY ("TenantId", "StoreId", "AttributeValueId")
);

CREATE INDEX IF NOT EXISTS "IX_StoreCatalogAttributeValues_Store" ON "StoreCatalogAttributeValues" ("TenantId", "StoreId");
