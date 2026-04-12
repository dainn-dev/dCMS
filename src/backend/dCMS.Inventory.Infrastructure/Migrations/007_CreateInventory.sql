-- Inventory: warehouses, variant stock (optimistic revision), movements, outbox.
CREATE TABLE IF NOT EXISTS "Warehouses"
(
    "Id"       VARCHAR(64)  NOT NULL PRIMARY KEY,
    "TenantId" VARCHAR(64)  NOT NULL,
    "StoreId"  VARCHAR(64)  NOT NULL,
    "Name"     VARCHAR(200) NOT NULL,
    "Address"  VARCHAR(500) NULL,
    "IsActive" BOOLEAN      NOT NULL DEFAULT TRUE
);

CREATE INDEX IF NOT EXISTS "IX_Warehouses_Tenant_Store" ON "Warehouses" ("TenantId", "StoreId");

CREATE TABLE IF NOT EXISTS "VariantStock"
(
    "Id"               SERIAL PRIMARY KEY,
    "VariantId"        VARCHAR(64) NOT NULL,
    "WarehouseId"      VARCHAR(64) NOT NULL,
    "Quantity"         INTEGER     NOT NULL,
    "ReservedQuantity" INTEGER     NOT NULL,
    "Revision"         BIGINT      NOT NULL DEFAULT 1,
    CONSTRAINT "FK_VariantStock_Warehouse" FOREIGN KEY ("WarehouseId") REFERENCES "Warehouses" ("Id"),
    CONSTRAINT "UQ_VariantStock_Variant_Warehouse" UNIQUE ("VariantId", "WarehouseId"),
    CONSTRAINT "CK_VariantStock_NonNegative" CHECK ("Quantity" >= 0 AND "ReservedQuantity" >= 0),
    CONSTRAINT "CK_VariantStock_ReservedLeQty" CHECK ("ReservedQuantity" <= "Quantity")
);

CREATE INDEX IF NOT EXISTS "IX_VariantStock_Variant_Warehouse" ON "VariantStock" ("VariantId", "WarehouseId");

CREATE TABLE IF NOT EXISTS "StockMovements"
(
    "Id"          BIGSERIAL PRIMARY KEY,
    "VariantId"   VARCHAR(64)  NOT NULL,
    "WarehouseId" VARCHAR(64)  NOT NULL,
    "Delta"       INTEGER      NOT NULL,
    "Type"        VARCHAR(32)  NOT NULL,
    "ReferenceId" VARCHAR(128) NULL,
    "CreatedAt"   TIMESTAMPTZ  NOT NULL,
    "CreatedBy"   VARCHAR(128) NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_StockMovements_Variant_Warehouse_Created"
    ON "StockMovements" ("VariantId", "WarehouseId", "CreatedAt");

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

CREATE INDEX IF NOT EXISTS "IX_Inventory_Outbox_Unprocessed" ON "OutboxEvents" ("ProcessedAt", "CreatedAt")
    WHERE "ProcessedAt" IS NULL;
