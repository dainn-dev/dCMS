# Architectural Decisions

---

## Decision: Order workflow state machine

**Date:** 2026-04-25

**Decision:** Order lifecycle được model hóa như state machine gồm Order-level + Item-level statuses.

**Reason:** Legacy system cho thấy item-level fulfillment cực quan trọng với partial shipment / cancellation.

**Statuses:**
Open, ReadyForDelivery, Processing, Delivered, PickedUp, Returned, Cancelled, PartialFulfilled.

---

## Decision: Promotions engine rule-based

**Date:** 2026-04-25

**Decision:** Promotions implement bằng rule engine thay vì hardcode.

**Reason:** Hỗ trợ nhiều loại promotion:
- Promo code
- Mix & Match
- PWP
- Product Discount
- After Sales Promo

---

## Decision: Fulfillment domain tách riêng khỏi Orders

**Date:** 2026-04-25

**Decision:** Fulfillment aggregate riêng gồm:
DeliveryMethod, Slots, PickupLocation, LogisticPartner, Tracking.

**Reason:** Delivery logic evolve độc lập với order/payment.

---

## Decision: Approval workflow genericized

**Date:** 2026-04-25

**Decision:** Dùng generic approval engine cho Product, Content, Campaign, PromoCode.

**Reason:** Legacy system có nhiều approval module trùng pattern.

States:
Draft → PendingApproval → Approved / Rejected

---

## Decision: Reporting read-model architecture

**Date:** 2026-04-25

**Decision:** Reports chạy trên read models / analytics DB thay vì OLTP.

**Reason:** Sales/report queries nặng, không nên impact transactional DB.

---

## Decision: Bulk import/export pipeline async

**Date:** 2026-04-25

**Decision:** Product/Image/Inventory import chạy background jobs.

**Reason:** Legacy system có bulk operations lớn, sync request không scale.

---

## Decision: Multi-payment support per order

**Date:** 2026-04-25 (refined 2026-04-27 with DAI-722)

**Decision:** Một order có thể chứa nhiều payment components: Gateway + Voucher + LoyaltyPoints + GiftCard. Schema: `OrderPayments` (1:1 với `Orders` qua UNIQUE) + `PaymentComponents` (1:N, ordered). Sum invariant `Σ Amount = OrderPayment.Total` enforced ở application code (DAI-722) khi save và khi recompute status — KHÔNG dùng DB trigger để giữ migration nhẹ. Component apply order khi capture: **Voucher → LoyaltyPoints → GiftCard → Gateway** (Vouchers/loyalty consume first, gateway absorbs remainder). Status `OrderPayment.Status` derived từ component states (`Captured` khi all captured, `Failed` khi any failed, `Authorized` khi all auth+, etc).

**Reason:** PDF system support multi payment. Voucher/loyalty là balance-bound nên consume trước để không waste khả dụng nếu gateway fail; gateway-last cho phép retry-only-gateway-leg khi card decline mà không phải re-reserve voucher.

---

## Decision: Content blocks schema-driven

**Date:** 2026-04-25

**Decision:** Homepage banners / product blocks / reusable sections dùng schema-driven block editor.

**Reason:** Marketing team cần tự build landing pages không cần dev.

---

## Decision: Search + admin filtering tách biệt

**Date:** 2026-04-25

**Decision:** Customer storefront search dùng Elasticsearch.  
Admin grids dùng SQL optimized filtering.

**Reason:** Admin cần exact filtering/export, storefront cần relevance ranking.

---

## Decision: Refund cases bounded context

**Date:** 2026-04-25

**Decision:** Refund Case là domain riêng linked với Orders.

**Reason:** Refund workflow có status/history khác order fulfillment.

---

## Decision: Audit trail bắt buộc cho admin actions

**Date:** 2026-04-25

**Decision:** Mọi thay đổi status, remarks, edits đều audit log.

**Reason:** Legacy guide ghi nhận remarks/history xuyên suốt workflow.

---

## Decision: Template system cho communications

**Date:** 2026-04-25

**Decision:** Email / admin / print templates là subsystem riêng.

**Reason:** Packing slip, receipts, confirmations cần customizable templates.

---

## Decision: Promotions evaluator + Order persistence (DAI-679)

**Date:** 2026-04-27

**Decision:** Order API gọi `dCMS.Promotions.Api /evaluate` ngay trước khi tạo lệnh; kết quả (`LineDiscount`, `OrderDiscount`, `PromoCode`, `PromoCodeId`, `AppliedPromotionSnapshot[]`) được persist trong `Order` aggregate qua migration `018_AddOrderPromotionSnapshot.sql`. Idempotency end-to-end được đảm bảo bởi UNIQUE `("TenantId","PromoCodeId","OrderId")` trên `PromoCodeRedemptions` (migration 024).

**Behaviour:**
- Feature flag `Promotions:Required` bật fail-closed (HTTP 503 PROMOTIONS_UNAVAILABLE) khi evaluate fail; mặc định fail-open (log warning + tiếp tục checkout).
- Bulk insert `OrderPromotions` qua `UNNEST(...)` (no N+1).
- Domain invariants: `OrderDiscount ∈ [0, lineSubtotal]`, `LineDiscount ∈ [0, grossLineTotal]`, `Total = max(0, subtotal − lineDiscounts − orderDiscount)`.
- Prometheus counters: `dcms_orders_promotions_applied_total{tenant}`, `dcms_promotions_evaluate_failures_total{tenant,mode}`.

**Reason:** Cần khả năng audit promotion áp dụng cho từng đơn (báo cáo, hoàn tiền, dispute) đồng thời decoupled khỏi Promotions service — Order service vẫn giữ snapshot đã được "ghim" tại thời điểm checkout.

---

## Decision: Bulk jobs MVP dùng Hangfire + SQL Server (DAI-684)

**Date:** 2026-04-27

**Decision:** Bulk import/export MVP trong Umbraco host (`dCMS.Web`) dùng **Hangfire** với **SQL Server storage** (cùng DB Umbraco). Trạng thái/progress job được lưu riêng trong bảng **`dcms_bulk_jobs`** (Umbraco migration plan `dCMS.Access`), và expose:
- Dashboard: `/umbraco/dcms/hangfire` (restrict by backoffice auth + optional header key)
- Backoffice API: `/umbraco/dcms/api/bulk-jobs/*`
- UI: eStore → Products → **Bulk jobs**

**Progress:** report theo percent (`ProgressProcessed/Total/Percent`) cập nhật theo batch để tránh write-churn.

**Reason:** Backoffice-driven bulk ops cần durable jobs + retry/cancel/monitor (UI “must-have”), vận hành đơn giản khi đặt job storage chung Umbraco SQL Server; đồng thời tránh serialize payload lớn vào Hangfire bằng cách lưu input/output dưới dạng file refs.

---

## Decision: Reports analytics DB tách riêng (DAI-709 / DAI-685)

**Date:** 2026-04-27

**Decision:** Dùng **PostgreSQL analytics DB riêng** (connection `ConnectionStrings:Analytics`) để chứa read-model cho reporting. Projection worker consume Order lifecycle messages (MassTransit) và upsert vào `analytics.*` tables; Reports API chỉ query analytics DB (không chạm OLTP).

**Reason:** Achieve workload isolation (report queries không ảnh hưởng transactional), vẫn giữ được SQL workflow quen thuộc, và dễ scale read independently. MVP tránh ops overhead của columnar; tách DB tốt hơn so với analytics schema trong OLTP.

---

## Decision: Umbraco doctype versioning via uSync.Complete (DAI-686)

**Date:** 2026-04-27

**Decision:** Doctype/datatype/template state của Umbraco được version-control bằng **uSync.Complete 16.1.2** (file-based, dump vào `uSync/v16/`). Production **không** import-on-startup; Development có thể bật `ImportAtStartup=All` + `ExportOnSave`. CI workflow (`.github/workflows/usync-drift.yml`) chạy fresh-DB export và compare với committed files để gate drift.

**Tenant bootstrap:** CLI **`SpawnTenant`** (`src/backend/tools/SpawnTenant`) tạo tenant DB SQL Server (`IF DB_ID(...) IS NULL CREATE DATABASE`), ghi `infra/tenants/<tenant>.env` (Unattended install + `uSync__Settings__ImportAtStartup=All`), verify connection, optional compose-up + healthcheck wait.

**Doctype catalog:** 8 element/composition types đặc tả trong `docs/usync/doctypes-spec.md` (HomepageMainBanner, HomepageSubBanner, ProductBlock, NavigationMenu, LandingPage, WysiwygPage, ReusableSection, EmbeddedVideo); aliases PascalCase, tabs `Content/SEO/Settings`, Block Grid blocks documented per doctype.

**Reason:** Spec-only over hand-authored XML — DataType GUIDs + Umbraco version stamps cần round-trip an toàn từ một DB thật, viết tay XML rủi ro lệch và corrupt content. CLI tách provisioning ra khỏi Umbraco host để spawn tenant không cần manual install wizard.

---

## Decision: Email/Notification template engine — Scriban + Postgres + MassTransit (DAI-687)

**Date:** 2026-04-27

**Decision:** Notification.Api owns templates CRUD + render preview; Notification.Worker dispatches qua MassTransit `EmailQueuedV1`. **Scriban** là template engine (sandboxed: `EnableRelaxedMemberAccess=false`, `LoopLimit=10000`, `RecursiveLimit=128`, không expose .NET objects — chỉ JSON model bridge). Templates lưu Postgres (`Templates` table, migration `025_CreateTemplates.sql`) với 4-tier locale fallback (tenant+locale → tenant+default → global+locale → global+default) qua SQL CASE-ORDER + `LIMIT 1`. Email pipeline: `EmailQueuedConsumer` → `IIdempotencyService` lock + processed check → `EnsureDeliveryRowAsync` (queued) → render → MailKit SMTP với Polly 3-retry → `MarkSentAsync`/`MarkFailedAsync` (delivery log `EmailDeliveries`, migration `026`).

**Multi-tenancy unique scope:** unique index `(COALESCE("TenantId",''), "Key", "Locale", "Channel")` để `NULL` (global) và empty string collapse — upsert idempotent.

**Reason:** Scriban an toàn hơn Razor (no compile-then-execute, có loop/recursion limits), JSON-only model loại bỏ risk leak `.NET` reflection. Locale fallback ở SQL layer (CASE-ORDER) đơn giản hơn 4 round-trips và xử lý đúng ưu tiên ở mọi tenant.

**Tests:** `dCMS.Tests/Integration/Notification/TemplateRendererIntegrationTests.cs` — 9 Testcontainers tests covering 4 fallback tiers, model rendering, missing-template + parse-error fallback, COALESCE-tenant upsert idempotency.

---

## Decision: Generic Approval Engine — strategy pattern + central ApprovalRequest aggregate (DAI-688)

**Date:** 2026-04-27

**Decision:** Approval workflow tách khỏi mỗi entity domain (Product/Campaign/PromoCode/Content) bằng một service riêng `dCMS.Approval.Api` (port 5009) backed by `ApprovalRequests` table (migration `027_CreateApprovalRequests.sql`). Mỗi domain implement `IApprovalSubject` (`EntityType`, `ValidateAsync`, `ApplyAsync`) — registry resolve theo `EntityType` (case-insensitive). API: POST `/approvals` (Submit), POST `{id}/approve|reject|request-changes`, POST `bulk-approve`, GET list.

**Side effects:**
- Campaign/PromoCode subjects giữ legacy `WorkflowState` column in sync (compat layer cho 1 release)
- Product subject flip `Products.IsActive` (true on Approve, false on Reject/RequestChanges) — column thêm qua `028_AddProductActivation.sql`; ES indexer sẽ subscribe sau (DAI-720 follow-up)

**Reason:** Trước đó mỗi entity có cột `WorkflowState` riêng + state-machine ad-hoc trong service tương ứng — bulk approval gần như không thể, audit trail rời rạc. Centralizing vào một bảng + strategy pattern cho phép:
- Bulk approve cross-entity (UI một dashboard duy nhất)
- History JSONB log unified (`[{state, by, at, notes}]`)
- Thêm entity mới (Content/Banner) chỉ cần implement subject — không cần migration column mới

**State machine:** `Draft → PendingApproval → Approved | Rejected | ChangesRequested`. Transition guard tại `TryTransitionAsync(expectedState='PendingApproval', ...)` — optimistic concurrency.

**DAI-721 Content subject — webhook over in-process:** `ContentApprovalSubject` chạy trong `dCMS.Approval.Api` nhưng `IContentService` chỉ available trong Umbraco host (`dCMS.Web`). Thay vì duplicate registry, subject HTTP-POST tới `/umbraco/dcms/api/content-approval/{publish|unpublish}` với `X-Internal-Api-Key` (SHA-256 fixed-time compare). `dCMS.Web` host:
- `ContentPublishingApprovalHandler` (INotificationAsyncHandler) cancels publish khi doctype alias nằm trong `ContentApproval:ApprovalRequiredDoctypes`, submit approval request qua `ApprovalApiClient` → `dCMS.Approval.Api`.
- `ApprovalGate` (AsyncLocal flag) bật khi callback controller publish — handler thấy bypass, skip cancel ⇒ publish thực sự xảy ra. Loop-safe.

**Reason:** Tránh share registry cross-process; tận dụng cùng pattern `X-Internal-Api-Key` đã dùng cho Inventory internal endpoints; AsyncLocal bypass đơn giản hơn so với phân biệt "trigger by approval vs trigger by user" trong Umbraco notification context.