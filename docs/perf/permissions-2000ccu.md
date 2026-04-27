# PermissionService — 2000 CCU load test (DAI-705)

Verifies the DAI-683 cache layer (memory L1 + Redis L2) sustains the project SLO of
2000 concurrent users hitting `PermissionService.HasPermissionAsync` with a cached p95
under 1 ms and a cache hit rate ≥ 95 %.

## Targets (from DAI-705 ACs)

| AC | Metric | Target |
|----|--------|--------|
| AC2 | Cached permission check, p95 (server-measured `secondMs`) | < 1 ms |
| AC2 | Cached permission check, p99 | < 5 ms |
| AC2 | End-to-end HTTP, p95 | < 50 ms |
| AC3 | Cache hit rate (`hits / (hits + misses)`) | ≥ 0.95 |
| AC4 | SQL Server permission `SELECT`s (steady state) | ≤ 100 / sec |

## How to run

```bash
cd infra

# 1. Pick a load-test key + enable the seed
export DCMS_LOADTEST_KEY=$(openssl rand -hex 16)
export DCMS_LOADTEST_SEED=true

# 2. Bring up Umbraco + dependencies (waits for SQL Server, Redis, Postgres)
docker compose up -d --build redis sqlserver postgres umbraco-web
docker compose ps        # umbraco-web should be 'healthy' before continuing

# 3. Sanity-check the load-test endpoint
curl -fsS -H "X-LoadTest-Key: $DCMS_LOADTEST_KEY" \
  "http://localhost:5000/dcms-internal/perm-loadtest/check?userId=1&module=orders&action=view" | jq
# Expect: { ..., "allowed": true, "allowed2": true, "firstMs": <db>, "secondMs": <cached> }

# 4. Run the 2000-VU / 60-s load test
docker compose --profile loadtest up --abort-on-container-exit k6-permission-cache

# 5. Capture metrics + Redis command stats
curl -s http://localhost:5000/metrics | grep -E '^dcms_permissions_' > permissions-metrics.txt
docker compose exec redis redis-cli INFO commandstats | grep -E 'cmdstat_(get|set|incr)' > redis-cmdstats.txt
```

## Capturing metric values

After the run finishes, the k6 container writes
`infra/loadtests/results/permission-cache-summary.json` and a streaming JSON of every
sample to `permission-cache-runs.json` in the same directory. The Prometheus snapshot
contains:

```
dcms_permissions_cache_hit_total{level="memory"}   <count>
dcms_permissions_cache_hit_total{level="redis"}    <count>
dcms_permissions_cache_miss_total                  <count>
dcms_permissions_cache_error_total{op="..."}       <count>
dcms_permissions_check_duration_seconds_bucket{...} <histogram>
```

Hit rate = `(memory + redis) / (memory + redis + miss)`.

## Result — <YYYY-MM-DD>

> Paste the actual numbers from your run below. Placeholders are intentional.

**Environment**

| | |
|---|---|
| Host | `<machine, cores, RAM>` |
| Compose stack | `umbraco-web` + `redis` + `sqlserver` (single host) |
| Image tags | Umbraco 16.5.1 / Redis 7.x / mssql 2022 |
| k6 image | `grafana/k6:0.50.0` |
| VUs / duration | 2000 / 60 s |

**k6 (HTTP)**

| Metric | min | med | avg | p95 | p99 | max |
|---|---|---|---|---|---|---|
| `http_req_duration` (ms) | _ | _ | _ | _ | _ | _ |
| `firstMs` (server, ms) | _ | _ | _ | _ | _ | _ |
| `secondMs` (server, ms) | _ | _ | _ | _ | _ | _ |

| Metric | Value |
|---|---|
| `http_req_failed` rate | _ |
| Total iterations | _ |

**Cache counters**

| Counter | Value |
|---|---|
| `dcms_permissions_cache_hit_total{level="memory"}` | _ |
| `dcms_permissions_cache_hit_total{level="redis"}` | _ |
| `dcms_permissions_cache_miss_total` | _ |
| `dcms_permissions_cache_error_total` (any op) | _ |
| Hit rate | _ |

**Redis command stats** (from `INFO commandstats`)

| cmd | calls | usec_per_call |
|---|---|---|
| `get` | _ | _ |
| `set` | _ | _ |
| `incr` | _ | _ |

**SQL Server permission queries** — sample with:

```sql
SELECT TOP 5 qs.execution_count, qs.last_elapsed_time/1000 AS last_ms, st.text
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
WHERE st.text LIKE '%dcms_role_module_permissions%'
ORDER BY qs.execution_count DESC;
```

| Query | Executions during run | p95 elapsed |
|---|---|---|
| `SELECT DISTINCT action FROM dcms_role_module_permissions ...` | _ | _ |

## Pass / fail

| AC | Target | Measured | Status |
|---|---|---|---|
| AC2 — cached p95 | < 1 ms | _ | _ |
| AC2 — cached p99 | < 5 ms | _ | _ |
| AC2 — http p95 | < 50 ms | _ | _ |
| AC3 — hit rate | ≥ 0.95 | _ | _ |
| AC4 — DB queries | ≤ 100 / s | _ | _ |

## Notes

- **`USER_ID` requirement** — the seed (`Dcms__Access__LoadTestSeedEnabled=true`) links
  Umbraco user `1` to the `dcmsOperations` group. If you target a different user, link
  it manually first or the `aliases` lookup short-circuits to `false` and the load
  test never enters the cached path.
- **Version-key bottleneck** — `PermissionCache.GetTenantVersionAsync` caches the
  Redis `roles_version` value in IMemoryCache for `Dcms:Access:PermissionCache:VersionMemoryTtl`
  (default 2 s). After a `BumpTenantVersionAsync`, callers may observe the old version
  for at most that window. If a tighter invalidation guarantee is required, drop the
  TTL — at 0 s the cache reverts to per-call Redis GETs and the 2000 CCU SLO will
  almost certainly fail.
- **Tuning iterations** — if hit rate is below target, look at `MemoryTtl` (default 30 s);
  if cached p95 misses target, look at the Redis round-trip and consider whether the
  Umbraco app pod is co-located with Redis in your test bench.
