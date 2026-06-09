-- DAI-31 - durable webhook replay protection.
CREATE TABLE IF NOT EXISTS "PaymentWebhookDeliveries"
(
    "Provider"        VARCHAR(64)  NOT NULL,
    "EventId"         VARCHAR(256) NOT NULL,
    "SignatureDigest" VARCHAR(128) NOT NULL,
    "ReceivedAt"      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT "PK_PaymentWebhookDeliveries" PRIMARY KEY ("Provider", "EventId")
);

CREATE INDEX IF NOT EXISTS "IX_PaymentWebhookDeliveries_ReceivedAt"
    ON "PaymentWebhookDeliveries" ("ReceivedAt");

CREATE INDEX IF NOT EXISTS "IX_PaymentWebhookDeliveries_SignatureDigest"
    ON "PaymentWebhookDeliveries" ("SignatureDigest");
