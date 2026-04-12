-- Product variants (SKU).
CREATE TABLE IF NOT EXISTS "ProductVariants"
(
    "Id"              VARCHAR(64)  NOT NULL PRIMARY KEY,
    "ProductId"       VARCHAR(64)  NOT NULL,
    "SKU"             VARCHAR(256) NOT NULL,
    "CombinationHash" VARCHAR(64)  NOT NULL,
    "Status"          VARCHAR(32)  NOT NULL DEFAULT 'active',
    "SortOrder"       INTEGER      NOT NULL DEFAULT 0,
    CONSTRAINT "FK_ProductVariants_Products" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id"),
    CONSTRAINT "UQ_ProductVariants_ProductCombination" UNIQUE ("ProductId", "CombinationHash")
);

CREATE INDEX IF NOT EXISTS "IX_ProductVariants_ProductId" ON "ProductVariants" ("ProductId");
