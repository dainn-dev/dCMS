# uSync (DAI-686)

## Folder

Baseline exports live at:

- `src/backend/dCMS.Web/uSync/v16/`

## Local dev

`dCMS.Web` (Development) is configured to:

- import at startup (`ImportAtStartup=All`)
- export at startup (`ExportAtStartup=All`)
- export on save (`ExportOnSave=All`)

## CI (drift detection)

Workflow: `.github/workflows/usync-drift.yml`

Full drift detection (fresh DB → export → `git diff --exit-code`) will be enabled after tenant unattended install + deterministic baseline export is in place (DAI-714).

## DAI-713 — DocTypes

Spec for the 7 content types this platform owns lives at
[`docs/usync/doctypes-spec.md`](../docs/usync/doctypes-spec.md). Author the
doctypes via the Umbraco backoffice on a fresh dev environment; with
`ExportAtStartup=All` (Development) the resulting XML lands in
`src/backend/dCMS.Web/uSync/v16/ContentTypes/` and should be committed.

## DAI-714 — Tenant bootstrap CLI

Full runbook: [`docs/operations/first-paying-tenant-provisioning.md`](../docs/operations/first-paying-tenant-provisioning.md)

Run (happy path):

```sh
dotnet run --project src/backend/tools/SpawnTenant -- provision \
    --tenant t-acme \
    --tenant-code acme \
    --sa-conn "Server=localhost,14333;User Id=sa;Password=...;TrustServerCertificate=True;" \
    --catalog-conn "Host=127.0.0.1;Port=5432;Database=dcms_catalog;Username=dcms;Password=..." \
    --umbraco-platform-conn "Server=localhost,14333;Database=Umbraco;User Id=sa;Password=...;TrustServerCertificate=True;" \
    --admin-email admin@acme.test \
    --admin-password 'P@ssw0rd!' \
    [--redis localhost:6379] [--domain shop.acme.test] \
    [--out infra/tenants/t-acme.env] [--compose] [--health http://localhost:5000/umbraco/api/keepalive/ping]
```

`spawn-tenant` remains an alias for `provision`.

Lifecycle commands: `status`, `retry`, `rollback`, `suspend`, `reactivate` (see runbook).

This pipeline:
1. Creates provisioning records in PostgreSQL catalog (`TenantProvisioning` + steps/audit/onboarding).
2. Creates platform tenant + trial subscription in Umbraco SQL and warms Redis entitlements.
3. Creates an empty SQL Server DB for the tenant (`dcms_tenant_<code>`).
4. Writes an env file for the dCMS.Web container (`Unattended` install + `uSync__Settings__ImportAtStartup=All`).
5. Optionally `docker compose --env-file <out> up -d dcms-web` and polls keepalive until healthy.
6. Binds domain → tenant/store in Redis when `--domain` is set.

The CI drift gate (DAI-712 AC4) can be promoted once a tenant container
spawned this way produces a clean `git diff` against the committed baseline.

