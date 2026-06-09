# Gateway tenant enforcement (MVP deferral ADR)

**Decision:** For the first-paying-tenant MVP, tenant/route mismatch is enforced at **microservice** layer, not at the API gateway path.

**Status:** Accepted (DAI-25-P0-03b option B)  
**Date:** 2026-06-09  
**P0 source:** dai-29-comment.md — "block client/tenant/store mismatches before requests reach services"

---

## Context

The gateway today performs:

1. **JWT validation** and short-lived internal token re-mint — [`GatewayAuthMiddleware`](../../src/backend/dCMS.Gateway/GatewayAuthMiddleware.cs)
2. **Subscription / operational entitlement** hard-fail — [`GatewayTenantEntitlementMiddleware`](../../src/backend/dCMS.Gateway/GatewayTenantEntitlementMiddleware.cs)

It does **not** compare JWT `tenant_id` to `{tenantId}` in the proxied URL path before forwarding to upstream services.

---

## Decision

MVP accepts **service-layer enforcement** via:

- `WithTenantAccess` / `WithTenantStoreAccess` / `WithTenantStoreHeaderAccess` endpoint filters
- Policy checks (`DcmsPolicies.*`) on sensitive routes

Cross-tenant denial is tested at service WAF/integration level:

- `PromotionsApiAuthIntegrationTests` — 403 `tenant_mismatch`
- `CatalogImportAuthTests`, `ReportsApiAuthTests`, `CrossTenantSmokeTests`
- Static guard: `TenantScopedRouteAuditTests`

Gateway integration tests use dead upstream → **502** to prove auth/entitlement pass-through, not path-level tenant match ([saas-core-rbac-matrix.md](./saas-core-rbac-matrix.md)).

---

## Rationale

- YARP routes are heterogeneous (header-scoped order API vs path-scoped catalog); a single gateway path rule risks false positives without per-cluster rules.
- Service filters already return consistent `403` + `tenant_mismatch` with observability `failureReason`.
- Edge path validation is defense-in-depth, not the only control — acceptable for single-tenant MVP with manual provisioning.

---

## Risks

- Misconfigured upstream route **without** tenant filter could leak until caught by audit tests — mitigated by `TenantScopedRouteAuditTests` + `SaasCorePolicyAuditTests` in CI.
- Penetration test may flag "defense in depth" — document this ADR in evidence pack.

---

## Phase 2 follow-up

Implement gateway middleware: for routes matching `/tenants/{tenantId}/`, compare claim to path segment; SuperAdmin bypass; integration test `GatewayTenantPathMismatchTests` (see [dai-25-child-issues.md](../../.multica/dai-25-child-issues.md)).

---

## Verification (MVP)

```powershell
dotnet test src/backend/dCMS.Tests --filter "FullyQualifiedName~dCMS.Tests.Unit.Access"
dotnet test src/backend/dCMS.Tests --filter "FullyQualifiedName~PromotionsApiAuthIntegrationTests|CatalogImportAuth|ReportsApiAuth"
dotnet test src/backend/dCMS.Order.Tests --filter "CrossTenant"
```
