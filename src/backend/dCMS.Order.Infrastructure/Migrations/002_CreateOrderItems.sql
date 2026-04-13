-- Order line items with catalog snapshots (US-18 / DAI-311). "Id" = domain line id (string).
CREATE TABLE IF NOT EXISTS "OrderItems"
(
    "Id"              VARCHAR(64)  NOT NULL PRIMARY KEY,
    "OrderId"         UUID         NOT NULL,
    "VariantId"     VARCHAR(64)    NOT NULL,
    "ProductId"     VARCHAR(64)    NOT NULL,
    "Quantity"      INTEGER        NOT NULL,
    "UnitPrice"     NUMERIC(18, 4) NOT NULL,
    "LineTotal"     NUMERIC(18, 4) NOT NULL,
    "ProductName"   JSONB          NOT NULL,
    "VariantSnapshot" JSONB        NOT NULL,
    CONSTRAINT "FK_OrderItems_Orders" FOREIGN KEY ("OrderId") REFERENCES "Orders" ("Id") ON DELETE CASCADE,
    CONSTRAINT "CK_OrderItems_Quantity_Positive" CHECK ("Quantity" > 0)
);

CREATE INDEX IF NOT EXISTS "IX_OrderItems_OrderId" ON "OrderItems" ("OrderId");
CREATE INDEX IF NOT EXISTS "IX_OrderItems_VariantId" ON "OrderItems" ("VariantId");
