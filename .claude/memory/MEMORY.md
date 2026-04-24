# dCMS — Memory

**Stack:** Umbraco CMS (ASP.NET Core) + Next.js (Phase 2) | **DB:** PostgreSQL (Catalog + Inventory + Order via Dapper); SQL Server (Umbraco) | **Users:** Super Admin / Chain Admin / Brand / Store Manager / Staff / End Customers

**Luôn nhớ:**
- Hierarchy 4 cấp: **Siêu thị (Tenant) → Brands[] → Stores[] → Storefront (Next.js, Phase 2)**
- **Tenant = Siêu thị** — 1 Umbraco instance + isolated DB per Siêu thị; Catalog/Inventory/Order services dùng PostgreSQL với cột `TenantId`
- Brand = layer tổ chức trong Siêu thị. Store = đơn vị bán (có storefront riêng)
- Mọi DB query scope theo **TenantId**; filter thêm BrandId/StoreId khi cần
- Elasticsearch: `dcms-{tenantId}-*`, filter brandId/storeId trong query
- RBAC: SuperAdmin → ChainAdmin → BrandManager → StoreManager → StoreStaff
- Hai lớp auth: Umbraco UserGroups + AllowedSections (shell backoffice) vs Platform JWT + ASP.NET policies (API business scope)
- Rate limiting + CORS bắt buộc trên mọi API endpoint
- 2000 CCU target — tránh N+1, tránh in-memory state
- Multi-language + multi-currency từ đầu
- Payment qua API Gateway external, không xử lý trực tiếp

**Mode:** CONSULTANT AGENT

---

## Current State

Status: Planning + scaffold US-1…US-13 trong `src/backend/` (2026-04-12) + Backoffice SPA + Access module + Order/Promotions mở rộng.

**M1 Catalog (US-1…US-13) — done (2026-04-12):**
- **US-5** `DAI-234` Catalog worker ES indexing + MediatR + `ProductDocument` + Redis invalidation
- **US-6/7** `DAI-235/236` GET `/stores/{storeId}/products` ES search + facets/sort + Redis cache + `ElasticsearchProductSearchService`
- **US-8** `DAI-237` ES index versioned (`…-v{N}`) + alias = `ElasticsearchIndexNames.Products`; `ProductSearchIndexAliasBootstrap` (greenfield / legacy concrete → reindex → alias); bump `ProductSearchIndexVersion` khi đổi mapping; Testcontainers ES + `ElasticsearchClientFactory`
- **US-9** `DAI-252` Store Manager writes — POST/PUT `/products/bulk` (≤100), PUT `/products/{id}`, DELETE→archive, PUT variants (SKU/status/sortOrder, dup SKU → 409), `DuplicateVariantSkuException`, **IdempotencyMiddleware** (Redis, path `/api/v1/` + `/products`, 24h)
- **US-10** `DAI-255` Public storefront API — GET `/api/v1/products` (ES + Redis 30s), GET `/products/{slug}` (SQL + `variantMatrix`, 10m + ETag `W/"product-{id}-v{unix}"`), slug-check (20/min/IP); migration `010_AddCombinationCanonical`
- **US-11** `DAI-253` Middleware — **HostTenantRoutingMiddleware** (Redis `dcms:host:{host}`), TenantStoreAccess (Chain/Brand bỏ qua store match), rate limit tier theo tenant (`starter/pro/enterprise`), **AuditMiddleware + AuditLogBackgroundService + SqlAuditLogPersistence** → `AuditLogs`
- **US-12** `DAI-254` Inventory — POST `/stock/bulk` (≤100, adjust/reserve/release, partial success), GET `/stock/variants/{id}`, GET/POST `/warehouses`, **internal** `POST /internal/inventory/{check|reserve|release}` + header `X-Internal-Api-Key` (SHA-256 fixed-time; empty → 503); policies `inventory:read`/`write`; idempotency path `/stock`
- **US-13** `DAI-276` Umbraco 13 section `dCMSCatalog` + wizard Step 1–5 + review/publish (`DAI-281…287`); workflow preference localStorage

**M2 Orders — Order.Api (port TBD):**
- `DAI-637` Failed-order recovery — POST `/api/orders/{id}/retry-failure` + `/resolve-failure`; failure states `PaymentFailed|AuthFailed|AddressError|StockError|SystemError` trên `Orders.Status`; migrations `010_CreateOrderFailures.sql` + `011_AddOrderFailureContextColumns.sql`
- `DAI-651/652/653/654/656` **Refund Cases** (2026-04-23/24):
  - Persistence: migration `012_AddOrderRefundTrackingColumns.sql` (cột `RefundStatus`, `RefundRemark`, `RefundedAt` trên `Orders`) — **không** có bảng `RefundCases` riêng; view = Cancelled order + latest qualifying PaymentTransaction
  - Core: `RefundCaseReadModels.cs` (`RefundCaseDetail`, `RefundCasePage`), `RefundCaseStatusMaps.cs` (UI↔DB: `Pending|Processing|Success|Rejected` → `pending_refund|Processing|success|failed`), `RefundCasePaymentRules.cs`, `OrderListCursorCodec.cs`, `PaymentTransactionQueryStore.cs`
  - Routes (policy `OrderAccess` read / `OrderFailureManage` write): GET `/api/refund-cases` (keyset cursor, limit 1–100, status filter), PATCH `/api/refund-cases/{orderId}` (legacy), GET/PUT `/api/orders/{orderId}/refund-case` — envelope `{data, meta, error}` camelCase; Headers `X-Tenant-Id`, `X-Store-Id` bắt buộc
  - Update flow (`UpdateRefundCaseStatusAsync`): status canonical (4 labels) + remark ≤1000; mapping UI→DB via `UiPatchToDb`; `KeyNotFoundException` → 422 `ORDER_NOT_ELIGIBLE_FOR_REFUND`
  - SPA: `orders/api/refundCasesApi.ts`; `RefundCasesPage` + `RefundCaseDialogs` wired (mock `refundCasesMock.ts` deleted)

**M2 Promotions — `dCMS.Promotions.Api` (service mới):**
- `DAI-659` / `DAI-664` **Promo codes** (2026-04-24):
  - Migrations `022_CreatePromoCodes.sql` (`PromoCodes` + `PromoCodeWorkflowHistory`; unique `(TenantId,Code)`) + `023_ExtendPromoCodes.sql` (optional label, minSpend, startDate/endDate)
  - Core: `PromoCodeRow` (workflow states `draft→pending_approval→approved|rejected|archived`, valid types `percentage|fixed|free_shipping`, code regex shared với `CampaignRow`), `IPromoCodePersistence`, `SqlPromoCodePersistence`
  - Routes `/api/v1/tenants/{tenantId}/promo-codes` — GET list (status filter, page ≤200), GET by id, POST/PUT CRUD (UPPER code, 409 on dup), POST `/{id}/submit|archive|approve|reject` (approve/reject cần `CatalogApproval`; reject yêu cầu comment); tenant access middleware áp dụng
  - Wired vào `dCMS.Promotions.Api/Program.cs` cạnh campaigns (`MapCampaignRoutes` + `MapPromoCodeRoutes`)

**M2 Approvals (cross-service):**
- `DAI-658` Catalog `GET /api/v1/tenants/{tenantId}/products/pending-approvals` (policy `CatalogApproval`) + `PendingApprovalListRow`
- `DAI-660` Approval SPA — `approval/api/approvalApi.ts` gom products/campaigns/promo-codes qua `GATEWAY.catalog`/`gateway.promotions` (YARP); pages `ProductApprovalPage` / `CampaignApprovalPage` / `PromoCodeApprovalPage`; `ContentApprovalPage` + `content-approval-columns` đã xoá

**Access module (Umbraco backoffice) — §8 + DAI-668…670:**
- Umbraco migration plan `dCMS.Access` (`AccessModuleMigrationPlan`): `access-v1.0` → `access-v1.1` (`AccessModuleTenantsAndRolesMetaMigration`) tạo `dcms_tenants` + `dcms_roles_meta`, seed 10 default role aliases (`dcmsItAdministrator`, `dcmsSysAdministrator`, `dcmsEcommerceManager`, `dcmsTenantProductManager`, `dcmsTenantInventoryManager`, `dcmsOperations`, `dcmsFinance`, `dcmsBrandManager`, `dcmsProductUpload`, `dcmsGuest`; `isTenantRole` flag)
- PG side: migration `015_AccessModule.sql` (2026-04-20) + EF entities (`dCMS.Persistence.Ef`); `IPermissionService`/`PermissionService` (NPoco), `RequirePermissionAttribute`, `DcmsAccessComposer`, `DcmsUserController`, `DcmsRoleController`, `DcmsTenantController`
- `DAI-668` `DcmsTenantController` — `/umbraco/dcms/api/tenants` CRUD (code regex `^[A-Za-z0-9][A-Za-z0-9_-]{0,18}$`, DELETE = soft deactivate `active=0`, 409 on dup code)
- `DAI-669` **Tenants + Users** SPA (2026-04-24) — `tenantsApi.ts`, `usersApi.ts` (cookie session, `credentials:"include"`, Umbraco `BackOfficeAccess` policy, no Bearer), `TenantsPage` / `TenantFormPage` / `UsersPage` / `UserFormPage` / `ChangePasswordModal` wired. Extra form fields (branding, categories…) UI-only cho đến khi API mở rộng
- `DAI-670` **Roles** SPA (2026-04-24) — `rolesApi.ts` (`/umbraco/dcms/api/roles` GET/POST/PUT/DELETE + permissions GET + PUT per module); `RolesPage` / `RoleFormPage` / `ManageModulesPage` dùng `authToken` (demo `accessRoles` bỏ); module matrix = fixed template merged với API rows

**Active branch:** main

---

## Key Components

- `src/backend/dCMS.AspNetCore.Auth/` — JWT + RBAC policies (`CatalogRead|Write|Approval`, `OrderAccess|FailureManage`, `inventory:read|write`), tenant/store route filter
- `src/backend/dCMS.Core/` — Domain (Product SPU, `ProductService`, shared inventory exceptions, `PromoCodeRow`, `CampaignRow`, `PendingApprovalListRow`), persistence interfaces
- `src/backend/dCMS.Infrastructure/` — Dapper `SqlCatalogPersistence` / `SqlCampaignPersistence` / `SqlPromoCodePersistence`, outbox relay, search (ES indexer + alias bootstrap + `SqlProductSearchRepository` + Redis invalidator), SQL migrations `001`–`023`, `IdempotencyMiddleware`
- `src/backend/dCMS.Inventory.Core` / `Infrastructure` — `VariantStock`, `StockService`, `SqlStockPersistence`, migration `007`
- `src/backend/dCMS.Order.Core/Ordering/` — `CreateOrder`/`CancelOrder`, `RefundCase*`, `OrderListCursorCodec`, `RefundCasePaymentRules`, `RefundCaseStatusMaps`
- `src/backend/dCMS.Order.Infrastructure/Persistence/` — `OrderUnitOfWork`, `OrderQueryStore`, `PaymentTransactionQueryStore`, migrations `001`–`012`
- `src/backend/dCMS.Catalog.Api/` — minimal API (:5001), envelope `{data, meta, error}`, product search (ES) + pending-approvals
- `src/backend/dCMS.Inventory.Api/` — Inventory REST (:5002) + internal endpoints
- `src/backend/dCMS.Promotions.Api/` — Campaigns + Promo codes routes (tenant-scoped)
- `src/backend/dCMS.Order.Api/` — Orders REST + refund cases + failure recovery
- `src/backend/dCMS.Catalog.Worker/` — Outbox → RabbitMQ; MediatR index + Redis invalidation
- `src/backend/dCMS.Web/` — **Umbraco 13** host (`Umbraco.Cms` 13.13.1): sections `dCMSCatalog`, Access controllers (`DcmsUser|Role|Tenant`), SPA dist (`App_Plugins/DcmsV16/dist/{approval|estore|orders|reports|dashboard}-spa.{js,css,map}`), Access migrations (SQL Server)
- `src/backend/dCMS.Tests/` — xUnit + FluentAssertions + Moq + SkippableFact + Testcontainers (PG/ES); `Integration/Catalog/PendingApprovalsPersistenceIntegrationTests.cs`
- `src/backend/dCMS.Order.Tests/` — `Unit/OrderListCursorCodecTests.cs`, `Unit/RefundCasePaymentRulesTests.cs`, `Unit/RefundCaseStatusMapsTests.cs`, `Integration/RefundCasesApiIntegrationTests.cs`, `Integration/OrderServicePostgresIntegrationTests.cs`
- `src/backoffice/dcms-backoffice-spa/` — React SPA (build → `dCMS.Web/App_Plugins/DcmsV16/dist/`): `estore` (catalog mgmt + promotions + access), `approval` (products/campaigns/promo-codes), `orders` (orders + refund cases), `reports`, `dashboard`; `estore/api/gatewayConfig.ts` centralised gateway base URLs
- `infra/docker-compose.yml` + `infra/docker/*` — PostgreSQL, RabbitMQ, ES, Catalog/Inventory/Order/Promotions APIs, Worker, umbraco-web (:5000); profile `test` → `m1-domain-tests`

## In Progress

- Catalog API: `ProductAttributeValues` / dynamic attributes persistence
- Worker hardening: DbUp migrations, dequeue locking, DLQ dedupe; ES explicit mappings; `StockUpdated` debounce; `ProductDocument` fields (`brandId`, prices, snapshot version)
- Refund cases: enriching list with customer name/phone/email (hiện để empty trong DTO)

## Recent Decisions

Xem `.claude/memory/decisions.md`

---

_Keep this file under 200 lines. Archive old context with compress-context skill._
