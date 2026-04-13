-- US-22 / DAI-331 — Shipment event log (PostgreSQL).
CREATE TABLE IF NOT EXISTS "ShipmentEvents"
(
    "Id"         BIGSERIAL    PRIMARY KEY,
    "ShipmentId" UUID         NOT NULL,
    "Status"     VARCHAR(32)  NOT NULL DEFAULT '',
    "Location"   VARCHAR(256) NULL,
    "OccurredAt" TIMESTAMPTZ  NOT NULL,
    "Payload"    JSONB        NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT "FK_ShipmentEvents_Shipments" FOREIGN KEY ("ShipmentId") REFERENCES "Shipments" ("Id") ON DELETE CASCADE
);

-- Idempotency for webhook events.
CREATE UNIQUE INDEX IF NOT EXISTS "UQ_ShipmentEvents_Shipment_OccurredAt" ON "ShipmentEvents" ("ShipmentId", "OccurredAt");
CREATE INDEX IF NOT EXISTS "IX_ShipmentEvents_ShipmentId" ON "ShipmentEvents" ("ShipmentId");
CREATE INDEX IF NOT EXISTS "IX_ShipmentEvents_OccurredAt" ON "ShipmentEvents" ("OccurredAt");
