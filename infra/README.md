# dCMS infra (Docker)

## Local stack (US-M0-1 / DAI-301)

A **`.dockerignore`** at the repository root excludes `**/bin/` and `**/obj/` so Docker builds do not copy broken host artifacts into the Linux build container.

From **repository root**, build and start the services that exist in this repo today:

```bash
docker compose -f infra/docker-compose.yml build
docker compose -f infra/docker-compose.yml up -d postgres rabbitmq redis elasticsearch catalog-api inventory-api order-api payment-api catalog-worker umbraco-web
```

**Included:** PostgreSQL (**four** databases: `dcms_catalog`, `dcms_inventory`, `dcms_order`, `dcms_payment`), RabbitMQ (AMQP + management UI), Redis, Elasticsearch, **Catalog.Api**, **Inventory.Api**, **Order.Api** and **Payment.Api** (minimal placeholders with `GET /health` only — replace with real services when implemented), **Catalog.Worker** (RabbitMQ / MassTransit consumers + outbox relays), **Umbraco** (`dCMS.Web`, SQLite in volume `umbraco_sqlite`).

**Deferred vs original DAI-301 wording:** **SQL Server** — this stack uses **PostgreSQL** only. Order/Payment placeholders do not connect to their databases yet; DBs exist for future migrations.

### RabbitMQ Management UI

- URL: [http://localhost:15672](http://localhost:15672)  
- Default credentials: `guest` / `guest`

### Health checks (`GET /health`)

| Service        | Host URL (published) |
|----------------|----------------------|
| Catalog.Api    | [http://localhost:5001/health](http://localhost:5001/health) |
| Inventory.Api  | [http://localhost:5002/health](http://localhost:5002/health) |
| Order.Api (placeholder) | [http://localhost:5003/health](http://localhost:5003/health) |
| Payment.Api (placeholder) | [http://localhost:5004/health](http://localhost:5004/health) |
| dCMS.Web (Umbraco) | [http://localhost:5000/health](http://localhost:5000/health) |

Compose **healthchecks** use `curl` against `http://127.0.0.1:8080/health` inside **catalog-api**, **inventory-api**, **order-api**, **payment-api**, and **umbraco-web** containers (runtime images install `curl` in the Dockerfiles). **catalog-worker** is a .NET worker host with **no HTTP port** — use logs / `docker compose ps` for liveness.

### Umbraco first run

SQLite is persisted in Docker volume `umbraco_web`. First visit completes the installer at `/umbraco`. Content Delivery API is enabled (`/umbraco/delivery/api/v2/...`); in Development, `PublicAccess` is on — tighten for production (API key / auth).

### SQL migrations (Postgres)

After Postgres is healthy, apply migrations (from repo root, with `psql` installed and Postgres on localhost):

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

On first boot, Postgres init scripts create **`dcms_inventory`** (`01-create-inventory-db.sql`) and **`dcms_order`** / **`dcms_payment`** (`02-create-order-payment-dbs.sql`) alongside the default **`dcms_catalog`** database from `POSTGRES_DB`.

If you reuse an **old** Postgres data volume that was created before `02-create-order-payment-dbs.sql` existed, either run that script manually against Postgres once or remove the volume (`docker compose … down -v`, **destructive**) so init scripts run again.

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
| Order API (placeholder) | 5003 |
| Payment API (placeholder) | 5004 |
| RabbitMQ AMQP  | 5672 |
| RabbitMQ UI    | 15672 |
| Redis          | 6379 |
| Elasticsearch  | 9200 |
