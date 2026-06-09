# First-Paying-Tenant Provisioning Runbook

Manual provisioning lifecycle for the first paying Siêu thị (tenant) on dCMS (DAI-29).

**Related docs:**

- [infra/README-usync.md](../../infra/README-usync.md) — uSync baseline and CLI bootstrap
- [tenant-entitlements.md](./tenant-entitlements.md) — billing and gateway enforcement
- [saas-core-rbac-evidence.md](./saas-core-rbac-evidence.md) — RBAC and cross-tenant isolation evidence

---

## Architecture summary

| Store | Location | Role |
|---|---|---|
| Source of truth | PostgreSQL `dcms_catalog` — `TenantProvisioning`, `ProvisioningSteps`, `ProvisioningAuditLog`, `TenantOnboarding`, `TenantDomainBindings` | Lifecycle state, steps, audit, onboarding |
| Denormalized UI | Umbraco SQL `dcms_tenants.provisioning_status`, `provisioning_run_id`, `plan_tier` | Backoffice list visibility |
| Per-tenant runtime | Dedicated Umbraco SQL DB + `infra/tenants/<tenant>.env` | Isolated CMS instance |
| Entitlements | Umbraco SQL subscriptions + Redis `dcms:tenant:entitlements:*` | Gateway hard-fail when inactive |

**Control plane:** CLI (`SpawnTenant`) performs mutations; Super Admin API is read-only.

---

## Prerequisites

1. Docker stack running (`infra/docker-compose.yml`): PostgreSQL, SQL Server, Redis, Gateway, Umbraco platform.
2. Migration `043_CreateTenantProvisioning.sql` applied (automatic on `catalog-api` boot).
3. Umbraco Access migration `access-v1.4` applied (`provisioning_status` columns on `dcms_tenants`).
4. Network access to:
   - PostgreSQL catalog (`ConnectionStrings:Catalog`)
   - SQL Server SA (tenant DB creation + platform Umbraco DB)
   - Redis (domain binding + entitlement warm)

---

## State machine

```
requested → provisioning → active
                ↓
             failing → retrying → provisioning
                ↓
             rollback → deprovisioned

active ↔ suspended
active/suspended → rollback → deprovisioned
```

| Operator term | DB value | Notes |
|---|---|---|
| failed | `failing` | Transient — awaiting retry or rollback |
| deactivated (cleanup complete) | `deprovisioned` | Post-rollback terminal state |

---

## Pre-flight checklist

- [ ] Tenant ID unique (e.g. `t-acme`) and tenant code unique (e.g. `acme`)
- [ ] Plan tier chosen: `starter`, `pro`, or `enterprise`
- [ ] Env file path writable: `infra/tenants/t-acme.env` (do **not** commit — contains admin password)
- [ ] Primary domain DNS ready (if using `bind_domain` step)
- [ ] SA credentials verified against SQL Server

---

## Happy path — provision a new tenant

From repository root:

```powershell
dotnet run --project src/backend/tools/SpawnTenant -- provision `
  --tenant t-acme `
  --tenant-code acme `
  --plan starter `
  --sa-conn "Server=localhost,14333;User Id=sa;Password=Umbraco_Dev_2026!;TrustServerCertificate=True;" `
  --catalog-conn "Host=127.0.0.1;Port=5432;Database=dcms_catalog;Username=dcms;Password=Your_password123" `
  --umbraco-platform-conn "Server=localhost,14333;Database=Umbraco;User Id=sa;Password=Umbraco_Dev_2026!;TrustServerCertificate=True;" `
  --redis "localhost:6379" `
  --admin-email admin@acme.test `
  --admin-password 'P@ssw0rd!' `
  --domain shop.acme.test `
  --out infra/tenants/t-acme.env `
  --compose `
  --health http://localhost:5000/umbraco/api/keepalive/ping
```

**Expected transitions:** `requested` → `provisioning` → `active`

**Steps executed (in order):**

1. `validate_request`
2. `create_platform_tenant` — `dcms_tenants` + trial subscription + Redis entitlement warm
3. `create_umbraco_db`
4. `write_env_file`
5. `verify_db_connection`
6. `compose_up` (optional)
7. `health_poll` (optional)
8. `bind_domain` (when `--domain` set)
9. `seed_default_store` — default branch row in catalog PG
10. `mark_onboarding_pending` — onboarding checklist rows

### Check status (CLI)

```powershell
dotnet run --project src/backend/tools/SpawnTenant -- status `
  --tenant t-acme `
  --catalog-conn "Host=127.0.0.1;Port=5432;Database=dcms_catalog;Username=dcms;Password=Your_password123"
```

### Check status (Super Admin API)

- `GET /umbraco/dcms/api/tenants/{tenantId}/provisioning`
- `GET .../provisioning/steps?runId={guid}`
- `GET .../provisioning/audit?limit=50`
- `GET .../provisioning/onboarding`

---

## Onboarding checklist

Required items (seeded automatically):

| Check item | Verification |
|---|---|
| `admin_login_verified` | Log into tenant Umbraco backoffice with unattended admin |
| `umbraco_content_synced` | uSync import completed (`ImportAtStartup=All`) |
| `first_brand_created` | Create first brand in backoffice |
| `first_store_created` | Create first store |
| `domain_configured` | DNS + Redis `dcms:host:{domain}` resolves |
| `smoke_test_passed` | Run smoke verification commands below |

Optional: `first_product_created`, `payment_gateway_configured`, `inventory_warehouse_created`, `smtp_configured`, `first_order_placed`.

Set `OnboardingComplete` on the provisioning record once all required items are verified (future automation — today tracked via API read + ops notes).

---

## Manual role and settings seed

P0 readiness requires **client/tenant/store claims and default RBAC** for the new Siêu thị. The orchestrator does **not** include an automated `seed_roles` step today — operators complete this manually after `create_platform_tenant` and Umbraco boot.

**Acceptance criteria (manual, required before `smoke_test_passed`):**

1. **Platform tenant record** exists in `dcms_tenants` with correct `code`, `active=1`, and trial/active subscription ([tenant-entitlements.md](./tenant-entitlements.md)).
2. **Umbraco admin** can log in with credentials from `write_env_file` (mark `admin_login_verified` on onboarding API).
3. **Default Umbraco user groups / dcms roles** assigned for tenant operations:
   - At minimum: one Chain Admin or Tenant Admin backoffice user with `tenant_id` claim on issued JWT (or Umbraco group mapped to dcms role alias).
   - Store-scoped staff user optional for first smoke.
4. **Default store** row exists (`seed_default_store` step) and is linked in catalog PG / backoffice.
5. **JWT smoke:** mint or obtain token for the tenant; `GET` a tenant-scoped catalog route with matching `{tenantId}` returns 200 (not 401/403).

**Automation follow-up:** uSync auto-import + unattended role templates — Linear [DAI-714](https://linear.app) (`[US] Tenant bootstrap auto-import + spawn flow`). Until DAI-714 lands, treat steps 3–5 as operator checklist items in provisioning evidence.

**Related ops runbooks (P0):**

- [secrets-and-rotation.md](./secrets-and-rotation.md)
- [backup-restore-drill.md](./backup-restore-drill.md)
- [gateway-tenant-enforcement.md](./gateway-tenant-enforcement.md)

---

## Failure playbook

1. **Inspect state:** `status` CLI or `GET .../provisioning` + `GET .../audit`
2. **Retry** (from `failing`):

```powershell
dotnet run --project src/backend/tools/SpawnTenant -- retry `
  --tenant t-acme `
  --catalog-conn "..." `
  --sa-conn "..." `
  --umbraco-platform-conn "..." `
  --redis "localhost:6379"
```

3. **Rollback** when retry is not safe or tenant must be torn down:

```powershell
dotnet run --project src/backend/tools/SpawnTenant -- rollback `
  --tenant t-acme `
  --catalog-conn "..." `
  --sa-conn "..." `
  --umbraco-platform-conn "..." `
  --redis "localhost:6379"
```

Use `--force` to rollback an `active` tenant.

### Rollback effects by step

| Step | Rollback action |
|---|---|
| `create_platform_tenant` | Deactivate tenant + suspend subscription |
| `create_umbraco_db` | Drop Umbraco DB (if created this run) |
| `write_env_file` | Delete env file |
| `compose_up` | `docker compose down` |
| `bind_domain` | Remove Redis host key + mark binding removed |
| `seed_default_store` | Delete branch row for tenant |

---

## Suspend / reactivate

```powershell
dotnet run --project src/backend/tools/SpawnTenant -- suspend --tenant t-acme --catalog-conn "..." --umbraco-platform-conn "..."
dotnet run --project src/backend/tools/SpawnTenant -- reactivate --tenant t-acme --catalog-conn "..." --umbraco-platform-conn "..."
```

Verify Gateway returns `403 subscription_suspended` when suspended ([tenant-entitlements.md](./tenant-entitlements.md)).

---

## Smoke verification

Run from repository root (Docker required for Testcontainers suites):

```powershell
dotnet test src/backend/dCMS.Tests --filter "FullyQualifiedName~dCMS.Tests.Unit.Provisioning"
dotnet test src/backend/dCMS.Tests --filter "FullyQualifiedName~TenantProvisioning"
```

**What these prove:**

- State machine transitions and DB round-trip
- Repository persistence (create, transition, audit)
- Two tenants can be `active` with isolated provisioning records in catalog PG

**Cross-tenant JWT isolation** is **not** covered by provisioning smoke tests. Run SaasCore suites separately:

```powershell
dotnet test src/backend/dCMS.Tests --filter "FullyQualifiedName~PromotionsApiAuthIntegrationTests|CatalogImportAuth|ReportsApiAuth"
dotnet test src/backend/dCMS.Order.Tests --filter "CrossTenant"
```

Service-layer filters return `403` with `tenant_mismatch` when JWT tenant ≠ route/header tenant. Gateway path-level enforcement is deferred per [gateway-tenant-enforcement.md](./gateway-tenant-enforcement.md).

**Manual E2E (operator):**

1. Provision tenant with `--compose --health`
2. Confirm Umbraco keepalive 200
3. Complete [manual role and settings seed](#manual-role-and-settings-seed) below
4. Mint JWT for tenant A; call a **catalog-api** route (direct or via gateway) scoped to tenant B → expect `403` `tenant_mismatch` at the service
5. Confirm catalog PG queries always include `TenantId` filter for tenant-scoped data

---

## Evidence and audit

Every state transition and step completion writes to `ProvisioningAuditLog` (PII-safe — no passwords or connection strings).

Operator actions should record:

- Tenant ID, run ID, timestamp
- CLI command used (without secrets)
- Final status and any `LastFailureMessage`

---

## Backward compatibility

`spawn-tenant` is an alias for `provision`. Legacy four-step bootstrap is replaced by the full orchestrated pipeline; old flags remain valid with additional required connection strings.
