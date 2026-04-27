# Fulfillment API — integration (DAI-612 / DAI-613 / DAI-614)

**Date:** 2026-04-23  
**Status:** Implemented

## Scope

- **DAI-612:** `dCMS.Fulfillment.Api` — domain + REST on PostgreSQL (`dcms_catalog`), migration `021_CreateFulfillment.sql`.
- **DAI-613:** eStore SPA — `src/estore/api/fulfillmentApi.ts`, `EStoreApp` loads/saves via `GATEWAY.fulfillment` when `tenantId` is set.
- **DAI-614:** Ops/docs — this note, `dCMS.Fulfillment.Api.http`, `infra/README.md` ports & health.

## URLs

| Layer | Base |
|--------|------|
| Service (direct) | `/api/v1/tenants/{tenantId}/fulfillment/...` |
| Gateway (YARP) | `/gateway/v1/fulfillment/tenants/{tenantId}/fulfillment/...` → strips gateway prefix, forwards as `/api/v1/...` |

Local ports: **5006** (Fulfillment.Api `dotnet run` and Docker host map), **5100** gateway.

## Resources

| Resource | Routes |
|----------|--------|
| Groupings | `GET/POST …/groupings`, `GET/PUT/DELETE …/groupings/{id}` |
| Slots | `GET/POST …/groupings/{groupingId}/slots`, `GET/PUT/DELETE …/groupings/{groupingId}/slots/{slotId}` |
| Collection locations | `GET/POST …/collection-locations`, `GET/PUT/DELETE …/collection-locations/{id}` |
| Logistic partners | `GET/POST …/logistic-partners`, `GET/PUT/DELETE …/logistic-partners/{id}` |
| Settings (JSON) | `GET/PUT …/settings` (`predefinedFields`, `dynamicFields`, `stockLocations`) |

## Auth & RBAC

Same policies as catalog eStore management: **catalog:read** / **catalog:write**; tenant route filter matches JWT `tenant_id` to `{tenantId}` (SuperAdmin bypass).

## Data

- Tables live in **catalog** DB (same database as campaigns).
- **Fulfillment.Api** runs `CatalogDbMigrationHostedService` (same as Catalog.Api / Catalog.Worker) so DbUp applies **`021_CreateFulfillment.sql`** on startup. **Promotions.Api** does not run catalog DbUp; compose starts **catalog-api** (or fulfillment-api) so `020` / `021` are applied before dependent services.

## Tests

- `dCMS.Tests/Integration/Fulfillment/FulfillmentApiIntegrationTests.cs` (Testcontainers Postgres).

## SPA

- Config: `src/estore/api/gatewayConfig.ts` → `fulfillment: "/gateway/v1/fulfillment"`.
- Dev proxy: Vite eStore config proxies `/gateway` → gateway (e.g. `localhost:5100`).
