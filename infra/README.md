# dCMS infra (Docker)

## Local stack (US-M0-1 / DAI-301)

A **`.dockerignore`** at the repository root excludes `**/bin/` and `**/obj/` so Docker builds do not copy broken host artifacts into the Linux build container.

From **repository root**, build and start the services that exist in this repo today:

```bash
docker compose -f infra/docker-compose.yml build
docker compose -f infra/docker-compose.yml up -d postgres sqlserver rabbitmq redis elasticsearch catalog-api inventory-api order-api payment-api promotions-api fulfillment-api gateway catalog-worker umbraco-web
```

**Included:** PostgreSQL (**four** databases: `dcms_catalog`, `dcms_inventory`, `dcms_order`, `dcms_payment`), **SQL Server 2022** (Umbraco CMS database `Umbraco` only; host port **14333**), RabbitMQ (AMQP + management UI), Redis, Elasticsearch, **Catalog.Api**, **Inventory.Api**, **Order.Api** (M5: `dCMS.Order.Api` + DbUp migrations in `dCMS.Order.Infrastructure` on startup; MassTransit/RabbitMQ), **Payment.Api** (minimal placeholder with `GET /health` until Payment service is implemented), **Promotions.Api** (campaigns on `dcms_catalog`), **Fulfillment.Api** (eStore fulfillment config on `dcms_catalog`; DAI-612), **dCMS.Gateway** (YARP — `/gateway/v1/catalog|orders|inventory|promotions|fulfillment/...`), **Catalog.Worker** (RabbitMQ / MassTransit consumers + outbox relays), **Umbraco** (`dCMS.Web`, **Microsoft SQL Server** + volume `umbraco_data` for `/app/umbraco/Data`).

**Note:** Application APIs use **PostgreSQL** only; **SQL Server** is used **only** for the Umbraco CMS database in Docker Compose.

**Order ↔ Inventory (DAI-314):** Compose sets **`InternalInventory__ApiKey`** on **inventory-api** (enables `POST /internal/inventory/*`) and matching **`Inventory__InternalApiKey`** + **`Inventory__BaseUrl=http://inventory-api:8080/`** on **order-api** for sync stock checks before order creation. **order-api** waits for **inventory-api** to be healthy.

### RabbitMQ Management UI

- URL: [http://localhost:15672](http://localhost:15672)  
- Default credentials: `guest` / `guest`

### Health checks (`GET /health`)

| Service        | Host URL (published) |
|----------------|----------------------|
| Catalog.Api    | [http://localhost:5001/health](http://localhost:5001/health) |
| Inventory.Api  | [http://localhost:5002/health](http://localhost:5002/health) |
| Order.Api (M5 — DbUp + RabbitMQ) | [http://localhost:5003/health](http://localhost:5003/health) |
| Payment.Api (placeholder) | [http://localhost:5004/health](http://localhost:5004/health) |
| Promotions.Api | [http://localhost:5005/health](http://localhost:5005/health) |
| Fulfillment.Api | [http://localhost:5006/health](http://localhost:5006/health) |
| dCMS.Gateway   | [http://localhost:5100/health](http://localhost:5100/health) |
| dCMS.Web (Umbraco) | [http://localhost:5000/health](http://localhost:5000/health) |

Compose **healthchecks** use `curl` against `http://127.0.0.1:8080/health` inside **catalog-api**, **inventory-api**, **order-api**, **payment-api**, and **umbraco-web** containers (runtime images install `curl` in the Dockerfiles). **catalog-worker** is a .NET worker host with **no HTTP port** — use logs / `docker compose ps` for liveness.

### Umbraco first run (Docker)

- **Database:** SQL Server (`sqlserver` service). Compose sets `ConnectionStrings:umbracoDbDSN` + `umbracoDbDSN_ProviderName` = `Microsoft.Data.SqlClient`. Init job `umbraco-db-init` creates database **`Umbraco`** if missing (SA password matches compose: `Umbraco_Dev_2026!`).
- **Host access (optional):** `localhost,14333` with `sa` / same password (SSMS / Azure Data Studio).
- **Local files:** Volume `umbraco_data` → `/app/umbraco/Data` (NuCache / local state; not the DB).
- **Installer:** First visit completes setup at `/umbraco`. Content Delivery API is enabled (`/umbraco/delivery/api/v2/...`); in Development, `PublicAccess` is on — tighten for production (API key / auth).

**Local `dotnet run` (no Docker):** `appsettings.json` still defaults to **SQLite** under `umbraco/Data/` unless you override connection strings.

**Upgrading from the old SQLite Compose setup:** remove the obsolete Docker volume if it still exists (name is often `dcms_umbraco_sqlite`): `docker volume rm dcms_umbraco_sqlite` after `docker compose … down`.

### SQL migrations (Postgres)

**Migrations run automatically on startup** — core services run DbUp (or service-specific upgrader) at boot. No manual `psql` steps required after `docker compose up`.

- **Catalog** (`dcms_catalog`): applied by **`catalog-api`**, **`catalog-worker`**, and **`fulfillment-api`** via `CatalogDbMigrationHostedService` (all embedded `.sql` scripts, including campaigns and fulfillment). **`promotions-api`** does not run DbUp — run it together with **`catalog-api`** or **`fulfillment-api`** so `dcms_catalog` is already migrated (e.g. `020_CreateCampaigns.sql`).
- **Inventory** (`dcms_inventory`): applied by `inventory-api` via `InventoryDbMigrationHostedService`
- **Order** (`dcms_order`): applied by `order-api` via `OrderDbMigrationHostedService`
- `catalog-worker` waits for both `catalog-api` and `inventory-api` to be healthy before starting, so migrations are guaranteed to be complete when the Worker connects.

**Local `dotnet run` (no Docker):** migrations still run automatically on startup. Ensure the target database exists and the connection string is configured correctly.

**Inventory.Api audit (US-11):** `ConnectionStrings:Audit` defaults to the Inventory DB. **Docker Compose** sets `ConnectionStrings__Audit` to **catalog** (`dcms_catalog`) so `AuditLogs` use the catalog schema. For local `dotnet run` without that variable, point `ConnectionStrings:Audit` at `dcms_catalog`.

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

## Umbraco v16 Backoffice — Orders SPA (React)

The custom **Orders** backoffice section is implemented as a **Vite + React SPA** and mounted by an Umbraco backoffice extension element.

- **SPA source**: `src/backoffice/dcms-backoffice-spa/`
- **Umbraco plugin**: `src/backend/dCMS.Web/App_Plugins/DcmsV16/`
- **Build output (served by Umbraco)**:
  - `src/backend/dCMS.Web/App_Plugins/DcmsV16/dist/orders-spa.js`
  - `src/backend/dCMS.Web/App_Plugins/DcmsV16/dist/orders-spa.css`
- **Bridge element (mounts SPA, injects CSS inside the element scope)**:
  - `src/backend/dCMS.Web/App_Plugins/DcmsV16/dcms-orders-section.js`

### Build the Orders SPA

From the SPA folder:

```bash
cd src/backoffice/dcms-backoffice-spa
npm install
npm run build
```

Notes:
- On **PowerShell**, do **not** chain commands with `&&` (it may fail depending on PS version). Prefer running commands on separate lines, or use `;`.
- The build is configured to emit bundles directly into Umbraco’s `App_Plugins/DcmsV16/dist/` folder so the backoffice can load them without extra copy steps.

## Ports

| Service        | Port |
|----------------|------|
| PostgreSQL     | 5432 |
| Umbraco (`dCMS.Web`) | 5000 |
| Catalog API    | 5001 |
| Inventory API  | 5002 |
| Order API (M5) | 5003 |
| Payment API (placeholder) | 5004 |
| Promotions API | 5005 |
| Fulfillment API | 5006 |
| dCMS.Gateway   | 5100 |
| RabbitMQ AMQP  | 5672 |
| RabbitMQ UI    | 15672 |
| Redis          | 6379 |
| Elasticsearch  | 9200 |
