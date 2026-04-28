# DAI-689 Multi-Tender Payment — Remaining-Gap Design

**Date:** 2026-04-28
**Epic:** [DAI-689](https://linear.app/dainndev/issue/DAI-689) — Multi-payment / multi-tender per order
**Status:** Approved scope — closing the four remaining gaps in an otherwise-implemented epic.

---

## 1. Context

DAI-689 epic has three sub-stories, all of which are **substantially already implemented** in the codebase:

| Sub-story | What exists | Files |
|---|---|---|
| DAI-722 — `OrderPayment` schema | Domain aggregate (`OrderPayment.Plan`, sum invariant, ordering Voucher→Loyalty→GiftCard→Gateway), repo, query store, GET endpoint, 8 unit tests | `dCMS.Order.Core/Domain/Payments/`, `dCMS.Order.Infrastructure/Persistence/OrderPaymentRepository.cs`, migration `020_CreateOrderPaymentsMultiTender.sql` |
| DAI-723 — Voucher + Loyalty services | Both APIs scaffolded with reserve/capture/release/refund + holds + ledger + events | `dCMS.Voucher.Api/`, `dCMS.Loyalty.Api/`, `TenderMessageContracts.cs` |
| DAI-724 — Multi-tender saga | `PaymentOrchestrator` (consumes `ProcessPaymentV1`, walks components, inline compensation, idempotent dispatch log), `ReleasePaymentComponentsConsumer` (late cancels), saga publishes `ReleasePaymentComponentsV1` at every cancel transition, 3 orchestrator unit tests | `dCMS.Order.Infrastructure/Payments/PaymentOrchestrator.cs`, `ReleasePaymentComponentsConsumer.cs`, `Sagas/OrderSaga.cs` |

This design closes the **four concrete gaps** that prevent the epic from being declared complete.

---

## 2. Gaps and Goals

### Gap 1 — `PaymentComponent` overloads `ExternalRef`

`PaymentComponent.ExternalRef` is currently used for two different things at two different times:

- **Before reserve:** the voucher code (so the orchestrator can pass it to `IVoucherTenderClient.ReserveAsync`) — see [PaymentOrchestrator.cs:117](src/backend/dCMS.Order.Infrastructure/Payments/PaymentOrchestrator.cs#L117).
- **After authorize:** the `holdId` returned from Voucher.Api / Loyalty.Api.

This is fragile: the orchestrator has a TODO comment about it, and any retry between reserve and authorize would lose the voucher code. We add a separate `Reference` column (the *input* to the tender call) and keep `ExternalRef` strictly as the *output* (the holdId / chargeId).

### Gap 2 — Gateway component is a no-op

Today, when a `PaymentComponent` of type `Gateway` flows through `PaymentOrchestrator`:

- Reserve returns `TenderCallResult.Ok(null)` (line 122)
- Capture returns `TenderCallResult.Ok()` (line 134)

That is correct for the legacy single-tender path, but for multi-tender it means a customer who pays "Voucher 40 + Gateway 60" gets the voucher captured and the gateway 60 silently swallowed. Acceptance criterion AC1 ("Gateway charges remainder") is unmet.

We add `IGatewayTenderClient` that talks to the existing `dCMS.Payment.Api` `/internal/payment/...` surface and the existing `StubPaymentGateway` (`dCMS.Payment.Infrastructure`). This **reuses** the existing stub rather than duplicating it.

### Gap 3 — TTL cleanup worker for expired holds

`VoucherHolds` and `LoyaltyHolds` have an `ExpiresAt` column and indexes filtered by `Status = 'Held'`, but nothing scans them. A customer who abandons checkout permanently locks voucher balance / loyalty points.

We add a hosted background service in each of `Voucher.Api` and `Loyalty.Api` that polls every 60s for expired held rows and releases them through the existing release path so events / ledger / balances stay consistent.

### Gap 4 — Saga + ReleaseConsumer integration tests

We have orchestrator unit tests but no:

- End-to-end saga test (`OrderPlaced → ReserveStock → ProcessPayment → PaymentCompleted → Confirmed`) with a multi-tender plan.
- `ReleasePaymentComponentsConsumer` unit test (refund Captured, release Authorized, no-op Pending).
- Hold-expiry test for the new TTL workers.

---

## 3. Design

### 3.1 Schema change — `PaymentComponents.Reference`

New migration `021_AddPaymentComponentReference.sql`:

```sql
ALTER TABLE "PaymentComponents"
    ADD COLUMN "Reference" VARCHAR(128) NULL;

-- Backfill: existing rows in Pending state with a non-null ExternalRef are voucher codes
-- (the only pre-authorize use of ExternalRef). Move them.
UPDATE "PaymentComponents"
SET "Reference" = "ExternalRef", "ExternalRef" = NULL
WHERE "State" = 'Pending' AND "ExternalRef" IS NOT NULL;
```

Domain change in `PaymentComponent`:

- New `string? Reference` property set at construction (voucher code, customer id, etc.).
- Constructor becomes `(Guid id, PaymentComponentType type, decimal amount, int ordering, string? reference = null)`.
- `Authorize(string externalRef)` continues to set `ExternalRef` (now strictly the holdId/chargeId).

`OrderPayment.Plan` accepts an optional `Reference`:

```csharp
public static OrderPayment Plan(
    Guid orderId,
    decimal total,
    IEnumerable<(PaymentComponentType Type, decimal Amount, string? Reference)> tenders);
```

The orchestrator then reads `component.Reference` (not `ExternalRef`) when calling `IVoucherTenderClient.ReserveAsync`.

### 3.2 Gateway tender client

New `IGatewayTenderClient` in `dCMS.Order.Infrastructure/Payments/TenderClients.cs`:

```csharp
public interface IGatewayTenderClient
{
    Task<TenderCallResult> AuthorizeAsync(
        string tenantId, string storeId, string customerId,
        Guid orderId, decimal amount, string currency, CancellationToken ct);
    Task<TenderCallResult> CaptureAsync(string tenantId, string chargeRef, CancellationToken ct);
    Task<TenderCallResult> RefundAsync(string tenantId, string chargeRef, CancellationToken ct);
    Task<TenderCallResult> VoidAsync(string tenantId, string chargeRef, string reason, CancellationToken ct);
}
```

Two implementations:

- **`StubGatewayTenderClient`** (default in dev/tests). Behavior:
  - Default: success, returns `chargeRef = $"ch_stub_{orderId}"`.
  - `customerId` contains `decline` (case-insensitive) → `Fail("card_declined", ...)`.
  - `customerId` contains `timeout` → throws `TaskCanceledException` (transient → Polly retry → eventually fail).
  - `amount` ends in `.99` → `Fail("insufficient_funds", ...)`.
  - In-memory dictionary makes capture/refund idempotent on `chargeRef`.

- **`HttpGatewayTenderClient`** (prod). Calls `dCMS.Payment.Api`'s existing `/internal/payment/create-intent` for `Authorize`; capture/refund issue webhook-style POSTs to `/internal/payment/{intentId}/capture|refund`. (Adding two minimal endpoints to `Payment.Api` is in scope; they delegate to `IPaymentGateway.ProcessPaymentAsync` / `RefundPaymentAsync`.)

DI registration in `OrderServiceCollectionExtensions.AddTenderHttpClients`:

```csharp
if (configuration.GetValue("Payment:UseStubGateway", true))
    services.AddSingleton<IGatewayTenderClient, StubGatewayTenderClient>();
else
    services.AddHttpClient<IGatewayTenderClient, HttpGatewayTenderClient>(c => ...);
```

Default is **stub** (matches existing `StubPaymentGateway` default in `dCMS.Payment.Infrastructure/PaymentServiceCollectionExtensions.cs:18`).

### 3.3 Orchestrator wiring

`PaymentOrchestrator` constructor takes `IGatewayTenderClient`. The `DispatchReserveAsync` switch becomes:

```csharp
PaymentComponentType.Voucher        => _vouchers.ReserveAsync(msg.TenantId, component.Reference ?? "", orderGuid, component.Amount, ct),
PaymentComponentType.LoyaltyPoints  => _loyalty.ReserveAsync(msg.TenantId, msg.CustomerId, orderGuid, component.Amount, ct),
PaymentComponentType.Gateway        => _gateway.AuthorizeAsync(msg.TenantId, /*storeId*/ "", msg.CustomerId, orderGuid, component.Amount, msg.Currency, ct),
PaymentComponentType.GiftCard       => TenderCallResult.Ok(null),  // future: GiftCardTenderClient
```

`DispatchCaptureAsync` / `ReleaseSingleAsync` / `CompensateAsync` get matching `Gateway` arms calling `_gateway.CaptureAsync` / `_gateway.VoidAsync` / `_gateway.RefundAsync`.

`ReleasePaymentComponentsConsumer` gets the same three new arms for `Gateway`.

### 3.4 TTL cleanup workers

New `dCMS.Voucher.Api/Workers/HoldExpiryWorker.cs` (and parallel `dCMS.Loyalty.Api/Workers/HoldExpiryWorker.cs`) — `BackgroundService` that:

1. Sleeps `Voucher:HoldExpiry:PollInterval` (default 60s).
2. Calls `store.ListExpiredHoldsAsync(now, batchSize: 100, ct)` → returns rows with `Status='Held' AND ExpiresAt <= now`.
3. For each row, calls `store.ReleaseAsync(tenantId, holdId, reason: "hold_expired", ct)` and publishes `VoucherReleasedV1` / `LoyaltyReleasedV1` with `Reason = "hold_expired"`.
4. Logs per-batch count; on exception, logs and continues.

The `ListExpiredHoldsAsync` query uses the existing `IX_VoucherHolds_Expires` partial index.

Concurrency safety: `ReleaseAsync` already does `UPDATE … WHERE Status='Held'` (CAS), so two workers across pods cannot double-release.

Configuration:

```json
"Voucher": { "HoldExpiry": { "PollIntervalSeconds": 60, "BatchSize": 100, "Enabled": true } }
```

`Enabled: false` lets one pod run the worker if HA strategy is single-instance later.

### 3.5 Tests

#### Unit tests (added to `dCMS.Order.Tests/Unit/Payments/`)

- `PaymentOrchestratorGatewayTests.cs`
  - `Voucher_then_Gateway_happy_path` — voucher captures, gateway captures, `PaymentCompletedV1` published.
  - `Gateway_decline_after_voucher_captured_refunds_voucher` — orchestrator publishes `PaymentFailedV1`, voucher refund called, dispatch log records `REFUND` success.
  - `Gateway_capture_idempotent_on_replay` — second `ProcessPaymentV1` for same orderId does not re-call `IGatewayTenderClient.CaptureAsync`.
  - `Component_reference_used_for_voucher_reserve` — `FakeVoucherClient` asserts `code == "PROMO10"` (set on plan, not on ExternalRef).

- `ReleasePaymentComponentsConsumerTests.cs`
  - `Captured_components_get_refunded`
  - `Authorized_components_get_released`
  - `Pending_components_are_skipped`
  - `Replay_short_circuits_via_dispatch_log`

- `StubGatewayTenderClientTests.cs`
  - Decline keyword, timeout keyword, insufficient-funds suffix, idempotent capture, idempotent refund.

#### Integration tests

- `dCMS.Order.Tests/Sagas/MultiTenderSagaIntegrationTests.cs` (MassTransit `ITestHarness`, no Postgres):
  - `Place_then_pay_with_Voucher_plus_Gateway_reaches_Confirmed` — wires `PaymentOrchestrator` + saga in harness, seeds plan via `FakeRepo`, fakes voucher/gateway clients to succeed. Asserts saga `Confirmed`, `OrderPaymentSettledV1` published.
  - `Customer_cancels_after_capture_triggers_ReleasePaymentComponentsV1_then_components_refund` — drives saga to `Confirmed`, then sends `OrderCustomerCancellationV1`, asserts `ReleasePaymentComponentsV1` consumed and `Refund` calls happened on both fakes.

- `dCMS.Voucher.Api`/`dCMS.Loyalty.Api` tests folder (new, `dCMS.Voucher.Tests`/`dCMS.Loyalty.Tests` aren't created yet — we add a single project `dCMS.Tender.Tests` to keep the project count down):
  - `HoldExpiryWorkerTests.cs` — Testcontainers Postgres, seed expired hold, run one worker tick, assert hold flipped to `Released` and ledger row appended.
  - `Reserve_capture_release_concurrency_test` — two parallel `Capture` calls for same hold; one wins (`Captured`), other returns `invalid_state`.

---

## 4. File map

```
src/backend/dCMS.Order.Core/Domain/Payments/
  PaymentComponent.cs                                  # [edit] add Reference property
  OrderPayment.cs                                      # [edit] Plan accepts Reference

src/backend/dCMS.Order.Infrastructure/
  Migrations/021_AddPaymentComponentReference.sql      # [new]
  Persistence/OrderPaymentRepository.cs                # [edit] Reference column upsert/load
  Payments/TenderClients.cs                            # [edit] IGatewayTenderClient
  Payments/StubGatewayTenderClient.cs                  # [new]
  Payments/HttpGatewayTenderClient.cs                  # [new]
  Payments/PaymentOrchestrator.cs                      # [edit] Gateway arms in 4 switches
  Payments/ReleasePaymentComponentsConsumer.cs         # [edit] Gateway arms in 2 switches
  OrderServiceCollectionExtensions.cs                  # [edit] register IGatewayTenderClient

src/backend/dCMS.Voucher.Api/Workers/HoldExpiryWorker.cs   # [new]
src/backend/dCMS.Voucher.Api/Persistence/IVoucherStore.cs  # [edit] ListExpiredHoldsAsync
src/backend/dCMS.Voucher.Api/Persistence/SqlVoucherStore.cs# [edit]
src/backend/dCMS.Voucher.Api/Program.cs                    # [edit] AddHostedService

src/backend/dCMS.Loyalty.Api/Workers/HoldExpiryWorker.cs   # [new]
src/backend/dCMS.Loyalty.Api/Persistence/ILoyaltyStore.cs  # [edit] ListExpiredHoldsAsync
src/backend/dCMS.Loyalty.Api/Persistence/SqlLoyaltyStore.cs# [edit]
src/backend/dCMS.Loyalty.Api/Program.cs                    # [edit] AddHostedService

src/backend/dCMS.Payment.Api/Program.cs                    # [edit] /internal/payment/{intentId}/capture|refund
src/backend/dCMS.Payment.Infrastructure/...                # [edit] thin route handlers, reuse IPaymentGateway

src/backend/dCMS.Order.Tests/Unit/Payments/
  PaymentOrchestratorGatewayTests.cs                       # [new]
  ReleasePaymentComponentsConsumerTests.cs                 # [new]
  StubGatewayTenderClientTests.cs                          # [new]
src/backend/dCMS.Order.Tests/Sagas/
  MultiTenderSagaIntegrationTests.cs                       # [new]

src/backend/dCMS.Tender.Tests/                             # [new project]
  HoldExpiryWorkerTests.cs
  ConcurrencyCaptureTests.cs
```

---

## 5. Self-review checklist (per CLAUDE.md)

| # | Check | Result |
|---|---|---|
| 1 | Input validation | ✓ Reference max length 128, voucher code regex enforced upstream |
| 2 | Injection / auth bypass | ✓ Reuses existing tenant-scoped routes + `WithTenantAccess` middleware |
| 3 | RBAC | ✓ `OrderAccess` policy already on order routes; gateway client auth via existing internal API key |
| 4 | Tenant isolation | ✓ `tenantId` in every tender call; HoldExpiryWorker scans across all tenants but `Releases` are tenant-scoped |
| 5 | In-memory state | ✓ Stub gateway uses ConcurrentDictionary like existing `StubPaymentGateway` (dev/test only) |
| 6 | N+1 / cache | ✓ Worker batches 100 holds per tick |
| 7 | Rate limit / CORS | ✓ No new public endpoints |
| 8 | i18n / currency | ✓ Currency passed through `ProcessPaymentV1.Currency` |
| 9 | Codebase pattern | ✓ Mirrors `IVoucherTenderClient` shape exactly; reuses `TenderCallResult`, `IPaymentComponentDispatchLog` |
| 10 | Side effects | Migration backfill is one-time and idempotent (only touches Pending rows with ExternalRef) |
| 11 | Docker compatible | ✓ No filesystem, no in-process locks |
| 12 | Tests | New unit + integration coverage detailed in §3.5 |

---

## 6. Out of scope

- Real (non-stub) payment gateway adapter — `HttpGatewayTenderClient` ships but real provider integration (Stripe/VNPay/Momo) is its own future story.
- GiftCard tender client — schema/contract reserves space but no dedicated service yet.
- Storefront UI for selecting tenders at checkout — Phase 2.
- Promotion-driven tender allocation (e.g. "this voucher must be applied first") — already encoded by `OrderPayment.Plan`'s static ordering.
