# dCMS — Memory

**Stack:** Umbraco CMS (ASP.NET Core) + Next.js | **DB:** PostgreSQL (Catalog + Inventory Dapper services) | **Users:** Super Admin / Store Owners / End Customers

**Luôn nhớ:**
- Hierarchy 4 cấp: **Siêu thị (Tenant) → Brands[] → Stores[] → Storefront (Next.js)**
- **Tenant = Siêu thị** — 1 Umbraco instance + isolated DB per Siêu thị (Umbraco); Catalog/Inventory services dùng PostgreSQL (schema hiện tại single-tenant scripts + `TenantId` columns)
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

- Status: Planning phase + **US-1 … US-13 (scaffold)** trong `src/backend/` (2026-04-12). **US-5 (DAI-234):** Catalog worker — index ES + MediatR + `ProductDocument` pipeline + Redis invalidation. **US-6 (DAI-235):** **GET** `…/stores/{storeId}/products` — Elasticsearch search cơ bản. **US-7 (DAI-236):** filters/facets/sort/cache Redis + `ElasticsearchProductSearchService`. **US-8 (DAI-237):** ES product index **versioned** (`…-v{N}`) + stable **alias** = `ElasticsearchIndexNames.Products`; `ProductSearchIndexAliasBootstrap` (greenfield hoặc legacy concrete cùng tên → reindex → alias write); bump `ProductSearchIndexVersion` khi đổi mapping. Testcontainers ES + `ElasticsearchClientFactory` `configureSettings`. Catalog API yêu cầu `Elasticsearch:Url`. **US-9 (DAI-252):** Store Manager catalog write — **POST/PUT** `…/products/bulk` (≤100), **PUT** `…/products/{id}` (full update), **DELETE** → archive, **PUT** `…/products/{id}/variants/{variantId}` (SKU/status/sortOrder, duplicate SKU → 409), `DuplicateVariantSkuException`; **idempotency** `IdempotencyMiddleware` (Redis, POST/PUT/PATCH trên path có `/api/v1/` + `/products`, TTL 24h). **Auth (riêng):** JWT + RBAC.
- **US-10 (DAI-255):** Public storefront API — **GET** `/api/v1/products?tenantId=&storeId=` (ES search + Redis 30s + `Cache-Control: public, max-age=30`), **GET** `/api/v1/products/{slug}` (SQL + variantMatrix, Redis cache-aside 10m, ETag `W/"product-{id}-v{unix}"`, `public, max-age=60`), **GET** `/api/v1/products/slug-check` (20 req/min/IP). Migration `010_AddCombinationCanonical` + `ProductVariant.CombinationCanonical` cho matrix keys; invalidate search/detail cache đã có từ indexer (`RedisCatalogSearchCacheInvalidator`).
- **US-11 (DAI-253):** Middleware stack — **HostTenantRoutingMiddleware** (Redis `dcms:host:{host}` → `HttpContext.Items`); **TenantStoreAccess**: **ChainAdmin**/**BrandManager** bỏ qua khớp `store_id` với route (vẫn khớp tenant); **rate limit** global partition theo tenant + tier Redis `dcms:tenant:plan:{tenantId}` (`starter` 200 / `pro` 500 / `enterprise` 1000 per `RateLimiting:WindowSeconds`, anonymous `anon:{ip}`); **AuditMiddleware** + **AuditLogChannel** + **AuditLogBackgroundService** + **SqlAuditLogPersistence** → bảng `AuditLogs` (mutating `/api/v1/tenants/...`, không log public `/api/v1/products`); **IPriceChangeAlerter** noop. Inventory.Api: `ConnectionStrings:Audit` (Compose → catalog DB); Redis trên inventory compose cho tier/host.
- **US-13 (DAI-276):** Umbraco 13 — **section** `dCMSCatalog` + wizard: Step 1–3; **Step 4 (DAI-285)** preview Cartesian client-side (`axesJson` → `combinationCount`, bảng tối đa 80 dòng, cảnh báo khi count rất lớn, map tên từ `variant-axes`); **Step 5** grid (DAI-286) + **review/publish (DAI-287):** `GET products/{id}` summary (status, variant count, active/inactive từ grid; price/stock ghi *n/a* do API/BFF); Save draft (variants); Submit/Publish; Hide (`POST …/hide` khi `active`); URL template mở tab sau publish/submit; workflow preference localStorage; BFF validator thêm `publish` / `submit-for-approval` / `hide` / `unhide` / `archive`; `forward` có `preserveError`. **DAI-281** grant admin/editor. **Linear:** parent **DAI-276** (US-13 epic) + **DAI-281–287** Done.
- **US-12 (DAI-254):** Inventory **POST** `…/stock/bulk` (≤100 items, `op`: adjust / reserve / release — partial success `succeeded`/`failed` như catalog bulk). **GET** `…/stock/variants/{variantId}` (stock theo warehouse). **GET/POST** `…/warehouses` (list + create, duplicate id → 409). **Internal** `POST /internal/inventory/{check|reserve|release}` + header **`X-Internal-Api-Key`** (SHA-256 fixed-time compare; `InternalInventory:ApiKey` empty → 503; rate limit off). Policy **`inventory:read`** (GET) / **`inventory:write`**. **IdempotencyMiddleware** + **IdempotencyOptions** chung trong `dCMS.Infrastructure`; Catalog `PathSubstrings = ["/products"]`, Inventory `["/stock"]`. Linear **Done** + comment API; integration **`WebApplicationFactory`** + Testcontainers PG: `dCMS.Tests/Integration/Inventory/` (marker `InventoryApiAssemblyMarker`).
- Active branch: main
- Last task: **DAI-276 (2026-04-12):** Linear epic US-13 → **Done** + comment tổng kết (child 281–287 đã Done trước đó). **Follow-up:** DbUp runner, dequeue locking, DLQ dedupe, ES mappings + indexing tests; wizard AC mở rộng (rich text, giá variant, store-config server, v.v.) → issue mới.

## Key Components (Proposed)

- `src/backend/dCMS.AspNetCore.Auth/` — JWT + RBAC policies + tenant/store route filter (auth layer; không phải US-5 ES indexing)
- `src/backend/dCMS.Core/` — Domain (Product SPU, events, `ProductService`, `ICatalogPersistence`, shared inventory exceptions)
- `src/backend/dCMS.Infrastructure/` — Dapper `SqlCatalogPersistence`, outbox relay, **search** (`ElasticsearchProductIndexer`, `ProductSearchIndexAliasBootstrap`, `SqlProductSearchRepository`, `ElasticsearchClientFactory`, Redis invalidator), SQL migrations (`001`–`004`, `008` DLQ, `009` audit/notifications)
- `src/backend/dCMS.Inventory.Core/` — `VariantStock`, `StockService`, `IInventoryStockPersistence`
- `src/backend/dCMS.Inventory.Infrastructure/` — `SqlStockPersistence`, migration `007_CreateInventory.sql`
- `src/backend/dCMS.Tests/` — xUnit + FluentAssertions + Moq + SkippableFact + Testcontainers.PostgreSql + Testcontainers.Elasticsearch
- `src/backend/dCMS.Catalog.Api/` — ASP.NET Core minimal APIs Catalog service (:5001 dev), envelope `{ data, meta, error }`, **product search (ES)**
- `src/backend/dCMS.Inventory.Api/` — Inventory REST (:5002 dev), stock adjust/reserve/release + bulk + idempotency (Redis)
- `src/backend/dCMS.Catalog.Worker/` — Outbox → RabbitMQ; **MediatR** product index notifications; ES + optional Redis
- `src/backend/dCMS.Web/` — **Umbraco 13** web host (`Umbraco.Cms` 13.13.1): `CreateUmbracoBuilder` + Delivery API + SQLite dev DB (`umbraco/Data/`), `ForwardedHeaders` trước pipeline Umbraco; Docker `infra/docker/Dockerfile.dcms-web` + compose service `umbraco-web` (:5000)
- `src/frontend/` — Next.js storefront (App Router)
- `infra/docker-compose.yml` + `infra/docker/*` — PostgreSQL, RabbitMQ, ES, Catalog/Inventory API, Worker; profile `test` → `m1-domain-tests` (xem `infra/README.md`)

## In Progress

- Catalog API: `ProductAttributeValues` / dynamic attributes persistence (post US-5)
- Worker hardening: DbUp migrations, dequeue locking, DLQ dedupe; ES **explicit mappings** + `StockUpdated` debounce; DB fields (`brandId`, prices, snapshot version) cho `ProductDocument`

## Recent Decisions

Xem `.claude/memory/decisions.md`

---

_Keep this file under 200 lines. Archive old context with compress-context skill._
