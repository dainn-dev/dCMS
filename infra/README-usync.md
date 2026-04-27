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

Run:

```sh
dotnet run --project src/backend/tools/SpawnTenant -- spawn-tenant \
    --tenant t-acme \
    --sa-conn "Server=localhost,14333;User Id=sa;Password=...;TrustServerCertificate=True;" \
    --admin-email admin@acme.test \
    --admin-password 'P@ssw0rd!' \
    [--out infra/tenants/t-acme.env] [--compose] [--health http://localhost:5000/umbraco/api/keepalive/ping]
```

This:
1. Creates an empty SQL Server DB for the tenant.
2. Writes an env file with `ConnectionStrings__umbracoDbDSN`,
   `Umbraco:CMS:Unattended:*` (auto-installs the admin user), and
   `uSync:Settings:ImportAtStartup=All` so the tenant container imports the
   committed baseline schema on first boot.
3. Optionally `docker compose --env-file <out> up -d dcms-web` and polls a
   keepalive URL until the container is healthy.

The CI drift gate (DAI-712 AC4) can be promoted once a tenant container
spawned this way produces a clean `git diff` against the committed baseline.

