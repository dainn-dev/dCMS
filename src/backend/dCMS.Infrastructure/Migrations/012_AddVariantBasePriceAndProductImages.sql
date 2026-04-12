-- US-14: per-variant list pricing + product images metadata (blob path + checksum).
ALTER TABLE "ProductVariants"
    ADD COLUMN IF NOT EXISTS "BasePriceAmount" BIGINT NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS "ProductImages"
(
    "Id"               VARCHAR(64)  NOT NULL PRIMARY KEY,
    "ProductId"        VARCHAR(64)  NOT NULL,
    "StorageKey"       VARCHAR(512) NOT NULL DEFAULT '',
    "ChecksumSha256"   VARCHAR(64)  NOT NULL,
    "SortOrder"        INTEGER      NOT NULL DEFAULT 0,
    "IsPrimary"        BOOLEAN      NOT NULL DEFAULT FALSE,
    "ImageType"        VARCHAR(32)  NOT NULL DEFAULT 'gallery',
    "UploadStatus"     VARCHAR(32)  NOT NULL DEFAULT 'pending',
    "ContentLength"    BIGINT       NOT NULL DEFAULT 0,
    "CreatedAt"        TIMESTAMPTZ  NOT NULL,
    CONSTRAINT "FK_ProductImages_Products" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE,
    CONSTRAINT "UQ_ProductImages_Product_Checksum" UNIQUE ("ProductId", "ChecksumSha256")
);

CREATE INDEX IF NOT EXISTS "IX_ProductImages_ProductId_Sort" ON "ProductImages" ("ProductId", "SortOrder");
