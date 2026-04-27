-- DAI-710: analytics tables for reporting read-model (PostgreSQL analytics DB).
CREATE SCHEMA IF NOT EXISTS analytics;

CREATE TABLE IF NOT EXISTS analytics.event_dedup
(
    event_id      uuid        NOT NULL PRIMARY KEY,
    processed_at  timestamptz NOT NULL DEFAULT now()
);

-- Daily rollup: one row per tenant/store/day.
CREATE TABLE IF NOT EXISTS analytics.orders_daily
(
    tenant_id      text        NOT NULL,
    store_id       text        NOT NULL,
    date           date        NOT NULL,
    orders_count   integer     NOT NULL DEFAULT 0,
    gross_amount   numeric     NOT NULL DEFAULT 0,
    net_amount     numeric     NOT NULL DEFAULT 0,
    refund_count   integer     NOT NULL DEFAULT 0,
    refund_amount  numeric     NOT NULL DEFAULT 0,
    PRIMARY KEY (tenant_id, store_id, date)
);

CREATE INDEX IF NOT EXISTS ix_orders_daily_tenant_date
    ON analytics.orders_daily (tenant_id, date);
CREATE INDEX IF NOT EXISTS ix_orders_daily_tenant_store_date
    ON analytics.orders_daily (tenant_id, store_id, date);

-- Sales rollups by dimension.
CREATE TABLE IF NOT EXISTS analytics.sales_by_product
(
    tenant_id    text    NOT NULL,
    store_id     text    NOT NULL,
    product_id   text    NOT NULL,
    date         date    NOT NULL,
    units_sold   integer NOT NULL DEFAULT 0,
    gross_amount numeric NOT NULL DEFAULT 0,
    PRIMARY KEY (tenant_id, store_id, product_id, date)
);

CREATE TABLE IF NOT EXISTS analytics.sales_by_category
(
    tenant_id     text    NOT NULL,
    store_id      text    NOT NULL,
    category_id   integer NOT NULL,
    date          date    NOT NULL,
    units_sold    integer NOT NULL DEFAULT 0,
    gross_amount  numeric NOT NULL DEFAULT 0,
    PRIMARY KEY (tenant_id, store_id, category_id, date)
);

-- Placeholder for DAI-711 / Phase 2 integration.
CREATE TABLE IF NOT EXISTS analytics.cart_events
(
    tenant_id    text        NOT NULL,
    store_id     text        NOT NULL,
    cart_id      text        NOT NULL,
    event_type   text        NOT NULL,
    occurred_at  timestamptz NOT NULL,
    payload      jsonb       NULL
);
CREATE INDEX IF NOT EXISTS ix_cart_events_tenant_occurred ON analytics.cart_events (tenant_id, occurred_at DESC);

CREATE TABLE IF NOT EXISTS analytics.restock_subscriptions
(
    tenant_id     text        NOT NULL,
    store_id      text        NOT NULL,
    product_id    text        NOT NULL,
    customer_id   text        NOT NULL,
    created_at    timestamptz NOT NULL,
    fulfilled_at  timestamptz NULL,
    PRIMARY KEY (tenant_id, store_id, product_id, customer_id, created_at)
);

