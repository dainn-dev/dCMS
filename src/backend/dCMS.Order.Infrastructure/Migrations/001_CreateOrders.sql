-- Orders (US-18 / DAI-311). PostgreSQL; aligns with ConnectionStrings:Order → dcms_order.
CREATE TABLE IF NOT EXISTS "Orders"
(
    "Id"               UUID         NOT NULL PRIMARY KEY DEFAULT gen_random_uuid(),
    "TenantId"         VARCHAR(64)  NOT NULL,
    "StoreId"          VARCHAR(64)  NOT NULL,
    "CustomerId"       VARCHAR(64)  NOT NULL,
    "Status"           VARCHAR(32)  NOT NULL,
    "Currency"         VARCHAR(8)   NOT NULL DEFAULT 'USD',
    "SubTotal"         NUMERIC(18, 4) NOT NULL DEFAULT 0,
    "TaxTotal"         NUMERIC(18, 4) NOT NULL DEFAULT 0,
    "Total"            NUMERIC(18, 4) NOT NULL DEFAULT 0,
    "PaymentIntentId"  VARCHAR(128) NULL,
    "IdempotencyKey"   VARCHAR(128) NOT NULL,
    "CreatedAt"        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT "CK_Orders_Status_NotEmpty" CHECK (length(trim("Status")) > 0),
    CONSTRAINT "CK_Orders_Idempotency_NotEmpty" CHECK (length(trim("IdempotencyKey")) > 0),
    CONSTRAINT "UQ_Orders_Idempotency" UNIQUE ("TenantId", "StoreId", "IdempotencyKey")
);

CREATE INDEX IF NOT EXISTS "IX_Orders_Tenant_Store_Customer" ON "Orders" ("TenantId", "StoreId", "CustomerId");
CREATE INDEX IF NOT EXISTS "IX_Orders_Status" ON "Orders" ("Status");
CREATE INDEX IF NOT EXISTS "IX_Orders_CreatedAt" ON "Orders" ("CreatedAt");
