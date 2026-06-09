# ADR: Data Residency & Region Strategy (DAI-52-P3)

**Status:** Accepted (metadata phase)  
**Date:** 2026-06-09

## Context

dCMS is multi-tenant at the Siêu thị (tenant) level. Microservice databases (catalog, order, payment) are logically isolated via `TenantId` columns but physically share regional PostgreSQL instances. Each tenant also receives a dedicated Umbraco SQL Server database via SpawnTenant.

Enterprise buyers may require contractual data residency (EU-only, APAC-only, etc.).

## Decision

1. **Phase 1 (this release):** Add `TenantProvisioning.Region` metadata (`default`, `eu-west`, `ap-southeast`, …). SpawnTenant accepts `--region` to persist the value. No automatic cross-region routing yet.
2. **Phase 2 (future):** Region-aware connection maps (Postgres/Redis/S3 endpoints per region) and gateway rejection of cross-region JWT claims.
3. **Data placement documentation:**

| Data class | Store | Residency control today |
|------------|-------|-------------------------|
| Umbraco content & admin users | Per-tenant SQL Server | Provision DB in target region pool |
| Catalog / Order / Payment | Shared Postgres (`TenantId`) | Metadata only — physical move requires P3-02 |
| Analytics | `dcms_analytics` Postgres | Same as commerce DBs |
| Entitlements / host map | Redis | Ephemeral; rebuild from Umbraco + provisioning |
| Secrets | Env / vault | Operator-managed per region |

## Consequences

- Sales can record residency intent on tenant records without false guarantees.
- True isolation requires P3-02 (separate DB clusters) and object-storage region pinning.
- DR drills remain per [`backup-restore-drill.md`](backup-restore-drill.md); extend with region-specific runbooks when pools exist.

## Open questions (DAI-51)

- Metadata-only vs physical isolation for regulated segments.
- Whether backoffice Umbraco must co-locate with commerce Postgres in v1.
