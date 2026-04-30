-- DAI-XXX: Composite covering index for transaction report queries.
-- Supports WHERE TenantId = @T AND StoreId = @S AND CreatedAt BETWEEN @From AND @To
-- Includes columns the overview/details aggregates project — keeps reads index-only at 2000 CCU target.
CREATE INDEX IF NOT EXISTS "IX_Orders_Tenant_Store_CreatedAt"
    ON "Orders" ("TenantId", "StoreId", "CreatedAt" DESC)
    INCLUDE ("Total", "TaxTotal", "OrderDiscount", "CustomerId", "Status");
