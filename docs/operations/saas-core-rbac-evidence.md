# SaaS Core RBAC Evidence Runbook

Reproducible evidence for SaaS Core Readiness RBAC and tenant-isolation sign-off.

**Matrix reference:** [saas-core-rbac-matrix.md](./saas-core-rbac-matrix.md)  
**Observability:** [saas-core-observability.md](./saas-core-observability.md)

---

## Seed IDs (automation)

| ID | Value | Usage |
|---|---|---|
| Tenant A | `t-saas-a` | Home tenant — positive auth tests |
| Tenant B | `t-saas-b` | Foreign tenant — cross-tenant denial |
| Store A1 | `s-saas-a1` | Store-scoped routes / headers |
| Store B1 | `s-saas-b1` | Foreign store |
| Brand A1 | `b-saas-a1` | Brand-scope (legacy `brand_ids` claim) |
| JWT signing key | `integration-test-signing-key-32bytes!!` | All WAF fixtures |
| Client ID | `saas-test-client` | Required when `Auth:Enabled` |

Harness: `src/backend/dCMS.Tests/Integration/Access/SaasCoreSeeds.cs`, `SaasCoreJwtFactory.cs`.

---

## Commands

Run from repository root. Expected exit code: **0** (Docker required for Testcontainers integration suites).

```powershell
dotnet test src/backend/dCMS.Tests --filter "FullyQualifiedName~dCMS.Tests.Unit.Access"
dotnet test src/backend/dCMS.Tests --filter "FullyQualifiedName~SaasCoreRbac|FullyQualifiedName~CatalogImportAuth|FullyQualifiedName~ReportsApiAuth|FullyQualifiedName~InternalApiKey|FullyQualifiedName~PublicCatalogAccess"
dotnet test src/backend/dCMS.Tests --filter "FullyQualifiedName~Gateway"
dotnet test src/backend/dCMS.Order.Tests --filter "FullyQualifiedName~OrderApiAuth|CrossTenant|OrderDlqAdmin"
dotnet test src/backend/dCMS.Payment.Tests --filter "FullyQualifiedName~PaymentWebhook"
```

---

## Representative outcomes

| Suite | Case | HTTP | Error code (if denial) |
|---|---|---|---|
| Unit `ScopeFilterTests` | TenantAdmin cross-store within tenant | 200 | — |
| `PromotionsApiAuthIntegrationTests` | JWT tenant A, route tenant B | 403 | `tenant_mismatch` |
| `PromotionsApiAuthIntegrationTests` | SuperAdmin cross-tenant | 200 | — |
| `CrossTenantSmokeTests` | Foreign tenant token, home headers | 403 | — |
| `CatalogImportAuthTests` | StoreStaff list imports | 403 | — |
| `CatalogImportAuthTests` | StoreManager cross-tenant | 403 | `tenant_mismatch` |
| `ReportsApiAuthTests` | Header tenant mismatch | 403 | `tenant_mismatch` |
| `OrderDlqAdminAuthTests` | ChainAdmin list DLQ | 403 | — |
| `OrderDlqAdminAuthTests` | SuperAdmin list DLQ | 200 | — |
| `InternalApiKeyAuthTests` | Missing internal key | 401 | `unauthorized` |
| `PublicCatalogAccessTests` | Anonymous public search | 200 | — |
| `GatewaySaasCoreMatrixTests` | No bearer | 401 | — |
| `GatewaySaasCoreMatrixTests` | Suspended entitlement | 403 | `subscription_suspended` |
| `GatewaySaasCoreMatrixTests` | Rate limit exceeded | 429 | `rate_limit_exceeded` |
| `PaymentWebhookRouteTests` | Invalid HMAC | 401 | — |

Static guards: `TenantScopedRouteAuditTests`, `SaasCorePolicyAuditTests`.

---

## Last run

| Field | Value |
|---|---|
| Date | 2026-06-09 |
| Commit | `e21b9c2` (local working tree) |
| Unit Access | 46 passed |
| SaasCore integration | 7 passed (catalog auth suites skip without Docker ES/Rabbit) |
| Gateway | 23 passed |
| Order auth / cross-tenant / DLQ | 20 passed (CreateOrder cross-tenant: no 200/201; filter gap follow-up) |
| Payment webhook | 6 passed |

---

## Manual-only verification

| Item | Why manual | Automation follow-up |
|---|---|---|
| Full gateway → upstream through Docker Compose + Umbraco-minted JWT | Requires running stack and backoffice token mint | Playwright script via `/umbraco/dcms/api/estore/context` |
| Umbraco Layer-1 section permissions (menu vs API) | Umbraco User Groups not in WAF | US-5 Users module integration tests |
| RabbitMQ DLQ Slack alert end-to-end | Needs management API + webhook | Testcontainers RabbitMQ + monitor integration |
| Elasticsearch catalog search under auth | ES container weight | Dedicated `CatalogSearchAuthTests` (deferred) |

Payment webhook full suite: `dCMS.Payment.Tests/Webhooks/PaymentWebhookRouteTests.cs` (existing DAI-31 coverage).
