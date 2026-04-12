-- Tenant-level category tree (minimal for Products FK). PostgreSQL.
CREATE TABLE IF NOT EXISTS "Categories"
(
    "Id"          SERIAL PRIMARY KEY,
    "TenantId"   VARCHAR(64)  NOT NULL,
    "ParentId"   INTEGER      NULL,
    "Path"       VARCHAR(512) NOT NULL DEFAULT '/',
    "Depth"      INTEGER      NOT NULL DEFAULT 0,
    "Name"       TEXT         NOT NULL,
    "Slug"       VARCHAR(256) NOT NULL,
    "SortOrder"  INTEGER      NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS "IX_Categories_Tenant" ON "Categories" ("TenantId");
