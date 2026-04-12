-- Per-store catalog UX / notification settings (AC: approval required toggle, optional low-stock threshold).
CREATE TABLE IF NOT EXISTS "StoreCatalogSettings"
(
    "TenantId"          VARCHAR(64) NOT NULL,
    "StoreId"           VARCHAR(64) NOT NULL,
    "ApprovalRequired"  BOOLEAN     NOT NULL DEFAULT FALSE,
    "LowStockThreshold" INTEGER     NULL,
    "UpdatedAt"         TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT "PK_StoreCatalogSettings" PRIMARY KEY ("TenantId", "StoreId")
);
