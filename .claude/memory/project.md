# Project Facts — dCMS

_Stable facts. Chỉ update khi project thay đổi căn bản._

## Description

dCMS là nền tảng eCommerce CMS headless theo **mô hình siêu thị**. Platform quản lý nhiều **Siêu thị** (chuỗi bán lẻ/tập đoàn) — mỗi Siêu thị là 1 tenant độc lập. Mỗi Siêu thị sở hữu nhiều **Brands** (thương hiệu con) và mỗi Brand vận hành nhiều **Stores** (cửa hàng/kênh bán online). Platform cung cấp:
- Quản lý sản phẩm nâng cao (per Store, grouped by Brand)
- SEO tools per Store
- Xử lý đơn hàng
- Quản lý doanh thu và marketing (per Brand hoặc per Store)
- Storefront tùy biến theo brand identity của từng Store

## Tech Stack

| Layer | Technology |
|---|---|
| CMS / Backend | Umbraco CMS (ASP.NET Core) — custom eCommerce layer |
| Storefront | Next.js (App Router) + TypeScript |
| Database | SQL Server (per-Siêu thị/tenant) + Platform DB (Siêu thị registry) |
| Search | Elasticsearch |
| Payment | External system qua API Gateway (không xử lý trực tiếp) |
| Auth | RBAC với dynamic roles |
| Containerization | Docker + docker-compose |
| CI/CD | GitHub Actions |
| Security | Rate limiting, CORS policy |

## Architecture

**Multi-tenant headless architecture — Mô hình Siêu thị:**

Hierarchy 4 cấp:
```
dCMS Platform  (Super Admin)
  └── Siêu thị / Chain  [TENANT]  →  1 Umbraco instance  +  1 SQL Server DB  +  ES prefix
        └── Brands[]  (thương hiệu con của Siêu thị)
              └── Stores[]  (cửa hàng/kênh bán — có storefront Next.js riêng)
```

Ví dụ thực tế:
```
dCMS Platform
  ├── Lotte Vietnam [Tenant]
  │     ├── Lotte Mart [Brand]
  │     │     ├── Lotte Mart Thủ Đức [Store] → lotte-thu-duc.com
  │     │     └── Lotte Mart Gò Vấp  [Store] → lotte-go-vap.com
  │     └── Lotte Online [Brand]
  │           └── Lotte Online VN    [Store] → lotteonline.vn
  └── BigC Vietnam [Tenant]
        └── BigC [Brand]
              ├── BigC Thăng Long    [Store] → bigc-thanglang.vn
              └── BigC online        [Store] → bigc-online.vn
```

**Isolation rules:**
- **Tenant (Siêu thị):** Hoàn toàn isolated — DB riêng, Umbraco instance riêng, ES index prefix riêng
- **Brand:** Layer tổ chức trong tenant — không isolated về DB, share Umbraco instance
- **Store:** Đơn vị bán hàng — có storefront (Next.js) riêng, domain riêng; dùng chung Umbraco instance của Siêu thị

**Data flow:**
```
End Customer → Next.js Storefront (Store domain)
  → StoreMiddleware: resolve tenantId + storeId từ domain → Platform DB
  → route đến Umbraco instance của Siêu thị
  → Umbraco Content Delivery API → SQL Server (Siêu thị DB) [filter storeId]
  → Elasticsearch dcms-{tenantId}-* [filter storeId/brandId]
  → API Gateway → External Payment System

Chain Admin → Umbraco Backoffice (Siêu thị) → quản lý tất cả Brands + Stores
Brand Manager → Umbraco Backoffice → chỉ thấy data của Brand được assign
Store Manager → Umbraco Backoffice → chỉ thấy data của Store được assign
Super Admin → Admin Portal → Platform DB (Siêu thị registry)
```

**Platform DB (tách riêng):**
- Siêu thị (id, name, plan, billingInfo, umbracoUrl, dbConnection)
- Brands (id, tenantId, name, logo, settings)
- Stores (id, brandId, tenantId, name, domain, slug, status)
- StoreUsers (id, userId, tenantId, brandId?, storeId?, roles[])
- Domain routing (domain → tenantId + storeId)

## Database

- **Type:** SQL Server (Microsoft)
- **ORM:** Umbraco built-in (NPoco) + custom repositories
- **Multi-tenant:** Mỗi **Siêu thị (Tenant)** có SQL Server database riêng — hoàn toàn isolated
- **Platform DB:** DB riêng biệt lưu Siêu thị registry, Brand/Store registry, domain routing, billing
- **Key domains (per Siêu thị DB — shared bởi tất cả Brands + Stores trong Siêu thị):**
  - Brands (id, tenantId, name, logo, settings)
  - Stores (id, brandId, tenantId, name, domain, slug)
  - Products (id, brandId, storeId, catalog, variants, pricing, inventory)
  - Orders (id, storeId, cart, checkout, fulfillment)
  - Customers (id, storeId, accounts, addresses, wishlists)
  - Content (Umbraco content tree — scoped theo Store)
  - Settings (store config, payment config, shipping rules)
- **Key entities (Platform DB):**
  - Siêu thị (id, name, plan, billingInfo, umbracoUrl, dbConnectionString)
  - Brands (id, tenantId, name, logo)
  - Stores (id, brandId, tenantId, name, domain, slug, status)
  - StoreUsers (id, userId, tenantId, brandId?, storeId?, roles[])
  - DomainRouting (domain → tenantId + storeId)

## API Overview

- **Umbraco Content Delivery API:** `/umbraco/delivery/api/v2/` — content fetching
- **Custom Commerce API:** `/api/v1/products`, `/api/v1/orders`, `/api/v1/cart`, `/api/v1/checkout`
- **Payment API:** Proxy qua API Gateway external — không expose payment logic trực tiếp
- **Search API:** `/api/v1/search` — Elasticsearch backed
- **Admin API:** `/api/admin/` — Super admin platform management

## Key Components

| Component | Path | Vai trò |
|---|---|---|
| Domain Core | `src/backend/dCMS.Core/` | Business logic, domain models, interfaces |
| Infrastructure | `src/backend/dCMS.Infrastructure/` | DB repos, Elasticsearch client, external service clients |
| Web Host | `src/backend/dCMS.Web/` | Umbraco setup, API controllers, middleware pipeline |
| Next.js Storefront | `src/frontend/` | Storefront UI, SSR/SSG pages, client-side interactions |
| Docker Config | `infra/docker/` | Dockerfiles + compose files |
| CI/CD | `.github/workflows/` | Build, test, deploy pipelines |

## Infrastructure

- **Runtime:** Docker containers
- **Local dev:** `docker-compose up` starts toàn bộ stack
- **CI/CD:** GitHub Actions — auto test + build images on push, deploy on merge to main
- **Environments:** dev / staging / production (Docker-based)

## Conventions

- **Multi-tenant first:** Mọi query phải filter theo `TenantId`
- **API response shape:** `{ data: T, meta: { pagination? }, error: null | { code, message } }`
- **Backend:** Clean Architecture — Core không depend vào Infrastructure
- **Frontend:** Server Components by default, chỉ dùng Client Components khi cần interactivity
- **Localization:** Dùng Umbraco multi-language feature + `next-intl` trên frontend
- **Currency:** Lưu giá dưới dạng integer (cents/smallest unit), convert khi display
- **Errors:** Log đầy đủ internally, trả về generic message cho client
