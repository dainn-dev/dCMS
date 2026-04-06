# Architectural Decisions

_Thêm decisions vào đây khi chúng được đưa ra._

---

## Decision: Umbraco CMS làm nền tảng backend

**Date:** 2026-04-03
**Decision:** Dùng Umbraco CMS (ASP.NET Core) làm core CMS với custom eCommerce layer xây dựng trên đó.
**Reason:** Umbraco cung cấp backoffice mạnh cho content management, hỗ trợ headless qua Content Delivery API, ecosystem .NET mature, và có thể extend tự do cho eCommerce features.
**Alternatives considered:** Shopify (không đủ flexibility), WooCommerce (PHP stack), custom từ đầu (tốn thời gian hơn)

---

## Decision: Mô hình Siêu thị — Tenant = Siêu thị, hierarchy 4 cấp

**Date:** 2026-04-03 (updated 2026-04-03)
**Decision:** dCMS theo mô hình siêu thị với hierarchy 4 cấp: **Platform → Siêu thị (Tenant) → Brands → Stores**. Tenant isolation ở cấp Siêu thị — 1 Umbraco instance + 1 SQL Server DB per Siêu thị. Brand là layer tổ chức bên trong. Store là đơn vị bán hàng có storefront riêng.
**Reason:** Phản ánh đúng thực tế kinh doanh — một tập đoàn bán lẻ (Lotte, BigC) có nhiều thương hiệu con, mỗi thương hiệu có nhiều cửa hàng. Isolation tại cấp Siêu thị đủ đảm bảo data security giữa các tập đoàn khác nhau. Brands/Stores trong cùng Siêu thị share Umbraco instance → giảm chi phí vận hành, dễ cross-brand reporting.
**Alternatives considered:**
- Isolation per Store (quá tốn resource, khó cross-brand analytics)
- Isolation per Brand (mất layer tổ chức ở cấp tập đoàn)
- Flat model không có Brand (không phản ánh thực tế retail)

---

## Decision: Headless architecture với Next.js storefront

**Date:** 2026-04-03
**Decision:** Umbraco chạy headless mode, Next.js fetch data qua Content Delivery API và custom Commerce API.
**Reason:** Tách biệt frontend và backend cho phép storefront scale độc lập, frontend developer tự do về UI, SEO tốt hơn với Next.js SSR/SSG.
**Alternatives considered:** Umbraco với Razor Views (tightly coupled, khó scale frontend)

---

## Decision: Elasticsearch cho search

**Date:** 2026-04-03
**Decision:** Dùng Elasticsearch cho toàn bộ product search và filtering.
**Reason:** Full-text search mạnh, hỗ trợ faceted filtering phức tạp, scalable, phù hợp với eCommerce catalog lớn. SQL Server full-text search không đủ mạnh cho eCommerce.
**Alternatives considered:** SQL Server full-text search (limited), Algolia (vendor lock-in + cost)

---

## Decision: Payment qua API Gateway external

**Date:** 2026-04-03
**Decision:** dCMS không xử lý payment trực tiếp — mọi payment request được proxy qua API Gateway external.
**Reason:** Giảm PCI compliance scope, tách biệt concern, dễ đổi payment provider, bảo mật tốt hơn khi không lưu payment data nhạy cảm trong hệ thống.
**Alternatives considered:** Tích hợp trực tiếp Stripe/PayPal SDK (tăng compliance burden)

---

## Decision: Platform DB tách riêng để quản lý Siêu thị/Brand/Store registry

**Date:** 2026-04-03 (updated 2026-04-03)
**Decision:** Platform DB riêng biệt lưu: Siêu thị registry (kèm Umbraco URL + DB connection), Brands, Stores, domain routing (domain → tenantId + storeId), billing, StoreUsers với role per scope (tenant/brand/store level).
**Reason:** Platform-level data phải tồn tại độc lập với business data của từng Siêu thị. Super Admin cần quản lý cross-tenant mà không access vào từng Umbraco instance. Domain routing phải resolve cực nhanh (< 5ms) từ 1 điểm trung tâm — thường được cache vào Redis.
**Alternatives considered:** Lưu trong Umbraco DB của từng tenant (không thể cross-query), lưu trong config file (không scale), service discovery thuần túy (thiếu business metadata)

---

## Decision: RBAC với dynamic roles, scoped theo cấp (Tenant / Brand / Store)

**Date:** 2026-04-03 (updated 2026-04-03)
**Decision:** RBAC với dynamic role assignment, scoped theo 3 cấp: Tenant-level (Siêu thị), Brand-level, Store-level. Một user có thể có role khác nhau ở các cấp khác nhau.
**Reason:** Hierarchy 4 cấp đòi hỏi phân quyền tương ứng. Chain Admin cần full access toàn Siêu thị. Brand Manager chỉ quản lý Brand được assign. Store Staff chỉ thao tác Store cụ thể.
**Role hierarchy:**
- `SuperAdmin` — platform-wide, quản lý tất cả Siêu thị
- `ChainAdmin` — full access toàn bộ 1 Siêu thị (tất cả Brands + Stores)
- `BrandManager` — quản lý 1 Brand và tất cả Stores trong Brand đó
- `StoreManager` — quản lý products, orders, content của 1 Store cụ thể
- `StoreStaff` — view orders, process fulfillment, limited actions
**Scope resolution:** JWT token chứa `{ tenantId, brandId?, storeId?, roles[] }` — middleware verify scope trước khi cho phép action
**Alternatives considered:** Flat role model (không phản ánh hierarchy), Umbraco built-in groups (không cross-brand/cross-store), hardcoded roles (không scale)

---

## Decision: Docker cho deployment

**Date:** 2026-04-03
**Decision:** Toàn bộ stack được containerize bằng Docker, orchestrate bằng docker-compose cho local dev.
**Reason:** Consistency giữa environments, dễ scale horizontally, isolate dependencies, CI/CD đơn giản hơn với Docker images.
**Alternatives considered:** IIS trực tiếp (Windows only, khó scale), Azure App Service (vendor lock-in)

---

## Decision: Lưu giá tiền dưới dạng integer (smallest currency unit)

**Date:** 2026-04-03
**Decision:** Tất cả giá trị tiền được lưu dưới dạng integer — đơn vị nhỏ nhất của currency (cents cho USD, đồng cho VND).
**Reason:** Tránh floating point precision errors trong tính toán tiền tệ, standard practice trong eCommerce.
**Alternatives considered:** Decimal (vẫn có precision risk), float (không dùng trong financial systems)
