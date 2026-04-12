# Business Requirements Document (BRD)

## dCMS — Headless Commerce & CMS Platform (Siêu thị / Multi-tenant)

| | |
|---|---|
| **Document version** | 1.0 |
| **Date** | 2026-04-12 |
| **Status** | Draft → Review |
| **Product** | dCMS |
| **Audience** | Product, Engineering, Operations, Compliance |

---

## 1. Executive Summary

dCMS là nền tảng **eCommerce + CMS headless** theo mô hình **siêu thị (retail chain)**. Một **Siêu thị** đóng vai trò **tenant** cô lập dữ liệu và vận hành; bên trong mỗi Siêu thị có **Brands** (thương hiệu con) và **Stores** (cửa hàng / kênh bán). Hệ thống cung cấp **Commerce API (REST)** cho storefront và đối tác, **tìm kiếm** qua Elasticsearch, **tồn kho** qua dịch vụ Inventory, **đồng bộ sự kiện** qua outbox + message bus, và lộ trình **Umbraco** cho backoffice nội dung theo từng Siêu thị.

**Giai đoạn hiện tại (MVP kỹ thuật):** các microservice **Catalog** và **Inventory** (ASP.NET Core, Dapper, PostgreSQL), **Catalog Worker** (outbox → RabbitMQ, index Elasticsearch), CORS + rate limiting trên API, envelope JSON chuẩn `{ data, meta, error }`. **Storefront Next.js** và **Umbraco host** nằm trong roadmap Phase 2 nhưng được BRD này mô tả ở mức nghiệp vụ để thiết kế API và dữ liệu thống nhất.

---

## 2. Objectives

| ID | Objective | Success criteria (measurable) |
|----|-----------|-------------------------------|
| O1 | **Multi-tenant an toàn** — dữ liệu Siêu thị A không lộ sang Siêu thị B | 100% API persistence scoped `TenantId` (+ `StoreId` khi áp dụng); kiểm thử isolation |
| O2 | **Headless commerce** — quản SPU/SKU, slug, trạng thái, biến thể qua API | CRUD/lifecycle sản phẩm qua Catalog API; variant theo combination hash |
| O3 | **Tồn kho chính xác** — điều chỉnh, giữ chỗ, hoàn chỉnh giao dịch | API Inventory với optimistic concurrency; không âm kho khả dụng khi reserve |
| O4 | **Đồng bộ tìm kiếm & tích hợp** — sự kiện domain xuất bản tin cậy | Outbox transactional; consumer index ES; retry + dead-letter có quan sát |
| O5 | **Vận hành 2000 CCU** (mục tiêu kiến trúc) | Không N+1 nóng; rate limit; connection pooling; horizontal scale stateless API |
| O6 | **Đa ngôn ngữ / đa tiền tệ** sẵn sàng từ đầu | Model JSON đa ngôn ngữ cho tên/mô tả; tiền tệ theo spec riêng (integer minor units) |
| O7 | **Thanh toán an toàn** | Không xử lý thẻ/trực tiếp trong dCMS — chỉ qua **API Gateway / Payment external** |

---

## 3. Scope

### 3.1 In scope (hiện tại hoặc BRD làm cơ sở triển khai ngay)

- **Catalog service:** sản phẩm (SPU), slug theo store, category, variants (SKU), trạng thái vòng đời (Draft → … → Active/Archived), domain events, outbox.
- **Inventory service:** kho theo `Warehouse`, tồn theo `VariantId` + `WarehouseId`, điều chỉnh / reserve / release, movement log, outbox `StockUpdated`.
- **Worker:** relay outbox Catalog + Inventory → RabbitMQ; consumer index Elasticsearch (product + stock); cảnh báo Slack tùy chọn khi có dead-letter.
- **Hạ tầng dev:** Docker Compose (PostgreSQL, RabbitMQ, Elasticsearch), migration SQL versioned.
- **Bảo mật tầng API:** CORS theo cấu hình, rate limiting toàn cục, correlation id (nền tảng cho trace).

### 3.2 In scope (lộ trình — thiết kế BRD, triển khai theo sprint)

- **AuthN/AuthZ:** JWT + RBAC động (Super Admin → … → Store Staff).
- **Platform DB:** đăng ký Siêu thị, Brands, Stores, domain routing (cache Redis).
- **Umbraco** per Siêu thị: backoffice + Content Delivery API.
- **Next.js storefront** per Store (Phase 2).
- **Order / Cart / Checkout** qua Commerce API + payment gateway.
- **Audit log, notification, approval** (bảng minimal đã có migration placeholder).

### 3.3 Out of scope (explicit)

- Xử lý **PCI / token thẻ** nội bộ dCMS.
- **Umbraco** chi tiết cấu hình content type trong BRD này (tham chiếu skill/workflow riêng).
- **SLA pháp lý** từng quốc gia (GDPR text) — chỉ ghi NFR “tuân thủ policy nội bộ”.

---

## 4. Business Process

### 4.1 Chuẩn bị Siêu thị & Store (platform)

1. Super Admin / Chain Admin tạo **Siêu thị** (tenant) trên platform.
2. Gán **Brands** và **Stores**; cấu hình domain → `tenantId` + `storeId`.
3. Provision DB PostgreSQL (hoặc schema) và connection cho Catalog/Inventory **theo policy triển khai** (shared cluster + logical isolation hoặc DB vật lý per tenant — quyết định kiến trúc triển khai).

### 4.2 Quản trị danh mục (Catalog)

1. Store Manager / Brand Manager tạo **Category** (cây, sort order).
2. Tạo **Product (SPU)** thuộc `TenantId` + `StoreId`, gắn category, tên/mô tả đa ngôn ngữ (JSON), slug unique trong store.
3. Sinh **Variants (SKU)** theo trục thuộc tính (generator domain); `CombinationHash` unique per product.
4. Chuyển trạng thái: Draft → Pending approval → Active (Publish) / Hidden / Archived; mỗi bước có thể phát **domain event** + ghi **Outbox** trong cùng transaction với thay đổi aggregate.

### 4.3 Tồn kho (Inventory)

1. Định nghĩa **Warehouse** thuộc `TenantId` + `StoreId`.
2. Gán **VariantStock** (số lượng, reserved, revision optimistic lock).
3. **Adjust** (delta), **Reserve**, **Release** — ghi **StockMovement** + event outbox **StockUpdated.v1** trong transaction.
4. Worker publish message; hệ thống search / storefront nhận cập nhật tồn.

### 4.4 Đồng bộ & lỗi (Outbox / DLQ)

1. Host worker poll `OutboxEvents` (batch), publish lên bus.
2. Thành công → `ProcessedAt` set; thất bại → tăng `RetryCount`, ghi lỗi; ≥ ngưỡng (ví dụ 5) → **DeadLetterEvents** + đánh dấu outbox đã xử lý để không kẹt hàng đợi.
3. Cảnh báo Slack (tùy cấu hình) khi DLQ depth > 0.

### 4.5 storefront (Phase 2 — nghiệp vụ)

1. Khách duyệt ES-backed catalog (filter `tenantId`, `storeId`, `brandId`).
2. Thêm giỏ / đặt hàng qua Commerce API; thanh toán redirect qua gateway.

---

## 5. Functional Requirements

### 5.1 Catalog

| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| CAT-01 | API lấy chi tiết product theo `productId` + `tenantId` (scoped) | Must |
| CAT-02 | Kiểm tra slug tồn tại / slug trùng sản phẩm khác trong cùng store | Must |
| CAT-03 | Lưu product + enqueue outbox events domain trong **một transaction** | Must |
| CAT-04 | Liệt kê variants của product theo `tenantId` + `storeId` | Must |
| CAT-05 | Sinh variants mới (bulk insert) + cập nhật product + outbox | Must |
| CAT-06 | Resolve `productId` từ `variantId` (cho luồng liên dịch vụ) | Must |
| CAT-07 | Health endpoint JSON envelope | Must |

### 5.2 Inventory

| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| INV-01 | API **adjust** stock (`delta`) theo tenant/store/variant/warehouse | Must |
| INV-02 | API **reserve** / **release** với quantity dương | Must |
| INV-03 | Không cho reserve vượt **available** = quantity − reserved | Must |
| INV-04 | Retry khi **conflict** optimistic (revision / row version) với backoff policy ở tầng service | Should |
| INV-05 | Ghi movement + outbox **StockUpdated** atomic với cập nhật tồn | Must |
| INV-06 | Health endpoint JSON envelope | Must |

### 5.3 Worker & Search

| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| WRK-01 | Relay outbox Catalog → bus (theo batch) | Must |
| WRK-02 | Relay outbox Inventory → bus | Must |
| WRK-03 | Consumer cập nhật chỉ mục Elasticsearch product (created/updated/published/archived) | Must |
| WRK-04 | Consumer cập nhật chỉ mục kho khi `StockUpdated` | Must |
| WRK-05 | Dead-letter sau N lần thử; optional Slack notifier | Should |

### 5.4 Cross-cutting API

| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| API-01 | Response envelope `{ data, meta, error }` thống nhất | Must |
| API-02 | CORS theo whitelist origin | Must |
| API-03 | Rate limiting global (configurable permit/window) | Must |
| API-04 | Correlation ID middleware | Should |

### 5.5 Future (tracked, not MVP code-complete)

| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| SEC-01 | JWT validation + RBAC dynamic per route | Must (roadmap) |
| PLT-01 | Platform registry Siêu thị / Brand / Store | Must (roadmap) |
| ORD-01 | Order service + saga (BRD chi tiết tách document) | Should |

---

## 6. Business Rules

| BR-ID | Rule |
|-------|------|
| BR-01 | **Tenant = Siêu thị:** mọi truy vấn ghi/đọc Catalog/Inventory phải include `TenantId` phù hợp ngữ cảnh xác thực. |
| BR-02 | **Store scope:** Product và thao tác slug scoped theo `StoreId` trong tenant. |
| BR-03 | **Slug unique:** `(StoreId, Slug)` unique toàn hệ thống trong DB catalog. |
| BR-04 | **Variant uniqueness:** `(ProductId, CombinationHash)` unique. |
| BR-05 | **Stock invariant:** `Quantity ≥ 0`, `ReservedQuantity ≥ 0`, `ReservedQuantity ≤ Quantity`. |
| BR-06 | **Reserve:** không vượt `AvailableQuantity = Quantity − ReservedQuantity`. |
| BR-07 | **Optimistic concurrency:** cập nhật tồn chỉ khi `Revision` khớp; nếu không → 409 Conflict (client refetch và thử lại). |
| BR-08 | **Outbox:** event chỉ được coi là đã publish khi relay set `ProcessedAt` (hoặc DLQ sau max retry). |
| BR-09 | **Payment:** dCMS không lưu PAN/CVV; chỉ token/reference do gateway cấp qua integration layer. |
| BR-10 | **Multi-language:** `Name` / `Description` lưu dạng JSON đa locale; rule validate tối thiểu locale bắt buộc (theo policy store). |

---

## 7. Non-functional Requirements

| NFR-ID | Category | Requirement |
|--------|----------|-------------|
| NFR-P01 | Performance | Mục tiêu ~**2000 CCU**; tránh N+1; batch query; pooling DB. |
| NFR-P02 | Scalability | API stateless; worker scale horizontal; ES cluster tách. |
| NFR-S01 | Security | Input validation; parameterized SQL; không lộ stack trace ra client. |
| NFR-S02 | Security | RBAC dynamic (khi bật auth); principle of least privilege per role. |
| NFR-S03 | Rate / Abuse | Global rate limit + (sau này) per-tenant limit. |
| NFR-R01 | Reliability | Outbox pattern đảm bảo at-least-once publish; consumer idempotent. |
| NFR-R02 | Observability | Correlation ID; structured logs; metrics (roadmap). |
| NFR-C01 | Compliance | Audit log table (mở rộng); PII handling theo policy. |
| NFR-I18n | i18n / currency | Không hardcode một locale; tiền integer minor unit (spec tài chính). |
| NFR-D01 | Deploy | Docker images; rolling-safe (no sticky in-memory session state for critical path). |

---

## 8. Appendix

### 8.1 Data model (tables — high level)

**Catalog database (`dcms_catalog` logical)**

| Table | Purpose |
|-------|---------|
| `Categories` | Cây danh mục theo tenant; path/depth/slug/sort |
| `Products` | SPU: tenant, store, category, i18n name/description, slug, status, sales aggregate, timestamps |
| `ProductVariants` | SKU: product ref, SKU, combination hash, status, sort |
| `OutboxEvents` | Event type, payload, created/processed, retry, error |
| `DeadLetterEvents` | Outbox failures không xử lý được sau retry |
| `AuditLogs` | Audit trail (minimal — mở rộng) |
| `ApprovalComments` | Luồng duyệt sản phẩm (minimal) |
| `NotificationEvents` | Thông báo người dùng (minimal) |

**Inventory database (`dcms_inventory` logical)**

| Table | Purpose |
|-------|---------|
| `Warehouses` | Kho theo tenant + store |
| `VariantStock` | Tồn variant tại warehouse; `Revision` optimistic lock |
| `StockMovements` | Lịch sử delta (adjust/reserve/release…) |
| `OutboxEvents` | Outbox riêng inventory (StockUpdated, …) |
| `DeadLetterEvents` | DLQ inventory |

**Elasticsearch (logical index)**

| Pattern | Purpose |
|---------|---------|
| `dcms-{tenantId}-*` | Index theo tenant; query filter `brandId` / `storeId` |

### 8.2 Glossary

| Term | Definition |
|------|------------|
| **Siêu thị** | Tenant độc lập — tổ chức bán lẻ cấp cao nhất được isolate dữ liệu. |
| **TenantId** | Định danh Siêu thị trong API/DB. |
| **Brand** | Thương hiệu con trong Siêu thị; không isolated DB riêng trong mô hình hiện tại. |
| **Store** | Kênh/cửa hàng bán; đơn vị slug và nhiều API scoped theo `StoreId`. |
| **SPU** | Standard Product Unit — `Product` trong Catalog. |
| **SKU** | Stock Keeping Unit — `ProductVariant`. |
| **CombinationHash** | Hash canonical của tập giá trị thuộc tính để định danh biến thể. |
| **Outbox** | Bảng ghi sự kiện cùng transaction với thay đổi domain để publish async tin cậy. |
| **Dead letter** | Sự kiện thất bại vượt ngưỡng retry, cần can thiệp thủ công/replay. |
| **Revision / RowVersion** | Token optimistic concurrency cho dòng `VariantStock` (PostgreSQL: cột `Revision`). |
| **Envelope** | JSON `{ data, meta, error }` cho response API. |

---

## Document control

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-04-12 | Engineering | Initial BRD aligned với codebase Catalog/Inventory/Worker + kiến trúc tài liệu dCMS |
