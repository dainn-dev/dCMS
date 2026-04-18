# Architecture — dCMS eCommerce CMS

## Overview

dCMS là nền tảng eCommerce CMS headless theo **mô hình siêu thị**. Platform quản lý nhiều **Siêu thị** (chuỗi bán lẻ/tập đoàn) — mỗi Siêu thị là 1 **tenant** hoàn toàn độc lập. Mỗi Siêu thị sở hữu nhiều **Brands** (thương hiệu con), mỗi Brand vận hành nhiều **Stores** (cửa hàng/kênh bán online). Hệ thống hỗ trợ multi-language, multi-currency, và integrate với payment gateway external.

**Tenant = Siêu thị.** Isolation về data, Umbraco instance, và SQL Server DB đều ở cấp Siêu thị. Brand/Store là layer tổ chức bên trong tenant.

**User roles:** Super Admin → Chain Admin → Brand Manager → Store Manager → Store Staff → End Customers.

## System Diagram

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                                  USERS                                        │
│  [End Customers]  [Store Staff]  [Store/Brand Mgr]  [Chain Admin] [SuperAdmin]│
└──────┬───────────────────┬──────────────┬──────────────────┬──────────┬───────┘
       │                   │              │                  │          │
       ▼                   ▼              ▼                  ▼          ▼
┌────────────┐   ┌──────────────────────────────────┐   ┌───────────────────┐
│ Next.js    │   │   Umbraco Backoffice              │   │  Admin Portal     │
│ Storefront │   │   (per Siêu thị / Tenant)         │   │  (Super Admin)    │
│ (per Store)│   │   manages all Brands + Stores     │   │                   │
└─────┬──────┘   └──────────────┬───────────────────┘   └────────┬──────────┘
      │                          │                                 │
      │          ┌───────────────┘                                 │
      │          │  Tenant Middleware:                             │
      │          │  domain → Platform DB → tenantId + storeId     │
      │          ▼                                                 │
      │   ┌──────────────────────────────────────────────────┐    │
      │   │         TENANT API LAYER (per Siêu thị)          │    │
      │   │                                                  │    │
      │   │  Umbraco Content Delivery API v2                 │    │
      └──►│  /umbraco/delivery/api/v2/content?storeId=...    │    │
          │                                                  │    │
          │  Custom Commerce API                             │    │
          │  /api/v1/products  /api/v1/orders                │    │
          │  /api/v1/cart      /api/v1/checkout              │    │
          │  /api/v1/search                                  │    │
          │                                                  │    │
          │  Middleware: Rate Limit · CORS · RBAC · TenantID │    │
          └──────┬──────────────────┬───────────────────────┘    │
                 │                  │                              │
         ┌───────┴──────┐  ┌────────┴──────────┐  ┌─────────────┐│
         │  SQL Server  │  │  Elasticsearch    │  │  External   ││
         │  (per Tenant)│  │  dcms-{tenantId}-*│  │  Payment GW ││
         └──────────────┘  └───────────────────┘  └─────────────┘│
                                                                   │
┌──────────────────────────────────────────────────────────────────▼──────────┐
│                     PLATFORM DB  (central, always-on)                        │
│  Siêu thị: id · name · plan · umbracoUrl · dbConn · billing                 │
│  Brands:   id · tenantId · name · logo · settings                           │
│  Stores:   id · brandId · tenantId · name · domain · slug · status          │
│  StoreUsers: userId · tenantId · brandId? · storeId? · roles[]              │
│  DomainRouting: domain → tenantId + storeId  [cached in Redis]              │
└──────────────────────────────────────────────────────────────────────────────┘
```

## Components

### Backend — Umbraco CMS (ASP.NET Core)

**Location:** `src/backend/`

Clean Architecture gồm 4 projects:

#### `dCMS.Core/`
- **Vai trò:** Domain layer — không depend vào bất kỳ infrastructure nào
- **Nội dung:** Domain models (Client, Store, Product, Order, Customer...), interfaces (IProductRepository, IOrderService, IStoreRepository...), domain events, value objects
- **Key files:** `Models/`, `Interfaces/`, `Services/`, `Events/`

#### `dCMS.Infrastructure/`
- **Vai trò:** Implement interfaces từ Core — database, external services, search
- **Nội dung:** SQL Server repositories (via Umbraco's NPoco), Elasticsearch client, Payment Gateway client, Email service
- **Key files:** `Repositories/`, `Search/`, `Payment/`, `Email/`

#### `dCMS.Web/`
- **Vai trò:** Umbraco web host — DI setup, API controllers, middleware pipeline
- **Nội dung:** Umbraco configuration, custom API controllers, middleware (tenant resolution, rate limiting, CORS, auth), Program.cs
- **Key files:** `Controllers/`, `Middleware/`, `Program.cs`, `appsettings.json`

#### `dCMS.Tests/`
- **Vai trò:** xUnit tests — unit tests cho Core services, integration tests cho API
- **Nội dung:** Unit/, Integration/, Fixtures/

### Frontend — Next.js Storefront

**Location:** `src/frontend/`

Next.js App Router với TypeScript.

| Directory | Vai trò |
|---|---|
| `app/[locale]/` | Localized pages (homepage, products, cart, checkout, account) |
| `components/` | Reusable UI components |
| `lib/` | API clients (Umbraco, Commerce API), utilities |
| `types/` | TypeScript type definitions |
| `public/` | Static assets |

### Infrastructure

**Location:** `infra/`

| File | Vai trò |
|---|---|
| `docker/Dockerfile.backend` | Umbraco backend image (ASP.NET Core 8) |
| `docker/Dockerfile.frontend` | Next.js frontend image (Node 20 Alpine) |
| `docker-compose.yml` | Local dev stack orchestration |

### CI/CD

**Location:** `.github/workflows/`

| Workflow | Trigger | Action |
|---|---|---|
| `test.yml` | Push to any branch | Run `dotnet test` + `npm test` |
| `build.yml` | Push to `main` | Build Docker images + push to registry |
| `deploy-staging.yml` | Push to `main` | Deploy to staging environment |
| `deploy-prod.yml` | Tag release | Deploy to production |

## Data Flow

### End Customer — Browse & Purchase

```
Customer → Next.js Storefront
  → fetch product list: GET /api/v1/products?q=...&category=... → Elasticsearch
  → fetch product detail: GET /api/v1/products/{slug} → SQL Server
  → fetch content: GET /umbraco/delivery/api/v2/content → SQL Server
  → add to cart: POST /api/v1/cart (session/cookie based)
  → checkout: POST /api/v1/checkout → API Gateway → External Payment
  → order created → SQL Server + Email notification
```

### Store Manager/Staff — Manage Store

```
Store Manager → Umbraco Backoffice (Siêu thị instance)
  → RBAC filter: chỉ thấy content/products của storeId được assign
  → manage products → SQL Server (Tenant DB, filtered by storeId)
  → product saved → Elasticsearch sync → dcms-{tenantId}-products [storeId field]
  → manage orders → SQL Server (Tenant DB, filtered by storeId)
```

### Brand Manager — Manage Brand

```
Brand Manager → Umbraco Backoffice (Siêu thị instance)
  → RBAC filter: thấy tất cả Stores trong brandId được assign
  → manage brand settings, products across stores
  → cross-store reporting within brand
```

### Chain Admin — Manage Siêu thị

```
Chain Admin → Umbraco Backoffice (Siêu thị instance)
  → full access: tất cả Brands + Stores trong Siêu thị
  → provision Stores mới → ghi vào Platform DB + Umbraco
  → manage users & roles → Platform DB StoreUsers
```

## Phân quyền backoffice & API — kiến trúc hai lớp

dCMS **không** gom toàn bộ vào một hệ permission đơn lẻ. Hai lớp tách biệt: **Umbraco User Groups** (section backoffice) và **Platform RBAC** (bảng gán quyền + JWT + policy ASP.NET). Khi code review cần phân biệt *vào được menu* với *được gọi API đúng scope*.

### Lớp 1 — Umbraco User Groups (backoffice shell)

| Khía cạnh | Nội dung |
|-----------|----------|
| **Công nghệ** | `IUserGroup` / `IUserGroupService`, **Allowed Sections** trên group Umbraco. |
| **Mục đích** | User có **thấy** section tùy biến (E-Store, Orders, Approval, Reports) hay không. |
| **Độ chi tiết** | Thô — theo section CMS; không diễn tả Chain / Brand / Store. |
| **Code** | `src/backend/dCMS.Web/DcmsSectionAliases.cs`; `GrantDcmsCustomSectionsNotificationHandler` (grant mặc định Admin/Editor). |

Ví dụ: user thuộc group Writers chưa được thêm section → không mở SPA dù vẫn là user hợp lệ trong Umbraco.

### Lớp 2 — RBAC nghiệp vụ (Platform DB + JWT + policy)

| Khía cạnh | Nội dung |
|-----------|----------|
| **Công nghệ** | Platform DB: ví dụ `StoreUsers` (`userId`, `tenantId`, `brandId?`, `storeId?`, `roles[]`); JWT chứa claims; `DcmsPolicies` / `DcmsRoles` trong `src/backend/dCMS.AspNetCore.Auth/`. |
| **Mục đích** | Ai được làm gì trên Commerce/Catalog API; phạm vi tenant / brand / store. |
| **Độ chi tiết** | Mịn — roles động, hierarchy; middleware khớp storeId / tenant trên route. |

Đã thấy section E-Store (lớp 1) vẫn có thể **403** nếu lớp 2 thiếu policy hoặc sai scope.

### Luồng trách nhiệm

```text
Umbraco login → User Groups (lớp 1) → có / không thấy section + SPA
HTTP API → JWT + assignment Platform (lớp 2) → allow / deny
```

**Nguyên tắc:** lớp 1 = vào UI; lớp 2 = đúng vai trò và đúng dữ liệu. Không thay Umbraco groups cho toàn RBAC eCommerce; cũng không coi “Admin Umbraco” là đủ mọi quyền API.

## Multi-Tenant Architecture (Tenant = Siêu thị)

Mỗi **Siêu thị (Tenant)** có:
- **1 Umbraco instance** chạy trong Docker container riêng
- **1 SQL Server database** — hoàn toàn isolated giữa các Siêu thị
- **1 Elasticsearch index prefix** — `dcms-{tenantId}-*`
- **N Next.js storefronts** — 1 per Store, routing qua subdomain hoặc custom domain

**Tenant + Store resolution flow:**
```
HTTP Request (domain: lotte-thu-duc.com)
  → TenantMiddleware
  → check Redis cache: domain → {tenantId, storeId}
  → cache miss: query Platform DB DomainRouting table
  → cache result in Redis (TTL: 5 phút)
  → set HttpContext.Items["TenantId"] + HttpContext.Items["StoreId"]
  → route request đến Umbraco instance của Tenant
  → tất cả downstream queries scoped theo tenantId + storeId
```

**Hierarchy trong Platform DB:**
```
Siêu thị:   id · name · plan · umbracoUrl · dbConn · billingInfo
Brands:     id · tenantId · name · logo · settings
Stores:     id · brandId · tenantId · name · domain · slug · status
StoreUsers: id · userId · tenantId · brandId? · storeId? · roles[]
            (null brandId/storeId = access toàn Siêu thị)
```

## External Services

| Service | Vai trò | Integration |
|---|---|---|
| External Payment Gateway | Process payments | REST API qua API Gateway, không lưu payment data |
| Email Provider | Order confirmation, notifications | SMTP hoặc Email API (SendGrid/AWS SES) |
| Elasticsearch | Product search, content search | Elasticsearch .NET client |

## Environment Variables

| Variable | Mô tả |
|---|---|
| `UMBRACO_DB_CONNECTION` | SQL Server connection string |
| `ELASTICSEARCH_URL` | Elasticsearch cluster URL |
| `PAYMENT_GATEWAY_URL` | External payment API Gateway URL |
| `PAYMENT_GATEWAY_KEY` | API key cho payment gateway |
| `UMBRACO_API_KEY` | Key để Next.js gọi Content Delivery API |
| `NEXT_PUBLIC_API_URL` | Commerce API URL (public, cho client-side calls) |
| `EMAIL_SMTP_HOST` | SMTP host cho email notifications |
| `JWT_SECRET` | Secret cho JWT signing (RBAC auth) |
