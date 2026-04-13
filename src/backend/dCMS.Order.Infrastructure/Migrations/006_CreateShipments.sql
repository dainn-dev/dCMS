-- US-22 / DAI-331 — Shipments read model (PostgreSQL).
CREATE TABLE IF NOT EXISTS "Shipments"
(
    "Id"             UUID         NOT NULL PRIMARY KEY DEFAULT gen_random_uuid(),
    "OrderId"        UUID         NOT NULL,
    "Carrier"        VARCHAR(64)  NOT NULL DEFAULT '',
    "TrackingNumber" VARCHAR(128) NOT NULL DEFAULT '',
    "Status"         VARCHAR(32)  NOT NULL DEFAULT '',
    "EstimatedAt"    TIMESTAMPTZ  NULL,
    "DeliveredAt"    TIMESTAMPTZ  NULL,
    "CreatedAt"      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT "FK_Shipments_Orders" FOREIGN KEY ("OrderId") REFERENCES "Orders" ("Id") ON DELETE CASCADE
);

-- Typically one shipment per order in MVP; enforce uniqueness on OrderId.
CREATE UNIQUE INDEX IF NOT EXISTS "UQ_Shipments_OrderId" ON "Shipments" ("OrderId");
CREATE INDEX IF NOT EXISTS "IX_Shipments_Status" ON "Shipments" ("Status");
CREATE INDEX IF NOT EXISTS "IX_Shipments_Carrier_Tracking" ON "Shipments" ("Carrier", "TrackingNumber");
