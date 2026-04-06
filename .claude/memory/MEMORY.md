# dCMS — Memory

**Stack:** Umbraco CMS (ASP.NET Core) + Next.js | **DB:** SQL Server | **Users:** Super Admin / Store Owners / End Customers

**Luôn nhớ:**
- Hierarchy 4 cấp: **Siêu thị (Tenant) → Brands[] → Stores[] → Storefront (Next.js)**
- **Tenant = Siêu thị** — 1 Umbraco instance + 1 SQL Server DB per Siêu thị
- Brand = layer tổ chức trong Siêu thị. Store = đơn vị bán (có storefront riêng)
- Mọi DB query scope theo **TenantId** (Siêu thị); filter thêm BrandId/StoreId khi cần
- Elasticsearch: `dcms-{tenantId}-*`, filter brandId/storeId trong query
- RBAC: Super Admin → Chain Admin → Brand Manager → Store Manager → Store Staff
- Rate limiting + CORS bắt buộc trên mọi API endpoint
- 2000 CCU target — tránh N+1, tránh in-memory state
- Multi-language + multi-currency từ đầu
- Payment qua API Gateway external, không xử lý trực tiếp

**Mode:** CONSULTANT AGENT

---

## Current State

- Status: Planning phase — khởi tạo bởi Blueberry Sensei (2026-04-03)
- Active branch: main
- Last task: initial setup & documentation

## Key Components (Proposed)

- `src/backend/dCMS.Core/` — Domain models, interfaces, business logic
- `src/backend/dCMS.Infrastructure/` — Repositories, external services, DB layer
- `src/backend/dCMS.Web/` — Umbraco web host, Content Delivery API endpoints
- `src/frontend/` — Next.js storefront (App Router)
- `infra/docker/` — Dockerfiles + docker-compose cho local dev

## In Progress

(none — project chưa có code)

## Recent Decisions

Xem `.claude/memory/decisions.md`

---

_Keep this file under 200 lines. Archive old context with compress-context skill._
