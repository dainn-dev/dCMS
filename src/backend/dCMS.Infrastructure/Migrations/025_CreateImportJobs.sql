-- DAI-684 / DAI-706: bulk import job tracking (catalog DB).
-- Stores async import jobs (products, product-images, inventory, promo-codes) for the worker
-- to consume from RabbitMQ. Status drives the polling endpoint shown in the backoffice.

CREATE TABLE IF NOT EXISTS "ImportJobs"
(
    "Id"               VARCHAR(36) NOT NULL PRIMARY KEY,           -- imp_{ulid}
    "TenantId"         VARCHAR(64) NOT NULL,
    "Type"             VARCHAR(40) NOT NULL,                       -- products|product-images|inventory|promo-codes
    "Status"           VARCHAR(30) NOT NULL DEFAULT 'Pending',     -- Pending|Running|Completed|Failed|PartiallyCompleted
    "FileKey"          TEXT        NOT NULL,                       -- s3 / object store key
    "Total"            INTEGER     NULL,
    "Processed"        INTEGER     NOT NULL DEFAULT 0,
    "Errors"           JSONB       NOT NULL DEFAULT '[]'::jsonb,   -- [{rowIndex,key,message}]
    "LastProcessedKey" TEXT        NULL,                           -- resume cursor (sku/code/external_id)
    "CreatedBy"        VARCHAR(64) NOT NULL DEFAULT 'system',
    "CreatedAt"        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "StartedAt"        TIMESTAMPTZ NULL,
    "CompletedAt"      TIMESTAMPTZ NULL,
    CONSTRAINT "CK_ImportJobs_Type" CHECK ("Type" IN ('products','product-images','inventory','promo-codes')),
    CONSTRAINT "CK_ImportJobs_Status" CHECK ("Status" IN ('Pending','Running','Completed','Failed','PartiallyCompleted'))
);

CREATE INDEX IF NOT EXISTS "IX_ImportJobs_Tenant_Status"
    ON "ImportJobs" ("TenantId", "Status");

CREATE INDEX IF NOT EXISTS "IX_ImportJobs_Created"
    ON "ImportJobs" ("TenantId", "CreatedAt" DESC);
