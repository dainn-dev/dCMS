# Restore drill evidence — OPS-RESTORE-01

| Field | Value |
|---|---|
| Date | 2026-06-09 |
| Operator | Ops (DAI-53 automated drill) |
| Environment | local compose staging / drill |
| Backups used | `backups/*_20260609_152417.*` (manifest on file) |
| Drill start (UTC) | 2026-06-09T08:24:42Z |
| RTO (stop to smoke complete) | **00:01:29** (target <= 4h) |
| Platform Umbraco restore | **pass** |
| Tenant Umbraco restore | n/a (platform-only MVP; per-tenant P1-04) |
| Catalog PG restore | **pass** |
| Payment DB restore | **pass** |
| Entitlement republish | **manual** — Redis flushed; republish via subscription API before prod drill |
| Smoke log | [restore-drill-smoke-20260609_152442.txt](./restore-drill-smoke-20260609_152442.txt) |
| Issues / follow-ups | `dotnet test` smoke blocked by pre-existing `SqlIntegrationRepositories.cs` build errors; QA re-run filters when build green |

## Backup artifacts (P0-04 dry-run)

| File | Size |
|---|---|
| dcms_catalog_20260609_152417.dump | 90,898 bytes |
| dcms_inventory_20260609_152417.dump | 19,926 bytes |
| dcms_order_20260609_152417.dump | 46,375 bytes |
| dcms_payment_20260609_152417.dump | 6,972 bytes |
| Umbraco_20260609_152417.bak | 13,758,464 bytes |

Script: `infra/scripts/backup-nightly.ps1` (docker exec pg_dump fallback on Windows).

## Integrity checks

- [x] Umbraco database present post-restore (`sys.databases`)
- [x] `PaymentTransactions` table readable; count **0** (empty staging; pre/post match)
- [ ] TenantProvisioning row cross-check — migration 043 not applied on this catalog volume
- [ ] Super Admin backoffice login — requires umbraco-web healthy
- [ ] Gateway JWT catalog read — requires entitlement republish + gateway

## Rollback

Drill restored in-place to same DB names (acceptable for staging). Primary data unchanged relative to backup taken immediately before restore. No failback required.

## QA attestation

Ready for **DAI-50 OPS-RESTORE-01**: verify backup manifest, RTO, DB restore pass rows; re-run smoke tests when solution builds.
