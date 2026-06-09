# SaaS Core RBAC Matrix

Human-readable role × endpoint matrix for SaaS Core Readiness sign-off. Automated coverage lives under `dCMS.Tests/Integration/Access/` and service-specific auth suites.

**Canonical seed IDs (automation):**

| Constant | Value |
|---|---|
| Tenant A | `t-saas-a` |
| Tenant B | `t-saas-b` |
| Store A1 | `s-saas-a1` |
| Store B1 | `s-saas-b1` |
| Brand A1 | `b-saas-a1` |

**Callers (columns):** Anonymous, Customer, StoreStaff, StoreManager, BrandManager, ChainAdmin, TenantAdmin, SuperAdmin.

**Legend:** ✓ = expected success (200/204), ✗401 = unauthorized, ✗403 = forbidden (policy or tenant/store scope), 429 = rate limit, 502 = gateway passed auth (upstream down in tests).

---

## Gateway proxy

Representative: `GET /gateway/v1/catalog/tenants/{tenantId}/brands`

| Caller | Expected | Notes |
|---|---|---|
| Anonymous | ✗401 | JWT required when `Auth:Enabled` |
| Customer–TenantAdmin | ✓502* | Valid JWT passes auth + entitlement when active |
| SuperAdmin | ✓502* | Bypasses entitlement middleware when snapshot missing |
| Any (rate limit) | 429 | After partition limit exceeded |
| ChainAdmin (suspended tenant) | ✗403 | `subscription_suspended` / `subscription_cancelled` / `trial_expired` |

\*Integration tests use dead upstream → 502 proves middleware pass-through.

---

## Catalog read

`GET /api/v1/tenants/{t}/stores/{s}/products` — policy `catalog:read` + `WithTenantStoreAccess`

| Caller | Home tenant/store | Cross-tenant route | Cross-store (constrained token) |
|---|---|---|---|
| Anonymous | ✗401 | ✗401 | ✗401 |
| Customer | ✗403 | ✗403 | ✗403 |
| StoreStaff | ✓200 | ✗403 `tenant_mismatch` | ✗403 `store_mismatch` |
| StoreManager | ✓200 | ✗403 | ✗403 |
| BrandManager | ✓200 (any store in tenant) | ✗403 | ✗403 if `store_ids` excludes header store |
| ChainAdmin | ✓200 (tenant-wide store bypass) | ✗403 | ✗403 if `store_ids` set |
| TenantAdmin | ✗403 | ✗403 | ✗403 — not in `catalog:read` policy today |
| SuperAdmin | ✓200 | ✓200 | ✓200 |

---

## Catalog write

`POST /api/v1/tenants/{t}/stores/{s}/products` — policy `catalog:write` + store filter + quota guard

| Caller | Home tenant | Cross-tenant |
|---|---|---|
| Anonymous | ✗401 | ✗401 |
| Customer / StoreStaff | ✗403 | ✗403 |
| StoreManager+ | ✓200/201* | ✗403 |
| SuperAdmin | ✓* | ✓* |

\*Subject to entitlement product quota when billing enforcement enabled.

---

## Catalog approval

`POST .../products/{id}/approve` — policy `catalog:approval`

| Caller | Expected |
|---|---|
| StoreStaff / StoreManager | ✗403 |
| BrandManager / ChainAdmin / SuperAdmin | ✓ (within tenant scope) |

---

## Bulk import

`GET|POST /api/v1/tenants/{t}/imports` — policy `catalog:import` + `WithTenantAccess`

| Caller | Expected |
|---|---|
| Anonymous | ✗401 |
| StoreStaff | ✗403 |
| StoreManager | ✓200 (list) / ✓201 (upload) |
| Token tenant A, route tenant B | ✗403 `tenant_mismatch` |

---

## Inventory read/write

`GET|POST /api/v1/tenants/{t}/stores/{s}/stock` — `inventory:read` / `inventory:write` + store filter

| Caller | Read | Write |
|---|---|---|
| Anonymous | ✗401 | ✗401 |
| StoreStaff | ✓ | ✓ |
| Customer | ✗403 | ✗403 |
| Cross-tenant | ✗403 | ✗403 |

---

## Orders

`GET|POST /api/orders` — policy `order:access` + `X-Tenant-Id` / `X-Store-Id` header filter

| Caller | Expected |
|---|---|
| Anonymous | ✗401 |
| Customer (own order) | ✓200 |
| Customer (other customer) | ✗403 |
| StoreStaff / StoreManager (staff) | ✓200 |
| Token tenant B, headers tenant A | ✗403 |
| SuperAdmin | ✓200 (handler scope) |

---

## Reports

`GET /api/v1/reports/sales` — policy `order:access` + `WithTenantStoreHeaderAccess`

| Caller | Matching headers | Tenant mismatch |
|---|---|---|
| Anonymous | ✗401 | ✗401 |
| ChainAdmin | ✓200* | ✗403 |
| Token A, header B | ✗403 | ✗403 |

\*Requires valid `dateFrom`/`dateTo` query params; auth tests assert status before handler validation where applicable.

---

## Promotions

`GET /api/v1/tenants/{t}/campaigns` — `catalog:read` + `WithTenantAccess`

| Caller | Home tenant | Cross-tenant |
|---|---|---|
| Anonymous | ✗401 | ✗401 |
| ChainAdmin | ✓200 | ✗403 |
| SuperAdmin | ✓200 | ✓200 |

---

## Order DLQ admin

`GET /api/v1/admin/orders/dlq` — policy `order:dlq-admin` (SuperAdmin only)

| Caller | Expected |
|---|---|
| Anonymous | ✗401 |
| ChainAdmin / TenantAdmin | ✗403 |
| SuperAdmin | ✓200 |

---

## Internal S2S

`GET /internal/catalog/tenants/{t}/products/{id}/exists` — API key filter (not JWT)

| Caller | Expected |
|---|---|
| No `X-Internal-Api-Key` | ✗401 |
| Wrong key | ✗403 |
| Valid key | ✓200 |

Promotions internal routes mirror this at `/internal/promotions/...`.

---

## Payment webhook

`POST /api/webhooks/payment/{provider}` — HMAC signature (anonymous + secret)

| Caller | Expected |
|---|---|
| Invalid signature | ✗401 |
| Valid signature | ✓200 |

See `dCMS.Payment.Tests/Webhooks/PaymentWebhookRouteTests.cs`.

---

## Public catalog search

`GET /api/v1/products?tenantId=&storeId=` — anonymous by design

| Caller | Expected |
|---|---|
| Anonymous | ✓200 (with valid query params) |
| Protected catalog write without token | ✗401 |

---

## TenantAdmin store bypass (DAI-752)

`TenantAdmin` matches `ChainAdmin` / `BrandManager` for **cross-store within tenant** on route and header filters (`TenantStoreAccessEndpointFilter`, `TenantStoreHeaderAccessEndpointFilter`). Unit coverage: `ScopeFilterTests.TenantStore_route_filter_passes_tenant_admin_cross_store_within_tenant`.

**Policy gap (documented):** `TenantAdmin` is not included in `catalog:read` / `catalog:write` policies — HQ tenant admin JWT cannot list catalog products until policies are extended (future US-5 work).

---

## Related docs

- [saas-core-rbac-evidence.md](./saas-core-rbac-evidence.md) — commands and last-run results
- [saas-core-observability.md](./saas-core-observability.md) — metrics and alerts
