# Order Service Design — dCMS

**Date:** 2026-04-06
**Status:** Approved
**Scope:** Order Service — microservices architecture, saga orchestration, fulfillment tracking. Does NOT include returns/refunds (future iteration).

> **Phase boundary:**
> - **Phase 1 (current):** Headless CMS + Commerce API. Order Service = core order lifecycle + fulfillment tracking.
> - **Returns/Refunds:** Out of scope — separate iteration after core order flow is stable.

---

## Constraints & Key Decisions

| Dimension | Decision |
|---|---|
| Architecture | Physical microservices — each service separate ASP.NET Core app + separate SQL Server DB |
| Umbraco role | Backoffice UI + Content Service (pages, banners, SEO). NOT a business logic service |
| Saga pattern | MassTransit Saga State Machine (Orchestration) via RabbitMQ |
| Order flow | Hybrid: sync stock check + payment intent, async saga for confirmation |
| Payment | Payment Service = thin proxy to external gateway |
| Fulfillment | Carrier webhook (primary) + backoffice manual fallback + polling worker |
| Message contracts | Shared `dCMS.Messaging.Contracts` NuGet package |
| Versioning | `EventName.v{N}` — additive changes OK, rename/remove → new version |

---

## Section 1: Architecture & Service Boundaries

```
Client (Backoffice / Storefront Phase 2)
          ↓
    API Gateway (YARP)
          ↓
┌──────────────────────────────────────────────────┐
│  Umbraco Service    :5000  CMS content+Backoffice│
│  Catalog Service    :5001  Products, ES indexing │
│  Inventory Service  :5002  Stock management      │
│  Order Service      :5003  Orders + Saga         │
│  Payment Service    :5004  Payment thin proxy    │
└──────────────────────────────────────────────────┘
          ↓
    RabbitMQ (MassTransit)
          ↓
  Workers / Search / Analytics
```

**Each service:**
- 1 ASP.NET Core app, 1 Docker container, 1 SQL Server database
- No service queries another service's DB — ever
- Async communication via RabbitMQ; sync HTTP only for immediate-response validation
- TenantId flows via JWT → all queries scoped per tenant

**Service boundaries:**

| Service | Owns | Does NOT own |
|---|---|---|
| Catalog | Products, Categories, Attributes, ES index | Stock, Orders |
| Inventory | VariantStock, Warehouses, StockMovements | Product details |
| Order | Orders, OrderItems, Shipments, Saga state | Stock numbers, Payment logic |
| Payment | Payment transactions, Gateway calls | Order state |

**Note:** Inventory is separate from Catalog — different scaling profile (stock updates are hot path vs product catalog changes).

---

## Section 2: Database Schema & Domain Model

### Order Service Database

```sql
-- ORDERS
Orders
  Id              nvarchar PK        -- ord_{uuid}
  TenantId        nvarchar
  StoreId         nvarchar
  CustomerId      nvarchar
  Status          nvarchar           -- placed|stock_reserved|payment_pending
                                     -- confirmed|processing|shipped|delivered|cancelled
  CancelReason    nvarchar?
  Currency        nvarchar(3)        -- ISO 4217: VND, USD
  SubTotal        bigint             -- integer, smallest unit
  ShippingFee     bigint
  Total           bigint
  ShippingAddress nvarchar(max)      -- JSON snapshot
  CreatedAt       datetimeoffset
  UpdatedAt       datetimeoffset

-- ORDER ITEMS (snapshot at order time)
OrderItems
  Id              nvarchar PK
  OrderId         nvarchar FK → Orders
  ProductId       nvarchar           -- reference only, no cross-service FK
  VariantId       nvarchar           -- reference only
  ProductName     nvarchar(max)      -- JSON snapshot multilang
  VariantSnapshot nvarchar(max)      -- JSON: {attributes, sku, imageUrl}
  UnitPrice       bigint
  Quantity        int
  LineTotal       bigint

-- SAGA STATE (MassTransit persists here)
OrderSagaState
  CorrelationId   uniqueidentifier PK  -- = OrderId
  CurrentState    nvarchar
  OrderId         nvarchar
  StockReserved   bit
  PaymentIntentId nvarchar?
  RowVersion      rowversion

-- SHIPMENTS
Shipments
  Id              nvarchar PK
  OrderId         nvarchar FK → Orders
  Carrier         nvarchar           -- GHN, GHTK, ViettelPost, DHL...
  TrackingNumber  nvarchar
  Status          nvarchar           -- pending|picked_up|in_transit|delivered|failed
  EstimatedAt     datetimeoffset?
  DeliveredAt     datetimeoffset?
  UpdatedAt       datetimeoffset

-- SHIPMENT EVENTS (audit trail from carrier webhooks)
ShipmentEvents
  Id              nvarchar PK
  ShipmentId      nvarchar FK → Shipments
  Status          nvarchar
  Location        nvarchar?
  Description     nvarchar?
  OccurredAt      datetimeoffset
```

**Snapshot pattern:** OrderItems stores `ProductName` + `VariantSnapshot` at order time — never queries Catalog Service post-creation. Price changes or product deletion do not affect order history.

### Order Saga State Machine

```
[placed] ──sync stock check──► OK
    │                           │
    │                      [stock_reserving]
    │                           │ ReserveStock cmd → Inventory
    │                    ┌──────┴──────────┐
    │             StockReserved      StockReservationFailed
    │                    │                 │
    │          [payment_pending]      [cancelled]
    │                    │
    │          ProcessPayment cmd → Payment Service
    │               ┌────┴────────┐
    │       PaymentCompleted   PaymentFailed
    │               │                │
    │          [confirmed]      ReleaseStock cmd
    │               │           [cancelled]
    │          [processing]
    │               │
    │     ShipmentCreated event
    │          [shipped]
    │               │
    │     ShipmentDelivered event
    │          [delivered]
    │
    └──── cancel anytime before [shipped] → compensation chain
```

**Compensation chain (cancel):**
1. If stock reserved → send `ReleaseStock` command to Inventory Service
2. If payment charged → send `RefundPayment` command to Payment Service
3. Order status → `cancelled`

### Domain Model

```csharp
Order (Aggregate Root)
  - Id, TenantId, StoreId, CustomerId
  - Status (enum)
  - Items: List<OrderItem>       // loaded with Order, not separate aggregate
  - ShippingAddress (value object)
  - SubTotal, ShippingFee, Total (bigint)
  - DomainEvents: List<IDomainEvent>

  + static Create(...)     → raises OrderPlaced event
  + Confirm()              → raises OrderConfirmed event
  + Cancel(reason)         → raises OrderCancelled event
  + MarkShipped(shipmentId)
  + MarkDelivered()
```

OrderItem is not a separate aggregate — loaded with Order, no independent identity.

---

## Section 3: Events & Commands (Message Bus Contracts)

### Message Envelope (all messages)

```json
{
  "messageId":     "uuid-v4",
  "correlationId": "ord_{uuid}",
  "causationId":   "messageId-of-trigger",
  "timestamp":     "2026-04-06T10:00:00Z",
  "tenantId":      "tenant_abc",
  "type":          "OrderPlaced.v1",
  "payload":       { }
}
```

**Ownership:**

| Field | Set by |
|---|---|
| `messageId`, `timestamp`, `tenantId`, `type` | `MessageEnvelopeMiddleware` (automatic) |
| `correlationId` | Caller (OrderId / SagaId — business context) |
| `causationId` | Caller (current `IMessageContext.CurrentMessageId`) |

`MessageEnvelopeMiddleware` lives in shared `dCMS.Messaging.Contracts` NuGet package. No service sets `messageId` or `timestamp` manually — treat as bug in code review.

```csharp
// Shared contract registration
[MessageVersion("OrderPlaced.v1")]
public record OrderPlaced(string OrderId, string TenantId, ...);

// Each service registers once
services.AddMassTransit(x => {
    x.AddPublishPipeSpecification<EnvelopePublishPipeSpec>();
    x.AddConsumePipeSpecification<EnvelopeConsumePipeSpec>();
});
```

### Versioning Strategy

```
Naming:   {EventName}.v{N}   e.g. OrderPlaced.v1
Rule 1:   Add optional field → NO version bump
Rule 2:   Rename / remove field / change type → new version (v2)
Rule 3:   Consumers MUST ignore unknown fields (forward compatibility)
Rule 4:   Keep handler for v(N-1) at least 1 sprint after v(N) deploys
```

### Commands (Order Service → other services)

```
Order Service → Inventory Service

  ReserveStock.v1
    CorrelationId   string
    OrderId         string
    TenantId        string
    StoreId         string
    Items           [{VariantId, WarehouseId, Qty}]
    TimeoutAt       datetimeoffset

  → Terminal event: StockReserved.v1 | StockReservationFailed.v1
  → Timeout:        StockReservationTimeout (saga scheduler, 30s)

  ReleaseStock.v1
    CorrelationId   string
    OrderId         string
    TenantId        string
    Items           [{VariantId, WarehouseId, Qty}]

  → Terminal event: StockReleased.v1

---

Order Service → Payment Service

  ProcessPayment.v1
    CorrelationId   string
    OrderId         string
    TenantId        string
    Amount          bigint
    Currency        string
    CustomerId      string
    PaymentMethod   string        -- card|wallet|cod|bank_transfer
    TimeoutAt       datetimeoffset

  → Terminal event: PaymentCompleted.v1 | PaymentFailed.v1
  → Timeout:        PaymentTimeout (saga scheduler, 15min)

  RefundPayment.v1
    CorrelationId   string
    OrderId         string
    TenantId        string
    PaymentIntentId string
    Amount          bigint
    Reason          string

  → Terminal event: PaymentRefunded.v1
```

**Contract rule:** *Every command must result in exactly one terminal event or a timeout event. No fire-and-forget commands.*

### Events (inbound to Order Saga)

```
Inventory Service publishes:

  StockReserved.v1
    OrderId         string
    ReservedItems   [{VariantId, WarehouseId, Qty}]
    ReservedAt      datetimeoffset

  StockReservationFailed.v1
    OrderId         string
    Reason          string
    FailedItems     [{VariantId, Requested, Available}]

  StockReleased.v1
    OrderId         string

---

Payment Service publishes:

  PaymentCompleted.v1
    OrderId         string
    PaymentIntentId string
    Amount          bigint
    Currency        string
    PaymentMethod   string        -- card|wallet|cod|bank_transfer
    Provider        string        -- stripe|vnpay|momo|...
    PaidAt          datetimeoffset

  PaymentFailed.v1
    OrderId         string
    Reason          string
    ErrorCode       string

  PaymentRefunded.v1
    OrderId         string
    RefundId        string
    Amount          bigint

---

Logistics Webhook → Order Service:

  ShipmentStatusUpdated.v1
    ShipmentId      string
    TrackingNumber  string
    ExternalStatus  string        -- raw carrier status
    MappedStatus    string        -- normalized: picked_up|in_transit|delivered|failed
    Location        string?
    OccurredAt      datetimeoffset
```

### Events (Order Service publishes — outbound)

```
OrderPlaced.v1
  OrderId, TenantId, StoreId, CustomerId
  Items: [{ProductId, VariantId, Qty, UnitPrice}]   -- IDs only, NO full snapshots
  Total: bigint
  Currency: string
  CreatedAt: datetimeoffset

OrderConfirmed.v1    { OrderId, TenantId, StoreId, CustomerId, ConfirmedAt }
OrderCancelled.v1    { OrderId, TenantId, Reason, CancelledAt }
OrderShipped.v1      { OrderId, TrackingNumber, Carrier, EstimatedAt }
OrderDelivered.v1    { OrderId, DeliveredAt }
```

**Consumers:** Notification Service (email/push), Analytics Worker, Inventory Worker (salesCount30d).

### Saga Timeout Events

```
StockReservationTimeout  { OrderId, TenantId }
  Triggered: 30s after ReserveStock command
  Action:    Cancel order — no compensation needed (stock not yet reserved)

PaymentTimeout           { OrderId, TenantId }
  Triggered: 15min after ProcessPayment command
  Action:    ReleaseStock + Cancel order
```

### Idempotency & At-least-once delivery

```
ReserveStock handler (Inventory):
  Check if OrderId already has StockReservation → skip if exists
  Use Outbox Pattern for StockReserved event

ProcessPayment handler (Payment):
  PaymentIntentId as idempotency key with external gateway
  Gateway returns "already processed" → treat as PaymentCompleted
```

### Event Size & DLQ

```
Size rule:
  Event payload < 64KB
  Large data → store in DB, send reference ID in event
  OrderPlaced sends item IDs only — NOT full VariantSnapshot

DLQ:
  3 retries with exponential backoff: 1s → 5s → 30s
  Dead letter queue: dlq.{exchange}.{routingKey}
  DLQ message retains full envelope + adds:
    x-death-count:   int
    x-first-death:   datetimeoffset
    x-death-reason:  string
  Alert: DLQ depth > 0 → Slack webhook
```

---

## Section 4: API Design

### Principles

1. **CQRS (light):** Write APIs hit SQL + start Saga. Read APIs optimizable with cache/read model later.
2. **Idempotency required** on `POST /orders` and `POST .../cancel` via `Idempotency-Key: uuid` header.
3. **No internal complexity exposed:** Client never sees saga, message bus, or retry logic.

### Order APIs

```
POST /api/orders
  Headers: Idempotency-Key: uuid
  Body:
    storeId, customerId
    items: [{variantId, quantity}]
    shippingAddress: {name, phone, addressLine}
    paymentMethod: vnpay|card|cod|...

  Flow:
    1. Validate input (variant exists, quantity > 0)
    2. Sync: check stock → Inventory Service /internal/inventory/check (read-only)
    3. Sync: create payment intent → Payment Service (get paymentUrl)
    4. Create Order (status: payment_pending)
    5. Start OrderSaga
    6. Return immediately

  Response 201:
    { orderId, status: "payment_pending", paymentUrl: "https://..." }

  Response 422:
    { success: false, error: { code: "OUT_OF_STOCK", message: "..." } }

---

GET /api/orders/{orderId}
  RBAC: customer = own order only | staff = store scope
  Response: { orderId, status, items, total, currency,
              shipment: { carrier, trackingNumber, status, events } }
  Cache: Redis dcms:order:{orderId} TTL 60s, invalidate on status change

---

GET /api/orders?customerId=...&status=...&cursor=...
  Cursor-based pagination
  Response: { items: [...], nextCursor: "..." }

---

POST /api/orders/{orderId}/cancel
  Headers: Idempotency-Key: uuid
  Body: { reason: string }
  Rule: Only allowed when status NOT IN (shipped, delivered, cancelled)
  Flow: Trigger saga compensation chain

---

POST /api/orders/{orderId}/ship          StoreStaff / StoreManager
  Body: { carrier, trackingNumber, estimatedAt }
  → Create Shipment (status: pending)
  → Publish OrderShipped.v1

---

POST /api/orders/{orderId}/deliver       StoreStaff / StoreManager (manual fallback)
  Rule: shipment must exist + status != delivered
  → Mark delivered + publish OrderDelivered.v1
```

### Shipment APIs

```
GET /api/orders/{orderId}/shipment
  RBAC: customer (own order), StoreStaff+
  Response: { carrier, trackingNumber, status, events: [...] }

POST /api/webhooks/shipment/{carrier}
  Auth: HMAC-SHA256 signature (no JWT)
  → verify signature → normalize → upsert ShipmentEvent
```

### Internal APIs (service-to-service)

```
POST /internal/inventory/check     -- sync, read-only stock validation
  Body: { items: [{variantId, warehouseId, qty}] }
  Response: { available: true } | { available: false, failures: [...] }
  Note: Does NOT lock stock. Actual reserve = ReserveStock saga command.
```

### Response Format

```json
// Success
{ "success": true, "data": { ... } }

// Error
{ "success": false, "error": { "code": "OUT_OF_STOCK", "message": "Variant v1 not available" } }
```

### Mandatory Requirements

```
Idempotency:
  Redis key: dcms:idempotency:{key} TTL 24h
  On hit: return cached response immediately

Rate limiting:
  Per tenant (plan tier) + per user
  Same model as Catalog Service

RBAC:
  Customer: WHERE CustomerId = jwt.sub AND TenantId = jwt.tenantId
  StoreStaff/Manager: WHERE StoreId IN jwt.storeIds AND TenantId = jwt.tenantId

Validation:
  variant exists (sync call or cached)
  quantity > 0
  shippingAddress all required fields present

Anti double-order:
  Idempotency-Key (backend)
  UI disables submit button after first click (frontend responsibility)
```

---

## Section 5: Fulfillment Tracking

### Shipment Lifecycle

```
Order confirmed
      ↓
StoreStaff creates shipment (POST /api/orders/{id}/ship)
      ↓
  [pending] → carrier picks up → [picked_up]
      ↓
  [in_transit] → multiple location updates
      ↓
  [delivered] ← carrier webhook (primary)
             ← backoffice manual (fallback)
      ↓
OrderDelivered.v1 published
```

### Carrier Webhook Handler

```
POST /api/webhooks/shipment/{carrier}
  Headers: X-Carrier-Signature: hmac-sha256

Flow:
  1. Verify HMAC signature (per-carrier secret from config)
  2. Replay protection: reject if OccurredAt > 5min ago
  3. Parse raw payload → normalize to ShipmentStatusUpdated.v1
  4. Upsert ShipmentEvent (idempotent: UNIQUE TrackingNumber + OccurredAt)
  5. Update Shipments.Status
  6. If MappedStatus = "delivered" → trigger OrderDelivered flow
  7. If MappedStatus = "failed"    → publish alert event for notification
```

**Carrier status mapping** (config file, not hardcoded):

```json
{
  "GHN":         { "Delivered": "delivered", "DeliveryFail": "failed" },
  "GHTK":        { "Đã giao hàng": "delivered", "Giao không thành công": "failed" },
  "ViettelPost": { "PHAT_THANH_CONG": "delivered", "PHAT_KHONG_THANH_CONG": "failed" }
}
```

### Polling Fallback (carriers without webhook support)

```
ShipmentPollingWorker (IHostedService)
  Schedule: every 30 minutes
  Query:    Shipments WHERE Status NOT IN ('delivered','failed')
              AND UpdatedAt < NOW() - 1 hour
  Action:   Call carrier tracking API → upsert ShipmentEvent if changed
```

### RBAC on Shipment APIs

```
POST /ship          StoreManager, StoreStaff
POST /deliver       StoreManager, StoreStaff
GET  /shipment      Customer (own order only), StoreStaff+
POST /webhooks/*    No JWT — HMAC signature verification only
```

---

## Section 7: Failure Matrix & Retry Strategy

### 7.1 Foundational Principles

| Principle | Rule |
|---|---|
| **At-least-once delivery** | Messages may duplicate — every consumer MUST be idempotent |
| **No distributed transactions** | Use Saga + Compensation instead |
| **Bounded retries** | Never retry infinitely — always have DLQ as terminal |
| **Every failure has terminal state** | Orders must never be stuck forever |

---

### 7.2 Failure Matrix — Core Flows

#### Create Order (sync flow)

| Step | Failure | Action |
|---|---|---|
| Input validation | Invalid fields | 400 — no saga started |
| Stock check (sync) | Insufficient stock | 422 OUT_OF_STOCK — no saga started |
| Payment intent (sync) | Gateway error | 422 PAYMENT_INIT_FAILED — no saga started |
| Save order to DB | DB failure | 500 — client retries with Idempotency-Key |
| Start saga | Internal error | MassTransit retry (immediate × 3) |

#### Stock Reservation (async saga)

**Scenario A — Inventory Service down:**
```
ReserveStock.v1 published → no response
  → MassTransit retry: 1s → 5s → 30s
  → StockReservationTimeout fires (30s hard deadline)
  → Order cancelled (no compensation — stock not yet reserved)
```

**Scenario B — Stock insufficient:**
```
StockReservationFailed.v1 received
  → Order cancelled immediately
  → No compensation needed
```

**Scenario C — Duplicate ReserveStock message:**
```
Inventory Service: if OrderId already has StockReservation → skip (idempotent)
  → Re-publish StockReserved.v1 (same result)
```

#### Payment Flow (async saga)

**Scenario A — Payment Service down:**
```
ProcessPayment.v1 → no response
  → MassTransit retry: 1s → 5s → 30s
  → PaymentTimeout fires (15min hard deadline)
  → Send ReleaseStock.v1 → Order cancelled
```

**Scenario B — Payment failed:**
```
PaymentFailed.v1 received
  → Send ReleaseStock.v1
  → Order cancelled
```

**Scenario C — Payment SUCCESS after timeout (CRITICAL):**
```
Timeline:
  t=0    ProcessPayment sent
  t=15m  PaymentTimeout → ReleaseStock → Order cancelled
  t=17m  PaymentCompleted webhook arrives (gateway was slow)

Handler: if Order.Status == cancelled → trigger RefundPayment automatically
  → Payment Service receives RefundPayment.v1 command
  → Calls gateway refund API
  → Publishes PaymentRefunded.v1

Rule: NEVER ignore a PaymentCompleted on a cancelled order.
      Always auto-refund. Log as anomaly for ops monitoring.
```

#### ReleaseStock Failure (compensation)

```
ReleaseStock.v1 published → Inventory Service fails
  → Retry: 1s → 5s → 30s
  → Still failing → DLQ: dlq.inventory.release-stock
  → Slack alert: "ReleaseStock stuck for OrderId={id}"
  → Manual intervention via Admin UI (reprocess DLQ)

Note: Stock remains reserved until manually released.
      VariantStock.ReservedQuantity will be inflated.
      Stock reconciliation job (daily) detects and alerts.
```

#### Webhook Failures

| Scenario | Action |
|---|---|
| Invalid signature | 401 + Slack alert + log raw payload |
| Duplicate webhook | Idempotent check (TrackingNumber + OccurredAt) → 200, no reprocess |
| Delayed webhook | Accept event regardless of age — apply if MappedStatus is a valid transition |
| Carrier down (no webhook) | ShipmentPollingWorker polls every 30min as fallback |

#### Message Consumer Crash

```
Consumer crashes mid-processing
  → RabbitMQ redelivers (at-least-once)
  → Consumer must check ProcessedMessages table before processing
  → Retry: 1s → 5s → 30s → DLQ
```

---

### 7.3 Retry Policy (production standard)

```
Attempt 1: immediate
Attempt 2: +1s
Attempt 3: +5s
Attempt 4: +30s
→ DLQ (no more retries)
```

**Retryable errors:** network timeout, temporary DB unavailable, 5xx from downstream service

**Non-retryable (go straight to DLQ):** validation error, business rule violation (e.g. OUT_OF_STOCK), 4xx from downstream

MassTransit config:
```csharp
e.UseMessageRetry(r => r.Intervals(
    TimeSpan.FromSeconds(1),
    TimeSpan.FromSeconds(5),
    TimeSpan.FromSeconds(30)
));
e.UseInMemoryOutbox();  // ensure publish after consume succeeds
```

---

### 7.4 Idempotency Strategy

| Key | Used by |
|---|---|
| `OrderId` | ReserveStock, ReleaseStock handlers in Inventory Service |
| `PaymentIntentId` | ProcessPayment handler + gateway calls |
| `MessageId` (from envelope) | General consumer idempotency |

**ProcessedMessages table** (per service):
```sql
ProcessedMessages
  MessageId   nvarchar PK    -- from envelope
  ProcessedAt datetimeoffset
  -- TTL: cleanup messages older than 7 days (background job)
```

**Rule:** Before processing any command/event → `IF EXISTS(SELECT 1 FROM ProcessedMessages WHERE MessageId=@id) → RETURN`

---

### 7.5 Saga Resilience Rules

| Rule | Detail |
|---|---|
| Every step has timeout | Stock: 30s · Payment: 15min |
| Every step has compensation | StockReserved → ReleaseStock · PaymentCompleted → RefundPayment |
| Saga state persisted | SQL Server — service crash → resume from last known state on restart |
| Saga is the source of truth | Order.Status always updated by saga, never directly by API |

---

### 7.6 DLQ Handling

```
On message entering DLQ:
  1. Slack alert: queue name, message type, OrderId, error reason
  2. Full context logged: envelope + payload + x-death headers
  3. DLQ depth metric exported to Prometheus

Admin UI (Umbraco "System Health" tab — Order Service section):
  → List DLQ messages with filter by type/date
  → View full payload
  → "Retry" — re-enqueue to original exchange
  → "Discard" — mark as resolved with reason

API:
  POST /api/v1/admin/orders/dlq/{messageId}/retry    (SuperAdmin)
  POST /api/v1/admin/orders/dlq/{messageId}/discard  (SuperAdmin)
```

---

### 7.7 Chaos Test Scenarios (required in test suite)

**Case 1 — Inventory reserved, crash before event sent:**
```
Setup: OutboxProcessor running, crash after DB INSERT but before RabbitMQ publish
Expected: On restart, OutboxProcessor re-reads unprocessed OutboxEvents → re-publishes
          StockReserved.v1 → saga continues normally
Verify: StockReserved.v1 delivered exactly once (idempotency check in saga)
```

**Case 2 — PaymentCompleted arrives while Order Service is down:**
```
Setup: PaymentCompleted.v1 published to RabbitMQ, Order Service pod killed
Expected: RabbitMQ holds message. On restart, Order Service consumes → updates saga state
          → Order.Status = confirmed
Verify: No message lost, no duplicate processing
```

**Case 3 — Duplicate PaymentCompleted:**
```
Setup: PaymentCompleted.v1 published twice (gateway retry)
Expected: Second message → ProcessedMessages check → skip
          Order.Status remains confirmed (not double-confirmed)
          No double OrderConfirmed.v1 event published
Verify: ProcessedMessages has exactly 1 entry for that MessageId
```

---

## Section 6: Cross-Cutting Concerns

### Observability

```
Distributed tracing: OpenTelemetry → Jaeger / Azure Monitor
  - correlationId from envelope propagated across all spans
  - Each service injects TraceId in response header: X-Trace-Id
  - Saga state transitions logged with TraceId

Structured logging: Serilog → Seq / ELK
  Log each:
    - Saga state transition (OrderId, FromState, ToState, Duration)
    - Command sent / event received (MessageId, Type, TenantId)
    - DLQ message (MessageId, RetryCount, Reason)
    - Webhook received (Carrier, TrackingNumber, RawStatus → MappedStatus)

Metrics (Prometheus):
  order_created_total{tenant, store}
  saga_step_duration_seconds{step}        -- p95 per step
  dlq_depth{queue}                        -- alert if > 0
  shipment_webhook_received_total{carrier, mapped_status}
```

### Security

```
API Gateway layer:
  JWT validation on all /api/* except /api/webhooks/*
  TenantId extracted from JWT → forwarded as X-Tenant-Id header
  Rate limiting per tenant plan

Order Service:
  RBAC enforced at query level (not just middleware)
  Idempotency-Key required on mutating POST endpoints
  Redis: dcms:idempotency:{key} TTL 24h

Webhook endpoints:
  HMAC-SHA256 per-carrier secret (stored in config, not env var)
  Replay protection: reject OccurredAt > 5min
  IP whitelist per carrier (optional, if carrier provides IP range)
```

### Error Handling & Compensation Matrix

| Scenario | Action |
|---|---|
| Stock check fail (sync) | 422 OUT_OF_STOCK — no saga started |
| Payment intent fail (sync) | 422 PAYMENT_INIT_FAILED — no saga started |
| StockReservationTimeout (30s) | Cancel order — no compensation (not yet reserved) |
| StockReservationFailed | Cancel order — no compensation |
| PaymentFailed | ReleaseStock + Cancel order |
| PaymentTimeout (15min) | ReleaseStock + Cancel order |
| Carrier webhook invalid signature | 401 — Slack alert |
| DLQ depth > 0 | Slack alert — manual intervention required |

### Testing Strategy

```
Unit tests:
  - OrderSaga state transitions (MassTransit InMemory harness)
  - Carrier status mapping (all known carrier statuses per carrier)
  - RBAC rules (customer owns order, staff scoped to store)
  - Compensation chain logic

Integration tests (Testcontainers):
  - SQL Server: Order CRUD, Shipment CRUD, Saga state persistence
  - RabbitMQ: full saga happy path end-to-end
  - RabbitMQ: compensation path (PaymentFailed → StockReleased → Cancelled)
  - Webhook handler: HMAC verify + status normalize + idempotency

Contract tests (Pact) — MANDATORY:
  - Order Service ↔ Inventory Service message contracts
  - Order Service ↔ Payment Service message contracts
  - Ensures v1 contracts are not broken on independent deploys
  - Run in CI before any service deploys to staging
```

Contract tests are non-negotiable — integration tests cannot catch breaking contract changes between independently deployed services.

---

## Architecture Impact on Existing Specs

The Product Catalog spec (`2026-04-06-product-catalog-design.md`) was designed as Umbraco-embedded. With the microservices pivot:

- **Catalog Service** → pure ASP.NET Core, separate DB, separate deployment
- **Inventory Service** → extracted from Catalog — own ASP.NET Core service
- **Product Catalog spec** needs revision in a separate brainstorming session

This is tracked as a known gap — do not implement Product Catalog spec as-is.
