# dCMS infra (Docker)

## Compose

From repository root:

```bash
docker compose -f infra/docker-compose.yml build
docker compose -f infra/docker-compose.yml up -d postgres rabbitmq elasticsearch
```

**Umbraco (`umbraco-web`):** SQLite DB persisted in Docker volume `umbraco_sqlite`. First visit completes the installer wizard at `/umbraco`. Content Delivery API is enabled (`/umbraco/delivery/api/v2/...`); in Development, `PublicAccess` is on — tighten for production (API key / auth).

```bash
docker compose -f infra/docker-compose.yml up -d umbraco-web
```

Apply SQL migrations (from repo root, with `psql` installed and Postgres listening on localhost):

```bash
export PGHOST=127.0.0.1 PGPORT=5432 PGUSER=dcms PGPASSWORD=Your_password123

psql -d dcms_catalog -v ON_ERROR_STOP=1 -f src/backend/dCMS.Infrastructure/Migrations/001_CreateCategories.sql
psql -d dcms_catalog -v ON_ERROR_STOP=1 -f src/backend/dCMS.Infrastructure/Migrations/011_CreateCatalogVariantAxes.sql
psql -d dcms_catalog -v ON_ERROR_STOP=1 -f src/backend/dCMS.Infrastructure/Migrations/003_CreateProducts.sql
psql -d dcms_catalog -v ON_ERROR_STOP=1 -f src/backend/dCMS.Infrastructure/Migrations/004_CreateVariants.sql
psql -d dcms_catalog -v ON_ERROR_STOP=1 -f src/backend/dCMS.Infrastructure/Migrations/010_AddCombinationCanonical.sql
psql -d dcms_catalog -v ON_ERROR_STOP=1 -f src/backend/dCMS.Infrastructure/Migrations/008_CreateDeadLetterEvents.sql
psql -d dcms_catalog -v ON_ERROR_STOP=1 -f src/backend/dCMS.Infrastructure/Migrations/009_CreateAuditAndNotifications.sql

psql -d dcms_inventory -v ON_ERROR_STOP=1 -f src/backend/dCMS.Inventory.Infrastructure/Migrations/007_CreateInventory.sql
psql -d dcms_inventory -v ON_ERROR_STOP=1 -f src/backend/dCMS.Inventory.Infrastructure/Migrations/008_CreateDeadLetterEvents.sql
```

**Inventory.Api audit (US-11):** `ConnectionStrings:Audit` defaults to the Inventory DB. **Docker Compose** sets `ConnectionStrings__Audit` to **catalog** (`dcms_catalog`) so `AuditLogs` use migration `009` already applied there. For local `dotnet run` without that variable, either point `ConnectionStrings:Audit` at `dcms_catalog` or apply `009_CreateAuditAndNotifications.sql` to `dcms_inventory`.

On first boot, `infra/postgres-init/01-create-inventory-db.sql` creates the `dcms_inventory` database alongside `dcms_catalog`.

## M1 — domain unit tests (Catalog + Inventory)

Compose profile **`test`** runs the full `dCMS.Tests` project inside a container. **Testcontainers** (Postgres + Elasticsearch integration tests) needs:

- **`/var/run/docker.sock`** mounted into the test container (already set in `docker-compose.yml` for `m1-domain-tests`).
- **`TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal`** + **`extra_hosts: host.docker.internal:host-gateway`** so the test process can reach sibling containers’ published ports (Docker Desktop and modern Docker Engine on Linux).

```bash
docker compose -f infra/docker-compose.yml --profile test build m1-domain-tests
docker compose -f infra/docker-compose.yml --profile test run --rm m1-domain-tests
```

If integration tests still skip, confirm the socket is mounted and Docker allows the test container to create sibling containers (not rootless-only restrictions without socket access).

Equivalent locally (full test project, same as the `m1-domain-tests` image):

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj -c Release --verbosity normal
```

## Ports

| Service        | Port |
|----------------|------|
| PostgreSQL     | 5432 |
| Umbraco (`dCMS.Web`) | 5000 |
| Catalog API    | 5001 |
| Inventory API  | 5002 |
| RabbitMQ AMQP  | 5672 |
| RabbitMQ UI    | 15672 |
| Elasticsearch  | 9200 |
