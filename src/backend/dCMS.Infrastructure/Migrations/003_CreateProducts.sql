-- Products (SPU) + catalog outbox.
CREATE TABLE IF NOT EXISTS "Products"
(
    "Id"              VARCHAR(64)  NOT NULL PRIMARY KEY,
    "TenantId"        VARCHAR(64)  NOT NULL,
    "StoreId"         VARCHAR(64)  NOT NULL,
    "CategoryId"      INTEGER        NOT NULL,
    "Name"            TEXT         NOT NULL,
    "Description"     TEXT         NOT NULL,
    "Slug"            VARCHAR(256) NOT NULL,
    "Status"          VARCHAR(32)  NOT NULL,
    "SalesCount30d"   INTEGER      NOT NULL DEFAULT 0,
    "CreatedAt"       TIMESTAMPTZ  NOT NULL,
    "UpdatedAt"       TIMESTAMPTZ  NOT NULL,
    CONSTRAINT "FK_Products_Categories" FOREIGN KEY ("CategoryId") REFERENCES "Categories" ("Id"),
    CONSTRAINT "UQ_Products_StoreSlug" UNIQUE ("StoreId", "Slug")
);

CREATE INDEX IF NOT EXISTS "IX_Products_Tenant_Store_Status" ON "Products" ("TenantId", "StoreId", "Status");
CREATE INDEX IF NOT EXISTS "IX_Products_Category_Status" ON "Products" ("CategoryId", "Status");

CREATE TABLE IF NOT EXISTS "OutboxEvents"
(
    "Id"          BIGSERIAL PRIMARY KEY,
    "EventType"   VARCHAR(128) NOT NULL,
    "Payload"     TEXT         NOT NULL,
    "CreatedAt"   TIMESTAMPTZ  NOT NULL,
    "ProcessedAt" TIMESTAMPTZ  NULL,
    "RetryCount"  INTEGER      NOT NULL DEFAULT 0,
    "Error"       TEXT         NULL
);

CREATE INDEX IF NOT EXISTS "IX_Outbox_Unprocessed" ON "OutboxEvents" ("ProcessedAt", "CreatedAt")
    WHERE "ProcessedAt" IS NULL;
