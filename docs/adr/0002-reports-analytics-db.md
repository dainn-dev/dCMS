# ADR 0002 — Reports analytics DB separation

**Date:** 2026-04-27  
**Status:** Accepted (DAI-709)  
**Related:** DAI-685 (epic), DAI-710 (projections), DAI-711 (reports endpoints + UI)

## Context

Hiện tại các report endpoints đang query trực tiếp OLTP PostgreSQL của Order service (ví dụ `OrderReportRoutes` → `OrderReportQueryStore`).
Khi report nặng (grouping + large time range) sẽ **impact transactional load** và phá vỡ decision “Reports chạy trên read models / analytics DB thay vì OLTP”.

Mục tiêu của epic:

- Tách report queries khỏi OLTP.
- Dùng event-driven projections (MassTransit) để cập nhật analytics tables.
- Reports endpoints chỉ query analytics DB.

## Decision

Chọn **PostgreSQL analytics DB riêng** (separate database instance) cho read-model:

- OLTP (Order service) tiếp tục dùng `ConnectionStrings:Order`.
- Analytics dùng `ConnectionStrings:Analytics` (Postgres dedicated).
- Projection worker (`dCMS.Reports.Worker`) consume các order lifecycle messages và upsert vào `analytics.*` tables trong analytics DB.
- Reports API (`dCMS.Reports.Api`) query analytics DB, không chạm OLTP.

## Options considered

### Option A — PG read replica (logical/streaming replica)

- **Pros**: SQL giống OLTP, có thể tận dụng schema sẵn có.
- **Cons**: Report nặng vẫn ăn CPU/IO (ở replica) và có thể tăng replication lag; cần ops cho replication; vẫn query schema OLTP (wide tables).

### Option B — Separate `analytics` schema ngay trong OLTP DB

- **Pros**: rẻ, đơn giản nhất.
- **Cons**: vẫn chia sẻ compute/storage với OLTP ⇒ không đạt mục tiêu isolation.

### Option C — Columnar (ClickHouse/DuckDB)

- **Pros**: aggregate cực nhanh.
- **Cons**: ops overhead cao, data ingestion/consistency phức tạp cho MVP.

### Option D — Materialized views trong OLTP

- **Pros**: triển khai nhanh.
- **Cons**: refresh scheduling; vẫn chạy compute trên OLTP; hard to keep near-real-time.

## Rationale

Option “analytics DB riêng” là cân bằng tốt nhất cho MVP:

- **Isolation**: report load không ảnh hưởng OLTP.
- **Simplicity**: vẫn là PostgreSQL + SQL quen thuộc.
- **Scalability**: dễ scale read workload độc lập.
- **Event-driven correctness**: projections idempotent + dedup đảm bảo không double-count.

## Consequences

- Cần thêm connection string `Analytics` trong infra + compose.
- Cần worker để project events (có dedup table).
- Cần cơ chế backfill một lần từ OLTP để có dữ liệu lịch sử.

## Spike / verification (pragmatic)

Trong repo, cung cấp script seed + query benchmark để kiểm tra latency trong môi trường local:

- `infra/loadtests/seed-analytics-1m.sql`: seed 1M rows cho daily rollups.
- Benchmark query: `SELECT ... GROUP BY ...` trên `analytics.orders_daily` (warm cache) với index phù hợp.

**Acceptance target**: p95 < 500ms trên analytics DB với 1M orders (thực tế production sẽ cần tune index + partition).

