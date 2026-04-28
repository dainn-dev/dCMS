-- DAI-709 spike: seed analytics tables with ~1M "orders" worth of rollups.
-- Run against Analytics Postgres (ConnectionStrings:Analytics).
--
-- NOTE: This is synthetic data for latency testing only.

CREATE SCHEMA IF NOT EXISTS analytics;

CREATE TABLE IF NOT EXISTS analytics.orders_daily
(
    tenant_id      text        NOT NULL,
    store_id       text        NOT NULL,
    date           date        NOT NULL,
    orders_count   integer     NOT NULL,
    gross_amount   numeric     NOT NULL,
    net_amount     numeric     NOT NULL,
    refund_count   integer     NOT NULL,
    refund_amount  numeric     NOT NULL,
    PRIMARY KEY (tenant_id, store_id, date)
);

-- Index for common filters (tenant, store, date range)
CREATE INDEX IF NOT EXISTS ix_orders_daily_tenant_date ON analytics.orders_daily (tenant_id, date);
CREATE INDEX IF NOT EXISTS ix_orders_daily_tenant_store_date ON analytics.orders_daily (tenant_id, store_id, date);

-- Seed: 1 tenant, 50 stores, 365 days.
-- Each row is an aggregate bucket; total "orders" implied by orders_count sums to ~1,000,000.
WITH days AS (
    SELECT (DATE '2025-01-01' + (g.i * INTERVAL '1 day'))::date AS d
    FROM generate_series(0, 364) AS g(i)
),
stores AS (
    SELECT 's' || i::text AS store_id
    FROM generate_series(1, 50) AS g(i)
),
base AS (
    SELECT
        't1'::text AS tenant_id,
        s.store_id,
        d.d AS date,
        -- roughly: 1,000,000 / (50*365) ≈ 54.8
        (40 + (random() * 40)::int) AS orders_count
    FROM stores s
    CROSS JOIN days d
)
INSERT INTO analytics.orders_daily (
    tenant_id, store_id, date,
    orders_count, gross_amount, net_amount,
    refund_count, refund_amount
)
SELECT
    tenant_id,
    store_id,
    date,
    orders_count,
    (orders_count * (50 + random() * 200))::numeric AS gross_amount,
    (orders_count * (45 + random() * 190))::numeric AS net_amount,
    (orders_count * 0.02)::int AS refund_count,
    (orders_count * (1 + random() * 5))::numeric AS refund_amount
FROM base
ON CONFLICT (tenant_id, store_id, date) DO UPDATE SET
    orders_count = EXCLUDED.orders_count,
    gross_amount = EXCLUDED.gross_amount,
    net_amount = EXCLUDED.net_amount,
    refund_count = EXCLUDED.refund_count,
    refund_amount = EXCLUDED.refund_amount;

