# DAI-705 — Load test PermissionService cache (2000 CCU)

## Goal

Verify that the new PermissionService cache (DAI-683) achieves:

- High cache hit rate under load
- Low p95 latency for cached permission checks
- Minimal DB load from permission queries

## Prerequisites

- Docker Desktop running
- Local stack up (`infra/docker-compose.yml`)

## How it works

We expose a **Development-only** internal endpoint on `dCMS.Web`:

- `GET /dcms-internal/perm-loadtest/check?userId=...&module=...&action=...`
- Guarded by header `X-LoadTest-Key` which must match config `Dcms:Access:LoadTestKey`
- The handler calls `PermissionService.HasPermissionAsync(...)` twice:
  - `firstMs`: cold/warm path (may query DB on first hit)
  - `secondMs`: expected cached path (Redis/memory)

## Run

From repo root:

```bash
docker compose -f infra/docker-compose.yml up -d postgres sqlserver rabbitmq redis elasticsearch umbraco-web
docker compose -f infra/docker-compose.yml --profile loadtest run --rm k6-permission-cache
```

Tune:

- `K6_VUS=2000` (default)
- `K6_DURATION=60s` (default)
- `DCMS_LOADTEST_KEY=...` (default `dcms-loadtest-key`)
- `K6_USER_ID=1`, `K6_MODULE=orders`, `K6_ACTION=view`

Example:

```bash
DCMS_LOADTEST_KEY=dev-key K6_VUS=2000 K6_DURATION=120s docker compose -f infra/docker-compose.yml --profile loadtest run --rm k6-permission-cache
```

## What to look for

- k6 thresholds:
  - `http_req_failed` low
  - `http_req_duration p95` stable (tune threshold per machine)
- Sample response body includes `firstMs` and `secondMs` — `secondMs` should be consistently small.

## Notes / safety

- Endpoint is **not available outside Development** (returns 404).
- Key is required; do not reuse the load-test key in production.

