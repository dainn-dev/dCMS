# Tenant entitlements and billing (MVP)

Manual invoicing foundation for the first paying tenant (DAI-29). Self-service subscription UI and payment automation are deferred.

## Source of truth

| Layer | Store | Role |
|---|---|---|
| Authoritative | Umbraco SQL Server — `dcms_plans`, `dcms_tenant_subscriptions`, `dcms_tenants.active` | Subscription state, plan quotas, manual invoice metadata |
| Replica | Redis — `dcms:tenant:entitlements:{tenantId}:v{version}` | Fast read path for Gateway and microservices |
| Rate limit tier | Redis — `dcms:tenant:plan:{tenantId}` | Existing US-11 plan tier for `TenantPlanRateLimit` |

**No card/PAN or payment instrument data is stored in dCMS.** Manual invoice fields are `manual_invoice_status`, `invoice_reference`, and `invoice_notes` only.

## Data model

### Subscription states

| State | Operational | Notes |
|---|---|---|
| `trial` | Yes until `trial_ends_at` | Default on tenant create (14 days, configurable) |
| `active` | Yes | Paid / manually activated |
| `suspended` | No | Hard-fail at Gateway (`403 subscription_suspended`) |
| `cancelled` | No | Hard-fail at Gateway (`403 subscription_cancelled`) |

Additionally, `dcms_tenants.active = 0` yields `403 tenant_inactive`.

Trial expiry is evaluated on each request (`403 trial_expired`).

### Manual invoice status

Informational for ops: `none`, `draft`, `sent`, `paid`, `overdue`, `waived`. MVP does **not** auto-suspend on `overdue` — ops suspend manually when needed.

### Plans (seeded)

| Code | Max brands | Max active products | Extra features |
|---|---|---|---|
| `starter` | 2 | 500 | catalog + orders |
| `pro` | 10 | 5000 | + `promotions.write` |
| `enterprise` | 100 | 50000 | + reports, fulfillment |

## Lookup strategy

JWT claims are **not** the entitlement source. Every enforcement path reads a **live snapshot** from cache (rebuilt from SQL on admin writes).

```
Admin mutation (dCMS.Web)
  → SQL upsert
  → version bump (Redis INCR dcms:tenant:entitlements:ver:{tenantId})
  → publish snapshot JSON at dcms:tenant:entitlements:{tenantId}:v{version}
  → sync dcms:tenant:plan:{tenantId}

Gateway / Catalog.Api request
  → read version from Redis
  → L1 memory (30s) then Redis payload
  → enforce IsOperational / features / quotas
```

### Cache / fallback

- **L1**: `IMemoryCache`, ~30s TTL, key includes version suffix.
- **L2**: Redis, ~15m TTL, refreshed on every admin publish.
- **Revocation**: `BumpVersionAsync` increments version and clears L1 version cache; old payload keys are orphaned and never read.
- **Gateway miss**: fail closed → `403 entitlement_unavailable` (warm cache on tenant create / subscription API publish).
- **Redis unavailable**: store returns `null`; Gateway fails closed; services fail closed via `IEntitlementGuard`.
- **Rebuild from SQL**: only `dCMS.Web` has Umbraco DB access — call subscription admin API or tenant create/update to republish.

Super Admin JWT role bypasses Gateway entitlement middleware (recovery/ops).

## Feature keys (plan-derived)

| Feature | Enforced on |
|---|---|
| `catalog.write` | Catalog `POST .../products` (+ `max_active_products` quota) |
| `orders.write` | Order `POST /api/orders`, `POST /api/v1/checkout` |
| `promotions.write` | Promotions `POST .../campaigns`, `POST .../promo-codes` |
| `reports.read` | Reports `GET /api/v1/reports/*` |

Per-tenant overrides merge at publish time via `TenantFeatureOverrides` (admin API under `/umbraco/dcms/api/tenants/{id}/feature-overrides`).

## Enforcement points

1. **Gateway** — `GatewayTenantEntitlementMiddleware` after JWT validation: blocks non-operational tenants.
2. **Gateway** — `GatewayHostRoutingMiddleware`: custom host → `X-Tenant-Id` / `X-Store-Id` on `/storefront/v1` (fail-closed for unknown hosts).
3. **Catalog API** — `POST .../products`: `catalog.write` + `max_active_products` quota.
4. **Order API** — create order + checkout: `orders.write`.
5. **Promotions API** — campaign/promo create: `promotions.write`.
6. **Reports API** — all report GET routes: `reports.read`.

Error envelope: `{ "data": null, "meta": null, "error": { "code": "<code>", "message": "..." } }`.

Codes: `tenant_inactive`, `subscription_suspended`, `subscription_cancelled`, `trial_expired`, `quota_exceeded`, `entitlement_denied`, `entitlement_unavailable`.

## Admin API (Super Admin / platform admin groups)

Base: `/umbraco/dcms/api/tenants/{tenantId}/subscription`

| Method | Path | Action |
|---|---|---|
| GET | `/` | Read subscription + plan quotas |
| PUT | `/` | Update manual invoice fields |
| POST | `/activate` | Activate or re-activate |
| POST | `/suspend` | Suspend (optional reason body) |
| POST | `/cancel` | Cancel (optional reason body) |
| PUT | `/plan` | Change plan (`planId`, optional `pendingPlanId`) |

Requires Umbraco backoffice auth and membership in `dcmsItAdministrator` or `dcmsSysAdministrator`.

## Manual invoicing workflow (ops)

1. Create tenant → trial subscription seeded automatically.
2. Send invoice offline; record `manual_invoice_status` + `invoice_reference` via PUT.
3. On payment, POST `/activate` and set invoice status to `paid`.
4. On non-payment, POST `/suspend` (optionally set `overdue` on invoice status for tracking).
5. On churn, POST `/cancel`.

## Configuration

```json
{
  "Dcms": {
    "Billing": {
      "DefaultTrialDays": 14,
      "DefaultPlan": "Starter",
      "EntitlementCache": {
        "RedisTtl": "00:15:00",
        "MemoryTtl": "00:00:30"
      }
    }
  },
  "ConnectionStrings": {
    "Redis": "127.0.0.1:6379,abortConnect=false"
  }
}
```

## Provisioning note

`SpawnTenant` creates the Umbraco DB and env file. After Umbraco boot, create the platform tenant via backoffice API (or ensure `dcms_tenants` + subscription rows exist) so Redis cache is warmed before Gateway auth is enabled in production.
