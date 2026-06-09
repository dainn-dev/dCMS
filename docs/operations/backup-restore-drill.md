# Backup and restore drill (first-paying-tenant MVP)

Ops runbook for P0 recoverability (DAI-29 / DAI-25-P0-09).

**Related:** [first-paying-tenant-provisioning.md](./first-paying-tenant-provisioning.md), [tenant-entitlements.md](./tenant-entitlements.md), [incident-response.md](./incident-response.md)

**Automation:** [`infra/scripts/backup-nightly.ps1`](../../infra/scripts/backup-nightly.ps1), [`restore-drill.ps1`](../../infra/scripts/restore-drill.ps1) — see [infra/README.md](../../infra/README.md).

**Evidence:** Completed drills → `docs/operations/evidence/restore-drill-YYYY-MM-DD.md` (do not overwrite the template table in this file).

---

## Backup scope

| Data store | Contents | Backup method (MVP) | RPO target |
|---|---|---|---|
| Umbraco SQL — platform | `dcms_tenants`, subscriptions, roles, users | Nightly full backup (`.bak` / managed backup) | 24h |
| Umbraco SQL — per tenant | CMS content, tenant Umbraco DB | Per-tenant nightly full backup | 24h |
| PostgreSQL `dcms_catalog` | Catalog, provisioning, brands | `pg_dump` nightly | 24h |
| PostgreSQL order/inventory/payment DBs | Orders, payments, stock | `pg_dump` nightly per service DB | 24h |
| Redis | Entitlements cache, rate limits, idempotency | **Not backed up** — rebuild from SQL | N/A |

Point-in-time restore (PITR) is deferred unless retention policy requires it (see dai-29-comment open decision).

---

## Scheduled backup (Docker Compose dev/staging)

Preferred: run from repository root:

```powershell
.\infra\scripts\backup-nightly.ps1
```

```bash
./infra/scripts/backup-nightly.sh
```

Manual equivalents (adjust paths and credentials):

```powershell
pg_dump -h 127.0.0.1 -U dcms -d dcms_catalog -Fc -f "backups/dcms_catalog_$(Get-Date -Format yyyyMMdd).dump"
# Umbraco: use backup-nightly.ps1 (docker exec + docker cp) — host sqlcmd paths are inside the container filesystem.
```

Store backups off-host (object storage or backup volume). Encrypt at rest. See [infra/README.md](../../infra/README.md#scheduled-backups-dai-53-p0-04).

---

## Restore drill procedure

Run in **non-production** at least once before first paying tenant go-live.

### Prerequisites

- Recent backup artifacts for: platform Umbraco DB, one tenant Umbraco DB, `dcms_catalog`, payment DB.
- Clean restore environment (separate DB names or disposable compose stack).

### Steps

1. **Stop** dependent services (gateway, APIs, Umbraco) to prevent writes during restore.
2. **Restore** platform Umbraco DB from `.bak`.
3. **Restore** tenant Umbraco DB from `.bak`.
4. **Restore** PostgreSQL databases via `pg_restore`.
5. **Start** Redis empty (or flush); **republish entitlements** for each tenant:
   - `POST /umbraco/dcms/api/tenants/{tenantId}/subscription/activate` (Super Admin) or tenant create publish path per [tenant-entitlements.md](./tenant-entitlements.md).
6. **Start** services; run smoke tests below.
7. **Record evidence** in drill log table.

### Post-restore smoke

```powershell
dotnet test src/backend/dCMS.Tests --filter "FullyQualifiedName~GatewayTenantEntitlement"
dotnet test src/backend/dCMS.Payment.Tests --filter "FullyQualifiedName~PaymentWebhook"
dotnet test src/backend/dCMS.Tests --filter "FullyQualifiedName~TenantProvisioning"
```

Manual:

- Super Admin login to Umbraco backoffice
- List tenants — restored tenant visible with correct `provisioning_status`
- One catalog read via gateway with tenant JWT

---

## Drill evidence template

| Field | Value |
|---|---|
| Date | |
| Operator | |
| Environment | staging / drill |
| Backups used (paths, dates) | |
| RTO (stop → smoke green) | |
| Platform Umbraco restore | pass / fail |
| Tenant Umbraco restore | pass / fail |
| Catalog PG restore | pass / fail |
| Payment DB restore | pass / fail |
| Entitlement republish | pass / fail |
| Issues / follow-ups | |

---

## Integrity checks

After restore, confirm together:

- Tenant ID in `dcms_tenants` matches provisioning row in `TenantProvisioning` (catalog PG).
- Subscription state in SQL matches operational expectation (trial/active).
- At least one payment transaction row matches pre-backup count for test tenant (payment DB).
- Auth: platform admin can access backoffice; tenant-scoped JWT works for restored tenant.

---

## Open follow-ups

- Automate backup jobs in production orchestrator (Kubernetes CronJob / managed DB backup).
- Cross-region backup copy for production SLA.
