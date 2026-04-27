# Follow-up — Order aggregate promotion persistence (DAI-693 part 2)

**Status:** done (2026-04-27)
**Created:** 2026-04-27
**Parent epic:** DAI-679 (Promotions Rule Engine Evaluator)
**Predecessor:** DAI-693 part 1 — typed `IPromotionsClient`, evaluate-call wiring in `CreateOrder`, saga side-effect consumers (`OrderRedemptionConfirmConsumer` / `OrderRedemptionReleaseConsumer`), migration `018_AddOrderPromotionSnapshot.sql`.

---

## Why this is a separate ticket

DAI-693 part 1 landed the integration boundary (HTTP client, evaluate hook in route, saga consumers) without touching the `Order` aggregate. Persisting `LineDiscount` / `OrderDiscount` / `OrderPromotions` rows requires changes that cut across:

- `dCMS.Order.Core.Domain.Order` and `OrderItem` aggregates
- `IOrderRepository` / `OrderService.CreateOrderAsync`
- `OrderUnitOfWork.SaveOrderAsync` SQL
- The `CreateOrderCommand` → adjustments flow inside `OrderHttpRoutes`
- Read-model projections (`OrderQueryStore`, `OrderReportQueryStore`)

This is too cross-cutting to bundle with the integration scaffolding without bloating the diff and the review surface. Splitting keeps each change reviewable and reversible.

---

## Scope

### 1. Aggregate fields

- `OrderItem.LineDiscount: Money` — non-negative, ≤ `UnitPrice * Quantity`. Default 0.
- `Order.OrderDiscount: Money` — non-negative, ≤ subtotal. Default 0.
- `Order.PromoCode: string?` and `Order.PromoCodeId: string?` — set when a code was applied + accepted.
- `OrderItem.LineTotal()` adjusted: `(UnitPrice * Quantity) - LineDiscount`.
- `Order.Total` computation: sum(line totals) - OrderDiscount (no negatives).

### 2. Command flow

- Extend `CreateOrderCommand` with `LineAdjustments[]`, `OrderAdjustments[]`, `AppliedPromotions[]`, `PromoCode`, `PromoCodeId` (all optional).
- `OrderHttpRoutes.EvaluatePromotionsAsync` returns the `EvaluateResponse` instead of `IResult?`; the caller maps `LineAdjustments` to the matching `CreateOrderLine` (by `lineId`) and threads everything into the command.
- `OrderService.CreateOrderAsync` builds the aggregate using the new fields.

### 3. Persistence (single transaction with `Orders` insert)

- `OrderUnitOfWork.SaveOrderAsync` extended:
  - `INSERT INTO "Orders" (..., "OrderDiscount", "PromoCode", "PromoCodeId")`
  - `INSERT INTO "OrderItems" (..., "LineDiscount")`
  - For each applied promotion: `INSERT INTO "OrderPromotions" (Id, TenantId, OrderId, CampaignId, EditorKind, Name, Amount, PromoCode)`.
- Update `OrderQueryStore` projection mapping to surface the new fields in read responses.

### 4. Saga side-effects light-up

Once the snapshot rows are populated, the existing `OrderRedemptionConfirmConsumer` / `OrderRedemptionReleaseConsumer` consumers begin firing real Promotions HTTP calls. No code change needed there — they already read from `OrderPromotions` and degrade to no-op when empty. Verify via integration test (below).

### 5. Read API surface

- Add `orderDiscount`, `lineDiscount`, `appliedPromotions[]`, `promoCode` to GET `/api/orders/{orderId}` and list responses.
- Decide whether to expose `PromoCodeId` (internal) or keep it private. Default: keep private.

### 6. Integration test — `OrderPromotionIntegrationTests.cs`

Spin up Promotions.Api fixture + Order.Api fixture (Postgres + RabbitMQ via Testcontainers) and assert:

1. Active campaign + cart → POST order → `Orders.OrderDiscount > 0`, `OrderPromotions` row persisted.
2. PromoCode applied → `Orders.PromoCode` and `PromoCodeId` set.
3. Order saga reaches `Confirmed` → `PromoCodeRedemptions` row in Promotions DB with `Status='confirmed'`.
4. Order saga reaches `Cancelled` → same redemption flipped to `Status='released'`.
5. Re-delivery of `OrderPaymentSettledV1` (via MassTransit redelivery) does not insert a duplicate redemption (UNIQUE constraint).
6. Empty `OrderPromotions` → consumers fire no Promotions HTTP call (assert via mock client wired in test).

### 7. Feature flag rollout

- `Promotions:Required=false` in dev, `true` in staging/prod (already wired in route's fail-open/closed).
- Add gauge metric `dcms_orders_promotions_applied_total` and counter `dcms_promotions_evaluate_failures_total{mode=fail-open|fail-closed}` to OpenTelemetry pipeline.

---

## Out of scope

- Splitting `dcms_promotions` from `dcms_catalog` DB — separate refactor ticket. Connection-string indirection is already in place: `ConnectionStrings:Promotions` falls back to `Catalog`.
- Per-line tax recomputation when discounts apply (current `Order` model uses `TaxTotal=0`).
- Customer-facing receipt/email rendering of applied promotions.
- Refund / partial-cancel flow → release proportional redemption amount (separate flow, intersects with `dCMS.Order.Api/refunds`).

---

## Files expected to change

| Path | Change |
|---|---|
| `src/backend/dCMS.Order.Core/Domain/Order.cs` | add `OrderDiscount`, `PromoCode`, `PromoCodeId`, recompute `Total` |
| `src/backend/dCMS.Order.Core/Domain/OrderItem.cs` | add `LineDiscount`, adjust `LineTotal()` |
| `src/backend/dCMS.Order.Core/Ordering/CreateOrderCommand.cs` | add adjustment + applied-promotion fields |
| `src/backend/dCMS.Order.Infrastructure/Services/OrderService.cs` | thread fields into aggregate |
| `src/backend/dCMS.Order.Infrastructure/Persistence/OrderUnitOfWork.cs` | extend SaveOrderAsync + write `OrderPromotions` |
| `src/backend/dCMS.Order.Infrastructure/Persistence/OrderQueryStore.cs` | project new columns |
| `src/backend/dCMS.Order.Api/Routes/OrderHttpRoutes.cs` | thread adjustments into command, expose in GET |
| `src/backend/dCMS.Order.Tests/Integration/OrderPromotionIntegrationTests.cs` | new |

---

## Verification checklist (CLAUDE.md)

- [x] Tenant isolation in every new SQL (`WHERE TenantId = @TenantId`) — all reads in `OrderQueryStore` filter by tenant; `OrderPromotions` rows always carry `TenantId` and the bulk `UNNEST` insert sets it per row
- [x] `OrderDiscount` + `LineDiscount` clamped at non-negative; `Total` cannot go below 0 — see `OrderAggregateTests.Total_clamps_at_zero_when_full_discount_applied` + invariant tests
- [x] No N+1 — `OrderPromotions` insert uses single-batch `INSERT … SELECT * FROM UNNEST(...)` (DAI-693, see `OrderUnitOfWork.SaveOrderAsync`)
- [x] Aggregate invariants enforced in `Order` constructor (not in route layer) — `Order.Create` / `OrderItem` ctor throw `ArgumentOutOfRangeException`; tests `Create_with_negative_order_discount_throws`, `Create_with_order_discount_above_subtotal_throws`, `OrderItem_with_…_throws`
- [x] Backward compat: orders created before migration 018 keep `OrderDiscount=0`, `LineDiscount=0` — `NOT NULL DEFAULT 0` in `018_AddOrderPromotionSnapshot.sql`
- [x] Saga retry of `OrderPaymentSettledV1` is idempotent end-to-end — UNIQUE `UX_PromoCodeRedemptions_Tenant_Code_Order` in migration `024`
- [x] Feature flag `Promotions:Required` covered by code path — fail-closed returns 503 PROMOTIONS_UNAVAILABLE; fail-open logs warning and proceeds; both increment `dcms_promotions_evaluate_failures_total{mode}`
