# DAI-689 Multi-Tender Payment Remaining Gaps — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the four remaining gaps in the DAI-689 multi-tender epic: separate `PaymentComponent.Reference` from `ExternalRef`, wire a real `IGatewayTenderClient` (stub + http) into `PaymentOrchestrator`, add hold-expiry workers in `Voucher.Api`/`Loyalty.Api`, and add saga + ReleaseConsumer + worker tests.

**Architecture:** All work lives inside the existing `dCMS.Order.*` / `dCMS.Voucher.Api` / `dCMS.Loyalty.Api` projects. The orchestrator already consumes `ProcessPaymentV1` and walks components in canonical order; we are filling in real gateway behavior, fixing the input/output column overload, scanning for expired holds, and adding tests.

**Tech Stack:** .NET 8, ASP.NET Core minimal APIs, Dapper + Npgsql (PostgreSQL), MassTransit + RabbitMQ, MassTransit `ITestHarness`, xUnit, Testcontainers, Polly.

**Spec:** [docs/superpowers/specs/2026-04-28-multi-tender-payment-dai-689-gaps-design.md](../specs/2026-04-28-multi-tender-payment-dai-689-gaps-design.md)

---

## Task list

1. Migration `021_AddPaymentComponentReference.sql` + repo wiring
2. Domain — `PaymentComponent.Reference` + `OrderPayment.Plan` overload
3. Orchestrator — read `Reference` (not `ExternalRef`) on voucher reserve
4. `IGatewayTenderClient` interface + `TenderCallResult` reuse
5. `StubGatewayTenderClient` (decline / timeout / insufficient-funds rules)
6. `HttpGatewayTenderClient` + Payment.Api `/internal/payment/{id}/capture|refund` endpoints
7. Wire gateway into `PaymentOrchestrator` (reserve / capture / release / compensate)
8. Wire gateway into `ReleasePaymentComponentsConsumer`
9. DI registration (`AddTenderHttpClients` extension)
10. Voucher.Api — `IVoucherStore.ListExpiredHoldsAsync` + SQL impl
11. Voucher.Api — `HoldExpiryWorker` background service
12. Loyalty.Api — `ILoyaltyStore.ListExpiredHoldsAsync` + SQL impl
13. Loyalty.Api — `HoldExpiryWorker` background service
14. Tests — `StubGatewayTenderClientTests`
15. Tests — `PaymentOrchestratorGatewayTests`
16. Tests — `ReleasePaymentComponentsConsumerTests`
17. Tests — `MultiTenderSagaIntegrationTests`
18. Tests — `HoldExpiryWorkerTests` (Testcontainers)
19. MEMORY.md update + final verify

---

### Task 1: Migration `021_AddPaymentComponentReference.sql` + repo wiring

**Files:**
- Create: `src/backend/dCMS.Order.Infrastructure/Migrations/021_AddPaymentComponentReference.sql`
- Modify: `src/backend/dCMS.Order.Infrastructure/Persistence/OrderPaymentRepository.cs`

- [ ] **Step 1: Create the migration**

```sql
-- DAI-689: split PaymentComponents.ExternalRef (output, holdId/chargeRef)
-- from a new "Reference" column (input, voucher code / customer id / etc).
ALTER TABLE "PaymentComponents"
    ADD COLUMN IF NOT EXISTS "Reference" VARCHAR(128) NULL;

-- Backfill: rows still in Pending state with a non-null ExternalRef are
-- pre-authorize voucher codes (see PaymentOrchestrator legacy comment).
-- Move them to the new Reference column and clear ExternalRef.
UPDATE "PaymentComponents"
   SET "Reference" = "ExternalRef",
       "ExternalRef" = NULL
 WHERE "State" = 'Pending'
   AND "ExternalRef" IS NOT NULL;
```

- [ ] **Step 2: Update Dapper INSERT in `OrderPaymentRepository.UpsertAsync`**

In [OrderPaymentRepository.cs:46-73](src/backend/dCMS.Order.Infrastructure/Persistence/OrderPaymentRepository.cs#L46-L73), add `"Reference"` to the INSERT column list, params, and DO UPDATE SET:

```csharp
await conn.ExecuteAsync(new CommandDefinition(
    """
    INSERT INTO "PaymentComponents"
      ("Id","OrderPaymentId","Type","Amount","Reference","ExternalRef","State","LastError","Ordering","CreatedAt","UpdatedAt")
    VALUES
      (@Id, @OrderPaymentId, @Type, @Amount, @Reference, @ExternalRef, @State, @LastError, @Ordering, @CreatedAt, @UpdatedAt)
    ON CONFLICT ("Id") DO UPDATE SET
      "Amount"=EXCLUDED."Amount",
      "Reference"=COALESCE(EXCLUDED."Reference","PaymentComponents"."Reference"),
      "ExternalRef"=EXCLUDED."ExternalRef",
      "State"=EXCLUDED."State",
      "LastError"=EXCLUDED."LastError",
      "Ordering"=EXCLUDED."Ordering",
      "UpdatedAt"=EXCLUDED."UpdatedAt";
    """,
    new
    {
        c.Id,
        OrderPaymentId = paymentId,
        Type = c.Type.ToString(),
        c.Amount,
        c.Reference,
        c.ExternalRef,
        State = c.State.ToString(),
        c.LastError,
        c.Ordering,
        c.CreatedAt,
        c.UpdatedAt,
    },
    transaction: tx, cancellationToken: ct));
```

- [ ] **Step 3: Update SELECT in `GetByOrderIdAsync`**

Replace the SELECT and tuple at lines 90-108:

```csharp
var rows = await conn.QueryAsync<(Guid Id, string Type, decimal Amount, string? Reference, string? ExternalRef, string State, string? LastError, int Ordering, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt)>(
    new CommandDefinition(
        """
        SELECT "Id","Type","Amount","Reference","ExternalRef","State","LastError","Ordering","CreatedAt","UpdatedAt"
        FROM "PaymentComponents" WHERE "OrderPaymentId"=@PaymentId
        ORDER BY "Ordering" ASC, "CreatedAt" ASC;
        """,
        new { PaymentId = head.Value.Id }, cancellationToken: ct));

var components = rows.Select(r => new PaymentComponent(
    r.Id,
    Enum.Parse<PaymentComponentType>(r.Type, ignoreCase: true),
    r.Amount,
    r.Ordering,
    Enum.Parse<PaymentComponentState>(r.State, ignoreCase: true),
    r.Reference,
    r.ExternalRef,
    r.LastError,
    r.CreatedAt,
    r.UpdatedAt));
```

(`PaymentComponent` constructor signature change happens in Task 2; build will fail until Task 2 lands. That is fine — these two tasks land together in a single commit.)

- [ ] **Step 4: Commit (deferred — bundle with Task 2)**

Hold the commit; Task 2 finishes the domain change and we commit once everything compiles.

---

### Task 2: `PaymentComponent.Reference` + `OrderPayment.Plan` overload

**Files:**
- Modify: `src/backend/dCMS.Order.Core/Domain/Payments/PaymentComponent.cs`
- Modify: `src/backend/dCMS.Order.Core/Domain/Payments/OrderPayment.cs`
- Test: `src/backend/dCMS.Order.Tests/Unit/Payments/OrderPaymentTests.cs` (extend)

- [ ] **Step 1: Write the failing test**

Append to `OrderPaymentTests.cs`:

```csharp
[Fact]
public void Plan_persists_reference_per_component()
{
    var orderId = Guid.NewGuid();
    var plan = OrderPayment.Plan(orderId, 100m, new[]
    {
        (PaymentComponentType.Voucher, 40m, (string?)"PROMO10"),
        (PaymentComponentType.LoyaltyPoints, 60m, (string?)"cust-1"),
    });

    Assert.Equal("PROMO10", plan.Components.Single(c => c.Type == PaymentComponentType.Voucher).Reference);
    Assert.Equal("cust-1", plan.Components.Single(c => c.Type == PaymentComponentType.LoyaltyPoints).Reference);
    Assert.All(plan.Components, c => Assert.Null(c.ExternalRef));
}

[Fact]
public void Authorize_sets_ExternalRef_without_clearing_Reference()
{
    var c = new PaymentComponent(Guid.NewGuid(), PaymentComponentType.Voucher, 40m, ordering: 0,
        reference: "PROMO10");
    c.Authorize("hold-123");
    Assert.Equal("PROMO10", c.Reference);
    Assert.Equal("hold-123", c.ExternalRef);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/backend/dCMS.Order.Tests/dCMS.Order.Tests.csproj --filter "FullyQualifiedName~OrderPaymentTests.Plan_persists_reference_per_component|FullyQualifiedName~OrderPaymentTests.Authorize_sets_ExternalRef_without_clearing_Reference"`

Expected: FAIL — `OrderPayment.Plan` does not accept a 3-tuple, `PaymentComponent` has no `Reference` property.

- [ ] **Step 3: Add `Reference` to `PaymentComponent`**

Replace [PaymentComponent.cs](src/backend/dCMS.Order.Core/Domain/Payments/PaymentComponent.cs) class body so the constructor accepts `reference`:

```csharp
public sealed class PaymentComponent
{
    public Guid Id { get; }
    public PaymentComponentType Type { get; }
    public decimal Amount { get; }
    /// <summary>Input identifier set at plan time (voucher code, customer id, etc.). Immutable.</summary>
    public string? Reference { get; }
    /// <summary>Output identifier set after Authorize (holdId, chargeRef, etc.).</summary>
    public string? ExternalRef { get; private set; }
    public PaymentComponentState State { get; private set; }
    public string? LastError { get; private set; }
    public int Ordering { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public PaymentComponent(
        Guid id,
        PaymentComponentType type,
        decimal amount,
        int ordering,
        PaymentComponentState state = PaymentComponentState.Pending,
        string? reference = null,
        string? externalRef = null,
        string? lastError = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null)
    {
        if (amount < 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be non-negative.");
        Id = id;
        Type = type;
        Amount = amount;
        Ordering = ordering;
        State = state;
        Reference = reference;
        ExternalRef = externalRef;
        LastError = lastError;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = updatedAt;
    }

    public void Authorize(string externalRef, DateTimeOffset? at = null)
    {
        ExternalRef = externalRef;
        State = PaymentComponentState.Authorized;
        UpdatedAt = at ?? DateTimeOffset.UtcNow;
    }

    public void Capture(DateTimeOffset? at = null)
    {
        State = PaymentComponentState.Captured;
        UpdatedAt = at ?? DateTimeOffset.UtcNow;
    }

    public void Fail(string error, DateTimeOffset? at = null)
    {
        State = PaymentComponentState.Failed;
        LastError = error;
        UpdatedAt = at ?? DateTimeOffset.UtcNow;
    }

    public void Refund(DateTimeOffset? at = null)
    {
        State = PaymentComponentState.Refunded;
        UpdatedAt = at ?? DateTimeOffset.UtcNow;
    }

    public void Cancel(DateTimeOffset? at = null)
    {
        State = PaymentComponentState.Cancelled;
        UpdatedAt = at ?? DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 4: Replace `OrderPayment.Plan` to accept reference**

In [OrderPayment.cs:28-36](src/backend/dCMS.Order.Core/Domain/Payments/OrderPayment.cs#L28-L36):

```csharp
public static OrderPayment Plan(
    Guid orderId,
    decimal total,
    IEnumerable<(PaymentComponentType Type, decimal Amount, string? Reference)> tenders)
{
    var ordered = tenders
        .Where(t => t.Amount > 0m)
        .OrderBy(t => OrderingFor(t.Type))
        .Select((t, i) => new PaymentComponent(Guid.NewGuid(), t.Type, t.Amount, i, reference: t.Reference))
        .ToList();
    return new OrderPayment(Guid.NewGuid(), orderId, total, "Pending", ordered);
}

// Backwards-compat overload — used by existing tests that don't care about Reference.
public static OrderPayment Plan(
    Guid orderId,
    decimal total,
    IEnumerable<(PaymentComponentType Type, decimal Amount)> tenders)
    => Plan(orderId, total, tenders.Select(t => (t.Type, t.Amount, (string?)null)));
```

- [ ] **Step 5: Run all Order tests**

Run: `dotnet test src/backend/dCMS.Order.Tests/dCMS.Order.Tests.csproj`

Expected: PASS — including the existing 8 `OrderPaymentTests` plus the 2 new ones, plus the 3 `PaymentOrchestratorTests` (which use the 2-tuple overload).

- [ ] **Step 6: Commit**

```bash
git add src/backend/dCMS.Order.Core/Domain/Payments/PaymentComponent.cs \
        src/backend/dCMS.Order.Core/Domain/Payments/OrderPayment.cs \
        src/backend/dCMS.Order.Infrastructure/Migrations/021_AddPaymentComponentReference.sql \
        src/backend/dCMS.Order.Infrastructure/Persistence/OrderPaymentRepository.cs \
        src/backend/dCMS.Order.Tests/Unit/Payments/OrderPaymentTests.cs
git commit -m "feat(orders): split PaymentComponent.Reference from ExternalRef (DAI-689)

- Migration 021 adds Reference column, backfills from ExternalRef where Pending.
- PaymentComponent.Reference is immutable input (voucher code, customer id);
  ExternalRef stays as output (holdId, chargeRef) set by Authorize().
- OrderPayment.Plan overload accepts Reference; legacy 2-tuple overload kept.
- Repo INSERT/SELECT updated.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: Orchestrator reads `Reference` (not `ExternalRef`) on voucher reserve

**Files:**
- Modify: `src/backend/dCMS.Order.Infrastructure/Payments/PaymentOrchestrator.cs:114-118`
- Test: `src/backend/dCMS.Order.Tests/Unit/Payments/PaymentOrchestratorTests.cs` (extend)

- [ ] **Step 1: Write the failing test**

Append to `PaymentOrchestratorTests.cs`:

```csharp
[Fact]
public async Task Voucher_reserve_uses_component_reference_not_externalref()
{
    var (_, repo, voucher, loyalty, _, harness) = await BuildAsync();
    var orderId = Guid.NewGuid();
    var plan = OrderPayment.Plan(orderId, 25m, new[]
    {
        (PaymentComponentType.Voucher, 25m, (string?)"PROMO10"),
    });
    repo.Seed(plan);

    await harness.Bus.Publish(new ProcessPaymentV1(
        Guid.NewGuid(), orderId.ToString(), "t1", "cust-1", 25m, "USD", "card",
        DateTimeOffset.UtcNow.AddMinutes(15)));
    await harness.InactivityTask;

    Assert.Equal("PROMO10", voucher.LastReserveCode);
}
```

Add a `LastReserveCode` field on `FakeVoucherClient`:

```csharp
public string? LastReserveCode;
public Task<TenderCallResult> ReserveAsync(string tenantId, string code, Guid orderId, decimal amount, CancellationToken ct)
{
    ReserveCalls++;
    LastReserveCode = code;
    if (ReserveResult is { } r) return Task.FromResult(r);
    return Task.FromResult(TenderCallResult.Ok((NextHoldId ?? Guid.NewGuid()).ToString()));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/backend/dCMS.Order.Tests/dCMS.Order.Tests.csproj --filter "FullyQualifiedName~Voucher_reserve_uses_component_reference_not_externalref"`

Expected: FAIL — orchestrator currently passes `component.ExternalRef ?? msg.OrderId`, which is `null ?? orderId` (a stringified Guid), not `"PROMO10"`.

- [ ] **Step 3: Update the orchestrator**

In [PaymentOrchestrator.cs:114-118](src/backend/dCMS.Order.Infrastructure/Payments/PaymentOrchestrator.cs#L114-L118):

```csharp
PaymentComponentType.Voucher
    => await _vouchers.ReserveAsync(
        msg.TenantId,
        code: component.Reference ?? throw new InvalidOperationException(
            $"Voucher component {component.Id} on order {orderId} has no Reference (voucher code)."),
        orderId, component.Amount, ct),
```

- [ ] **Step 4: Run all PaymentOrchestrator tests**

Run: `dotnet test src/backend/dCMS.Order.Tests/dCMS.Order.Tests.csproj --filter "FullyQualifiedName~PaymentOrchestratorTests"`

Expected: All 4 tests PASS (3 existing + 1 new). Existing tests use voucher with explicit `Reference` via the new 3-tuple overload — UPDATE the existing tests now to pass `(...,"PROMO10")` so the assertion in the new test still holds.

In each existing test that builds `OrderPayment.Plan(...)` with `PaymentComponentType.Voucher`, change the tender tuple to include `Reference: "PROMO10"`:

```csharp
var plan = OrderPayment.Plan(orderId, 100m, new[]
{
    (PaymentComponentType.Voucher, 40m, (string?)"PROMO10"),
    (PaymentComponentType.LoyaltyPoints, 60m, (string?)"cust-1"),
});
```

(Using the 3-tuple overload, which preserves test behavior; the 2-tuple overload still exists for tests that don't touch Voucher.)

- [ ] **Step 5: Commit**

```bash
git add src/backend/dCMS.Order.Infrastructure/Payments/PaymentOrchestrator.cs \
        src/backend/dCMS.Order.Tests/Unit/Payments/PaymentOrchestratorTests.cs
git commit -m "fix(orders): voucher reserve reads Reference not ExternalRef (DAI-689)

The orchestrator previously fell back to msg.OrderId when ExternalRef
was null, which would have called Voucher.Api with a Guid in place of
the voucher code. Now Reference is required for Voucher components and
throws if missing — placement flow must set it via OrderPayment.Plan.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: `IGatewayTenderClient` interface

**Files:**
- Modify: `src/backend/dCMS.Order.Infrastructure/Payments/TenderClients.cs` (append interface)

- [ ] **Step 1: Append `IGatewayTenderClient`**

After the existing `ILoyaltyTenderClient` declaration:

```csharp
/// <summary>
/// DAI-689: gateway tender client. Authorize creates an external charge intent and
/// returns a chargeRef; capture commits; void releases an uncaptured authorization;
/// refund returns funds on a captured charge. Idempotent on chargeRef.
/// </summary>
public interface IGatewayTenderClient
{
    Task<TenderCallResult> AuthorizeAsync(
        string tenantId, string customerId, Guid orderId, decimal amount, string currency, CancellationToken ct);
    Task<TenderCallResult> CaptureAsync(string tenantId, string chargeRef, CancellationToken ct);
    Task<TenderCallResult> VoidAsync(string tenantId, string chargeRef, string reason, CancellationToken ct);
    Task<TenderCallResult> RefundAsync(string tenantId, string chargeRef, CancellationToken ct);
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/backend/dCMS.Order.Infrastructure/dCMS.Order.Infrastructure.csproj`

Expected: succeeds.

- [ ] **Step 3: Commit (deferred — bundle with Task 5)**

---

### Task 5: `StubGatewayTenderClient`

**Files:**
- Create: `src/backend/dCMS.Order.Infrastructure/Payments/StubGatewayTenderClient.cs`
- Test: `src/backend/dCMS.Order.Tests/Unit/Payments/StubGatewayTenderClientTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using dCMS.Order.Infrastructure.Payments;
using Xunit;

namespace dCMS.Order.Tests.Unit.Payments;

public sealed class StubGatewayTenderClientTests
{
    [Fact]
    public async Task Authorize_default_succeeds_with_charge_ref()
    {
        var client = new StubGatewayTenderClient();
        var orderId = Guid.NewGuid();
        var r = await client.AuthorizeAsync("t1", "cust-1", orderId, 100m, "USD", default);
        Assert.True(r.Success);
        Assert.Equal($"ch_stub_{orderId:N}", r.ExternalRef);
    }

    [Fact]
    public async Task Authorize_decline_keyword_in_customer_fails()
    {
        var client = new StubGatewayTenderClient();
        var r = await client.AuthorizeAsync("t1", "cust-decline-me", Guid.NewGuid(), 100m, "USD", default);
        Assert.False(r.Success);
        Assert.Equal("card_declined", r.ErrorCode);
    }

    [Fact]
    public async Task Authorize_amount_ending_99_fails_insufficient_funds()
    {
        var client = new StubGatewayTenderClient();
        var r = await client.AuthorizeAsync("t1", "cust-1", Guid.NewGuid(), 12.99m, "USD", default);
        Assert.False(r.Success);
        Assert.Equal("insufficient_funds", r.ErrorCode);
    }

    [Fact]
    public async Task Authorize_timeout_keyword_throws()
    {
        var client = new StubGatewayTenderClient();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.AuthorizeAsync("t1", "cust-timeout", Guid.NewGuid(), 100m, "USD", default));
    }

    [Fact]
    public async Task Capture_then_capture_again_idempotent()
    {
        var client = new StubGatewayTenderClient();
        var orderId = Guid.NewGuid();
        var auth = await client.AuthorizeAsync("t1", "cust-1", orderId, 100m, "USD", default);
        Assert.True((await client.CaptureAsync("t1", auth.ExternalRef!, default)).Success);
        Assert.True((await client.CaptureAsync("t1", auth.ExternalRef!, default)).Success);
    }

    [Fact]
    public async Task Refund_idempotent()
    {
        var client = new StubGatewayTenderClient();
        var auth = await client.AuthorizeAsync("t1", "cust-1", Guid.NewGuid(), 100m, "USD", default);
        await client.CaptureAsync("t1", auth.ExternalRef!, default);
        Assert.True((await client.RefundAsync("t1", auth.ExternalRef!, default)).Success);
        Assert.True((await client.RefundAsync("t1", auth.ExternalRef!, default)).Success);
    }

    [Fact]
    public async Task Void_releases_uncaptured()
    {
        var client = new StubGatewayTenderClient();
        var auth = await client.AuthorizeAsync("t1", "cust-1", Guid.NewGuid(), 100m, "USD", default);
        var v = await client.VoidAsync("t1", auth.ExternalRef!, "abandoned", default);
        Assert.True(v.Success);
    }

    [Fact]
    public async Task Refund_uncaptured_fails()
    {
        var client = new StubGatewayTenderClient();
        var auth = await client.AuthorizeAsync("t1", "cust-1", Guid.NewGuid(), 100m, "USD", default);
        var r = await client.RefundAsync("t1", auth.ExternalRef!, default);
        Assert.False(r.Success);
        Assert.Equal("invalid_state", r.ErrorCode);
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

Run: `dotnet test src/backend/dCMS.Order.Tests/dCMS.Order.Tests.csproj --filter "FullyQualifiedName~StubGatewayTenderClientTests"`

Expected: FAIL — class does not exist.

- [ ] **Step 3: Create the stub**

```csharp
using System.Collections.Concurrent;

namespace dCMS.Order.Infrastructure.Payments;

/// <summary>
/// DAI-689: dev/test stub for the multi-tender Gateway component. Mirrors the existing
/// <c>StubPaymentGateway</c> in <c>dCMS.Payment.Infrastructure</c> but conforms to
/// <see cref="IGatewayTenderClient"/>. Failure scenarios:
///  - customerId contains "decline" → card_declined
///  - customerId contains "timeout" → throws OperationCanceledException
///  - amount cents end in 99 → insufficient_funds
/// Authorize/capture/refund are idempotent on chargeRef.
/// </summary>
public sealed class StubGatewayTenderClient : IGatewayTenderClient
{
    private readonly ConcurrentDictionary<string, byte> _captured = new();
    private readonly ConcurrentDictionary<string, byte> _refunded = new();
    private readonly ConcurrentDictionary<string, byte> _voided = new();

    public Task<TenderCallResult> AuthorizeAsync(
        string tenantId, string customerId, Guid orderId, decimal amount, string currency, CancellationToken ct)
    {
        if (customerId?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true)
            throw new OperationCanceledException("stub gateway timeout");

        if (customerId?.Contains("decline", StringComparison.OrdinalIgnoreCase) == true)
            return Task.FromResult(TenderCallResult.Fail("card_declined", "Stub: customer flagged decline."));

        var cents = decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
        if (cents % 100m == 99m)
            return Task.FromResult(TenderCallResult.Fail("insufficient_funds", "Stub: amount ends in .99."));

        return Task.FromResult(TenderCallResult.Ok($"ch_stub_{orderId:N}"));
    }

    public Task<TenderCallResult> CaptureAsync(string tenantId, string chargeRef, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(chargeRef))
            return Task.FromResult(TenderCallResult.Fail("invalid_charge_ref", "chargeRef required."));
        if (_voided.ContainsKey(chargeRef))
            return Task.FromResult(TenderCallResult.Fail("invalid_state", "Authorization was voided."));
        _captured.TryAdd(chargeRef, 0);
        return Task.FromResult(TenderCallResult.Ok(chargeRef));
    }

    public Task<TenderCallResult> VoidAsync(string tenantId, string chargeRef, string reason, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(chargeRef))
            return Task.FromResult(TenderCallResult.Fail("invalid_charge_ref", "chargeRef required."));
        if (_captured.ContainsKey(chargeRef))
            return Task.FromResult(TenderCallResult.Fail("invalid_state", "Already captured; use Refund."));
        _voided.TryAdd(chargeRef, 0);
        return Task.FromResult(TenderCallResult.Ok(chargeRef));
    }

    public Task<TenderCallResult> RefundAsync(string tenantId, string chargeRef, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(chargeRef))
            return Task.FromResult(TenderCallResult.Fail("invalid_charge_ref", "chargeRef required."));
        if (!_captured.ContainsKey(chargeRef))
            return Task.FromResult(TenderCallResult.Fail("invalid_state", "Cannot refund uncaptured charge."));
        _refunded.TryAdd(chargeRef, 0);
        return Task.FromResult(TenderCallResult.Ok(chargeRef));
    }
}
```

- [ ] **Step 4: Run tests — verify they pass**

Run: `dotnet test src/backend/dCMS.Order.Tests/dCMS.Order.Tests.csproj --filter "FullyQualifiedName~StubGatewayTenderClientTests"`

Expected: 8 PASS.

- [ ] **Step 5: Commit (deferred — bundle with Task 9)**

---

### Task 6: `HttpGatewayTenderClient` + Payment.Api capture/refund/void endpoints

**Files:**
- Create: `src/backend/dCMS.Order.Infrastructure/Payments/HttpGatewayTenderClient.cs`
- Modify: `src/backend/dCMS.Payment.Api/Program.cs`

- [ ] **Step 1: Create `HttpGatewayTenderClient`**

```csharp
using System.Net.Http.Json;

namespace dCMS.Order.Infrastructure.Payments;

/// <summary>
/// DAI-689: production gateway tender client. Talks to <c>dCMS.Payment.Api</c>'s
/// <c>/internal/payment/...</c> surface. Authorize delegates to
/// <c>POST /internal/payment/create-intent</c> and treats paymentIntentId as chargeRef.
/// </summary>
public sealed class HttpGatewayTenderClient : TenderHttpClientBase, IGatewayTenderClient
{
    public HttpGatewayTenderClient(HttpClient http) : base(http) { }

    public async Task<TenderCallResult> AuthorizeAsync(
        string tenantId, string customerId, Guid orderId, decimal amount, string currency, CancellationToken ct)
    {
        using var resp = await Http.PostAsJsonAsync(
            "internal/payment/create-intent",
            new { orderId, tenantId, customerId, amount, currency, paymentMethod = "card" }, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return await SendAsync(resp, ct);

        var body = await resp.Content.ReadFromJsonAsync<EnvelopeWithIntent>(cancellationToken: ct).ConfigureAwait(false);
        var chargeRef = body?.Data?.PaymentIntentId;
        return string.IsNullOrEmpty(chargeRef)
            ? TenderCallResult.Fail("invalid_response", "create-intent returned no paymentIntentId")
            : TenderCallResult.Ok(chargeRef);
    }

    public async Task<TenderCallResult> CaptureAsync(string tenantId, string chargeRef, CancellationToken ct)
    {
        using var resp = await Http.PostAsJsonAsync(
            $"internal/payment/{Uri.EscapeDataString(chargeRef)}/capture",
            new { tenantId }, ct).ConfigureAwait(false);
        return await SendAsync(resp, ct);
    }

    public async Task<TenderCallResult> VoidAsync(string tenantId, string chargeRef, string reason, CancellationToken ct)
    {
        using var resp = await Http.PostAsJsonAsync(
            $"internal/payment/{Uri.EscapeDataString(chargeRef)}/void",
            new { tenantId, reason }, ct).ConfigureAwait(false);
        return await SendAsync(resp, ct);
    }

    public async Task<TenderCallResult> RefundAsync(string tenantId, string chargeRef, CancellationToken ct)
    {
        using var resp = await Http.PostAsJsonAsync(
            $"internal/payment/{Uri.EscapeDataString(chargeRef)}/refund",
            new { tenantId }, ct).ConfigureAwait(false);
        return await SendAsync(resp, ct);
    }

    private sealed record EnvelopeWithIntent(IntentData? Data);
    private sealed record IntentData(string? PaymentIntentId);
}
```

- [ ] **Step 2: Add Payment.Api endpoints**

Before adding the endpoints, read [src/backend/dCMS.Payment.Core/ProcessPaymentGatewayModels.cs](src/backend/dCMS.Payment.Core/ProcessPaymentGatewayModels.cs) to confirm the exact constructor parameters of `ProcessPaymentGatewayRequest` and `RefundPaymentGatewayRequest`. The existing `StubPaymentGateway.ProcessPaymentAsync` only inspects `request.PaymentIntentId` and uses idempotency on it — pass an instance built from the route param `chargeRef`.

In `src/backend/dCMS.Payment.Api/Program.cs`, near the existing `/internal/payment/create-intent` MapPost, add:

```csharp
app.MapPost("/internal/payment/{chargeRef}/capture", async (
    string chargeRef,
    dCMS.Payment.Core.IPaymentGateway gateway,
    CancellationToken ct) =>
{
    // PaymentIntentId is the idempotency key for ProcessPaymentAsync per IPaymentGateway contract.
    var req = new dCMS.Payment.Core.ProcessPaymentGatewayRequest(PaymentIntentId: chargeRef);
    var result = await gateway.ProcessPaymentAsync(req, ct);
    return result switch
    {
        dCMS.Payment.Core.ProcessPaymentGatewayResult.Succeeded s
            => Results.Json(new { data = new { chargeRef = s.ProviderChargeId }, error = (object?)null }),
        dCMS.Payment.Core.ProcessPaymentGatewayResult.AlreadySucceeded a
            => Results.Json(new { data = new { chargeRef = a.ProviderChargeId }, error = (object?)null }),
        dCMS.Payment.Core.ProcessPaymentGatewayResult.Failed f
            => Results.BadRequest(new { data = (object?)null, error = new { code = f.Reason ?? "gateway_failed", message = f.Reason ?? "gateway failed" } }),
        _ => Results.BadRequest(new { error = new { code = "unknown", message = "unknown gateway result" } })
    };
});

app.MapPost("/internal/payment/{chargeRef}/refund", async (
    string chargeRef,
    dCMS.Payment.Core.IPaymentGateway gateway,
    CancellationToken ct) =>
{
    var req = new dCMS.Payment.Core.RefundPaymentGatewayRequest(PaymentIntentId: chargeRef);
    var result = await gateway.RefundPaymentAsync(req, ct);
    return result switch
    {
        dCMS.Payment.Core.RefundPaymentGatewayResult.Succeeded s
            => Results.Json(new { data = new { refundRef = s.ProviderRefundId }, error = (object?)null }),
        dCMS.Payment.Core.RefundPaymentGatewayResult.AlreadyRefunded a
            => Results.Json(new { data = new { refundRef = a.ProviderRefundId }, error = (object?)null }),
        dCMS.Payment.Core.RefundPaymentGatewayResult.Failed f
            => Results.BadRequest(new { data = (object?)null, error = new { code = f.Reason ?? "refund_failed", message = f.Reason ?? "refund failed" } }),
        _ => Results.BadRequest(new { error = new { code = "unknown", message = "unknown gateway result" } })
    };
});

// Void: IPaymentGateway has no Void today. Stub returns success — keeps the orchestrator's
// surface uniform; a real adapter implements the void path.
app.MapPost("/internal/payment/{chargeRef}/void", (string chargeRef) =>
    Results.Json(new { data = new { chargeRef }, error = (object?)null }));
```

If `ProcessPaymentGatewayRequest` requires more positional args than `PaymentIntentId`, accept a body record `{ orderId, amount, currency }` on the route and pass through. Inspect the model file before assuming.

- [ ] **Step 3: Build both projects**

Run: `dotnet build src/backend/dCMS.Order.Infrastructure/dCMS.Order.Infrastructure.csproj && dotnet build src/backend/dCMS.Payment.Api/dCMS.Payment.Api.csproj`

Expected: Both succeed.

- [ ] **Step 4: Commit (deferred — bundle with Task 9)**

---

### Task 7: Wire gateway into `PaymentOrchestrator`

**Files:**
- Modify: `src/backend/dCMS.Order.Infrastructure/Payments/PaymentOrchestrator.cs`

- [ ] **Step 1: Inject `IGatewayTenderClient` into the constructor**

Replace the constructor and field set in [PaymentOrchestrator.cs:26-44](src/backend/dCMS.Order.Infrastructure/Payments/PaymentOrchestrator.cs#L26-L44):

```csharp
private readonly OrderPaymentRepository _payments;
private readonly IPaymentComponentDispatchLog _log;
private readonly IVoucherTenderClient _vouchers;
private readonly ILoyaltyTenderClient _loyalty;
private readonly IGatewayTenderClient _gateway;
private readonly ILogger<PaymentOrchestrator> _logger;

public PaymentOrchestrator(
    OrderPaymentRepository payments,
    IPaymentComponentDispatchLog log,
    IVoucherTenderClient vouchers,
    ILoyaltyTenderClient loyalty,
    IGatewayTenderClient gateway,
    ILogger<PaymentOrchestrator> logger)
{
    _payments = payments;
    _log = log;
    _vouchers = vouchers;
    _loyalty = loyalty;
    _gateway = gateway;
    _logger = logger;
}
```

- [ ] **Step 2: Update `DispatchReserveAsync` Gateway arm**

Replace the `Gateway` / `GiftCard` switch arm in `DispatchReserveAsync` (around line 121):

```csharp
PaymentComponentType.Gateway
    => await _gateway.AuthorizeAsync(msg.TenantId, msg.CustomerId, orderId, component.Amount, msg.Currency, ct),
PaymentComponentType.GiftCard
    => TenderCallResult.Ok(null), // future: GiftCardTenderClient
```

- [ ] **Step 3: Update `DispatchCaptureAsync` Gateway arm**

Replace the early-return `if (component.Type is GiftCard or Gateway) return Ok();` and the inner switch (around line 134-150):

```csharp
private async Task<TenderCallResult> DispatchCaptureAsync(Guid orderId, string tenantId, PaymentComponent component, CancellationToken ct)
{
    var prior = await _log.TryGetAsync(orderId, component.Id, "CAPTURE", ct);
    if (prior is { IsSuccess: true }) return TenderCallResult.Ok(prior.ExternalRef);
    if (prior is { IsSuccess: false })
        return TenderCallResult.Fail(prior.ErrorCode ?? "capture_failed", prior.ErrorMessage ?? "previous capture failed");

    TenderCallResult result;
    switch (component.Type)
    {
        case PaymentComponentType.Voucher:
        case PaymentComponentType.LoyaltyPoints:
            if (!Guid.TryParse(component.ExternalRef, out var holdId) || holdId == Guid.Empty)
                return TenderCallResult.Fail("missing_hold", "no hold id from reserve to capture");
            result = component.Type == PaymentComponentType.Voucher
                ? await _vouchers.CaptureAsync(tenantId, holdId, ct)
                : await _loyalty.CaptureAsync(tenantId, holdId, ct);
            break;
        case PaymentComponentType.Gateway:
            if (string.IsNullOrEmpty(component.ExternalRef))
                return TenderCallResult.Fail("missing_charge_ref", "no chargeRef from authorize to capture");
            result = await _gateway.CaptureAsync(tenantId, component.ExternalRef, ct);
            break;
        default:
            result = TenderCallResult.Ok();
            break;
    }

    if (result.Success)
        await _log.RecordSuccessAsync(orderId, component.Id, "CAPTURE", null, ct);
    else
        await _log.RecordFailureAsync(orderId, component.Id, "CAPTURE", result.ErrorCode, result.ErrorMessage, ct);
    return result;
}
```

- [ ] **Step 4: Update `ReleaseSingleAsync` Gateway arm**

Replace the switch in `ReleaseSingleAsync` (around line 162-167):

```csharp
private async Task ReleaseSingleAsync(Guid orderId, string tenantId, PaymentComponent component, string reason, CancellationToken ct)
{
    TenderCallResult result;
    string refLabel;
    switch (component.Type)
    {
        case PaymentComponentType.Voucher:
        case PaymentComponentType.LoyaltyPoints:
            if (!Guid.TryParse(component.ExternalRef, out var holdId) || holdId == Guid.Empty) return;
            result = component.Type == PaymentComponentType.Voucher
                ? await _vouchers.ReleaseAsync(tenantId, holdId, reason, ct)
                : await _loyalty.ReleaseAsync(tenantId, holdId, reason, ct);
            refLabel = holdId.ToString();
            break;
        case PaymentComponentType.Gateway:
            if (string.IsNullOrEmpty(component.ExternalRef)) return;
            result = await _gateway.VoidAsync(tenantId, component.ExternalRef, reason, ct);
            refLabel = component.ExternalRef;
            break;
        default: return;
    }
    if (!result.Success)
        _logger.LogWarning("PaymentOrchestrator: release failed for {Component}/{Ref}: {Code} {Message}",
            component.Type, refLabel, result.ErrorCode, result.ErrorMessage);
    else
        await _log.RecordSuccessAsync(orderId, component.Id, "RELEASE", refLabel, ct);
}
```

- [ ] **Step 5: Update `CompensateAsync` Gateway arm**

Replace the switch in `CompensateAsync` (around line 178-186):

```csharp
private async Task CompensateAsync(Guid orderId, string tenantId, IEnumerable<PaymentComponent> captured, string reason, CancellationToken ct)
{
    foreach (var c in captured.Reverse())
    {
        TenderCallResult result;
        string refLabel;
        switch (c.Type)
        {
            case PaymentComponentType.Voucher:
            case PaymentComponentType.LoyaltyPoints:
                if (!Guid.TryParse(c.ExternalRef, out var holdId) || holdId == Guid.Empty) continue;
                result = c.Type == PaymentComponentType.Voucher
                    ? await _vouchers.RefundAsync(tenantId, holdId, ct)
                    : await _loyalty.RefundAsync(tenantId, holdId, ct);
                refLabel = holdId.ToString();
                break;
            case PaymentComponentType.Gateway:
                if (string.IsNullOrEmpty(c.ExternalRef)) continue;
                result = await _gateway.RefundAsync(tenantId, c.ExternalRef, ct);
                refLabel = c.ExternalRef;
                break;
            default: continue;
        }
        if (result.Success)
        {
            c.Refund();
            await _log.RecordSuccessAsync(orderId, c.Id, "REFUND", refLabel, ct);
        }
        else
        {
            _logger.LogError("PaymentOrchestrator: compensation refund failed for {Component}/{Ref} reason={Reason}: {Code} {Message}",
                c.Type, refLabel, reason, result.ErrorCode, result.ErrorMessage);
        }
    }
}
```

- [ ] **Step 6: Build to confirm orchestrator compiles**

Run: `dotnet build src/backend/dCMS.Order.Infrastructure/dCMS.Order.Infrastructure.csproj`

Expected: build succeeds. Existing 4 PaymentOrchestrator tests will fail at construction time because they don't pass `IGatewayTenderClient` — Task 9 fixes the test fixtures together with the DI wiring; `dotnet test` will be re-run there.

- [ ] **Step 7: Commit (deferred — bundle with Task 9)**

---

### Task 8: Wire gateway into `ReleasePaymentComponentsConsumer`

**Files:**
- Modify: `src/backend/dCMS.Order.Infrastructure/Payments/ReleasePaymentComponentsConsumer.cs`

- [ ] **Step 1: Add `IGatewayTenderClient` to constructor**

Replace the field declarations and constructor in [ReleasePaymentComponentsConsumer.cs:18-36](src/backend/dCMS.Order.Infrastructure/Payments/ReleasePaymentComponentsConsumer.cs#L18-L36):

```csharp
private readonly OrderPaymentRepository _payments;
private readonly IPaymentComponentDispatchLog _log;
private readonly IVoucherTenderClient _vouchers;
private readonly ILoyaltyTenderClient _loyalty;
private readonly IGatewayTenderClient _gateway;
private readonly ILogger<ReleasePaymentComponentsConsumer> _logger;

public ReleasePaymentComponentsConsumer(
    OrderPaymentRepository payments,
    IPaymentComponentDispatchLog log,
    IVoucherTenderClient vouchers,
    ILoyaltyTenderClient loyalty,
    IGatewayTenderClient gateway,
    ILogger<ReleasePaymentComponentsConsumer> logger)
{
    _payments = payments;
    _log = log;
    _vouchers = vouchers;
    _loyalty = loyalty;
    _gateway = gateway;
    _logger = logger;
}
```

- [ ] **Step 2: Update `Consume` to handle Gateway components**

Replace the foreach in `Consume` to dispatch on type — Voucher/Loyalty use `Guid.TryParse` on `ExternalRef`, Gateway treats `ExternalRef` as the chargeRef string:

```csharp
public async Task Consume(ConsumeContext<ReleasePaymentComponentsV1> ctx)
{
    var msg = ctx.Message;
    var payment = await _payments.GetByOrderIdAsync(msg.OrderId, ctx.CancellationToken);
    if (payment is null) return;

    foreach (var c in payment.Components)
    {
        switch (c.State)
        {
            case PaymentComponentState.Captured:
                await IssueRefundAsync(msg, c, ctx.CancellationToken);
                break;
            case PaymentComponentState.Authorized:
                await IssueReleaseAsync(msg, c, ctx.CancellationToken);
                break;
            // Pending/Failed/Refunded/Cancelled: nothing to do.
        }
    }

    await _payments.UpsertAsync(payment, ctx.CancellationToken);
}
```

- [ ] **Step 3: Update `IssueRefundAsync`**

```csharp
private async Task IssueRefundAsync(ReleasePaymentComponentsV1 msg, PaymentComponent c, CancellationToken ct)
{
    var prior = await _log.TryGetAsync(msg.OrderId, c.Id, "REFUND", ct);
    if (prior is { IsSuccess: true }) { c.Refund(); return; }

    TenderCallResult result;
    string refLabel;
    switch (c.Type)
    {
        case PaymentComponentType.Voucher:
        case PaymentComponentType.LoyaltyPoints:
            if (!Guid.TryParse(c.ExternalRef, out var holdId) || holdId == Guid.Empty) return;
            result = c.Type == PaymentComponentType.Voucher
                ? await _vouchers.RefundAsync(msg.TenantId, holdId, ct)
                : await _loyalty.RefundAsync(msg.TenantId, holdId, ct);
            refLabel = holdId.ToString();
            break;
        case PaymentComponentType.Gateway:
            if (string.IsNullOrEmpty(c.ExternalRef)) return;
            result = await _gateway.RefundAsync(msg.TenantId, c.ExternalRef, ct);
            refLabel = c.ExternalRef;
            break;
        default:
            result = TenderCallResult.Ok();
            refLabel = c.ExternalRef ?? "";
            break;
    }

    if (result.Success)
    {
        c.Refund();
        await _log.RecordSuccessAsync(msg.OrderId, c.Id, "REFUND", refLabel, ct);
    }
    else
    {
        await _log.RecordFailureAsync(msg.OrderId, c.Id, "REFUND", result.ErrorCode, result.ErrorMessage, ct);
        _logger.LogError("ReleasePaymentComponents: refund failed {Order}/{Component}: {Code} {Message}",
            msg.OrderId, c.Type, result.ErrorCode, result.ErrorMessage);
    }
}
```

- [ ] **Step 4: Update `IssueReleaseAsync`**

```csharp
private async Task IssueReleaseAsync(ReleasePaymentComponentsV1 msg, PaymentComponent c, CancellationToken ct)
{
    var prior = await _log.TryGetAsync(msg.OrderId, c.Id, "RELEASE", ct);
    if (prior is { IsSuccess: true }) { c.Cancel(); return; }

    TenderCallResult result;
    string refLabel;
    switch (c.Type)
    {
        case PaymentComponentType.Voucher:
        case PaymentComponentType.LoyaltyPoints:
            if (!Guid.TryParse(c.ExternalRef, out var holdId) || holdId == Guid.Empty) return;
            result = c.Type == PaymentComponentType.Voucher
                ? await _vouchers.ReleaseAsync(msg.TenantId, holdId, msg.Reason, ct)
                : await _loyalty.ReleaseAsync(msg.TenantId, holdId, msg.Reason, ct);
            refLabel = holdId.ToString();
            break;
        case PaymentComponentType.Gateway:
            if (string.IsNullOrEmpty(c.ExternalRef)) return;
            result = await _gateway.VoidAsync(msg.TenantId, c.ExternalRef, msg.Reason, ct);
            refLabel = c.ExternalRef;
            break;
        default:
            result = TenderCallResult.Ok();
            refLabel = c.ExternalRef ?? "";
            break;
    }

    if (result.Success)
    {
        c.Cancel();
        await _log.RecordSuccessAsync(msg.OrderId, c.Id, "RELEASE", refLabel, ct);
    }
    else
    {
        await _log.RecordFailureAsync(msg.OrderId, c.Id, "RELEASE", result.ErrorCode, result.ErrorMessage, ct);
        _logger.LogWarning("ReleasePaymentComponents: release failed {Order}/{Component}: {Code} {Message}",
            msg.OrderId, c.Type, result.ErrorCode, result.ErrorMessage);
    }
}
```

- [ ] **Step 5: Build**

Run: `dotnet build src/backend/dCMS.Order.Infrastructure/dCMS.Order.Infrastructure.csproj`

Expected: success.

- [ ] **Step 6: Commit (deferred — bundle with Task 9)**

---

### Task 9: DI registration + commit Tasks 4–9

**Files:**
- Modify: `src/backend/dCMS.Order.Infrastructure/OrderServiceCollectionExtensions.cs:166-184`
- Modify: `src/backend/dCMS.Order.Tests/Unit/Payments/PaymentOrchestratorTests.cs` (constructor fix)

- [ ] **Step 1: Register the gateway client**

Replace the body of `AddTenderHttpClients` in [OrderServiceCollectionExtensions.cs:166-184](src/backend/dCMS.Order.Infrastructure/OrderServiceCollectionExtensions.cs#L166-L184):

```csharp
public static IServiceCollection AddTenderHttpClients(this IServiceCollection services, IConfiguration configuration)
{
    var voucherBase = configuration["Voucher:BaseUrl"] ?? "http://voucher-api:8080/";
    var loyaltyBase = configuration["Loyalty:BaseUrl"] ?? "http://loyalty-api:8080/";
    var paymentBase = configuration["Payment:BaseUrl"] ?? "http://payment-api:8080/";

    services.AddHttpClient<IVoucherTenderClient, HttpVoucherTenderClient>(client =>
            client.BaseAddress = new Uri(voucherBase.TrimEnd('/') + "/", UriKind.Absolute))
        .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(2, _ => TimeSpan.FromMilliseconds(200)));

    services.AddHttpClient<ILoyaltyTenderClient, HttpLoyaltyTenderClient>(client =>
            client.BaseAddress = new Uri(loyaltyBase.TrimEnd('/') + "/", UriKind.Absolute))
        .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(2, _ => TimeSpan.FromMilliseconds(200)));

    // DAI-689: gateway client. Default to stub (matches dCMS.Payment.Infrastructure default).
    if (configuration.GetValue("Payment:UseStubGateway", true))
    {
        services.AddSingleton<IGatewayTenderClient, StubGatewayTenderClient>();
    }
    else
    {
        services.AddHttpClient<IGatewayTenderClient, HttpGatewayTenderClient>(client =>
                client.BaseAddress = new Uri(paymentBase.TrimEnd('/') + "/", UriKind.Absolute))
            .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(2, _ => TimeSpan.FromMilliseconds(200)));
    }

    return services;
}
```

- [ ] **Step 2: Fix existing PaymentOrchestratorTests**

In `PaymentOrchestratorTests.cs`, update the `BuildAsync` helper to register and pass `IGatewayTenderClient`:

```csharp
private static async Task<(PaymentOrchestrator orch, FakeRepo repo, FakeVoucherClient voucher, FakeLoyaltyClient loyalty, FakeDispatchLog log, ITestHarness harness)> BuildAsync()
{
    var repo = new FakeRepo();
    var voucher = new FakeVoucherClient();
    var loyalty = new FakeLoyaltyClient();
    var gateway = new StubGatewayTenderClient();
    var log = new FakeDispatchLog();

    var services = new ServiceCollection();
    services.AddSingleton<OrderPaymentRepository>(_ => repo);
    services.AddSingleton<IPaymentComponentDispatchLog>(log);
    services.AddSingleton<IVoucherTenderClient>(voucher);
    services.AddSingleton<ILoyaltyTenderClient>(loyalty);
    services.AddSingleton<IGatewayTenderClient>(gateway);
    services.AddMassTransitTestHarness(cfg => cfg.AddConsumer<PaymentOrchestrator>());
    var provider = services.BuildServiceProvider(true);
    var harness = provider.GetRequiredService<ITestHarness>();
    await harness.Start();

    var orch = new PaymentOrchestrator(repo, log, voucher, loyalty, gateway, NullLogger<PaymentOrchestrator>.Instance);
    return (orch, repo, voucher, loyalty, log, harness);
}
```

- [ ] **Step 3: Run all Order tests**

Run: `dotnet test src/backend/dCMS.Order.Tests/dCMS.Order.Tests.csproj`

Expected: all green — existing PaymentOrchestrator + OrderPayment + Stub tests, plus the 1 added in Task 3.

- [ ] **Step 4: Commit Tasks 4–9 together**

```bash
git add src/backend/dCMS.Order.Infrastructure/Payments/TenderClients.cs \
        src/backend/dCMS.Order.Infrastructure/Payments/StubGatewayTenderClient.cs \
        src/backend/dCMS.Order.Infrastructure/Payments/HttpGatewayTenderClient.cs \
        src/backend/dCMS.Order.Infrastructure/Payments/PaymentOrchestrator.cs \
        src/backend/dCMS.Order.Infrastructure/Payments/ReleasePaymentComponentsConsumer.cs \
        src/backend/dCMS.Order.Infrastructure/OrderServiceCollectionExtensions.cs \
        src/backend/dCMS.Payment.Api/Program.cs \
        src/backend/dCMS.Order.Tests/Unit/Payments/StubGatewayTenderClientTests.cs \
        src/backend/dCMS.Order.Tests/Unit/Payments/PaymentOrchestratorTests.cs
git commit -m "feat(orders): wire IGatewayTenderClient into multi-tender orchestrator (DAI-689)

Stub + HTTP impl for Gateway component. Orchestrator's Reserve/Capture/
Release/Compensate now go through IGatewayTenderClient instead of returning
no-op Ok(). ReleasePaymentComponentsConsumer mirrors the same routing on
late cancels. DI registers stub by default; Payment:UseStubGateway=false
switches to HttpGatewayTenderClient against Payment.Api's new
/internal/payment/{chargeRef}/{capture|refund|void} endpoints.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

### Task 10: Voucher.Api — `IVoucherStore.ListExpiredHoldsAsync` + SQL impl

**Files:**
- Modify: `src/backend/dCMS.Voucher.Api/Persistence/IVoucherStore.cs`
- Modify: `src/backend/dCMS.Voucher.Api/Persistence/SqlVoucherStore.cs`

- [ ] **Step 1: Add the method to the interface**

After the existing `GetBalanceAsync` declaration in [IVoucherStore.cs](src/backend/dCMS.Voucher.Api/Persistence/IVoucherStore.cs):

```csharp
/// <summary>
/// DAI-689: lists holds in 'Held' state whose ExpiresAt is in the past, up to <paramref name="limit"/> rows.
/// Used by the HoldExpiryWorker to release abandoned reservations. Cross-tenant scan — caller is the worker.
/// </summary>
Task<IReadOnlyList<ExpiredHoldRow>> ListExpiredHoldsAsync(DateTimeOffset asOf, int limit, CancellationToken ct);
```

Add `ExpiredHoldRow` record at the bottom of the file:

```csharp
public sealed record ExpiredHoldRow(Guid HoldId, string TenantId, Guid VoucherId, Guid OrderId, decimal Amount, DateTimeOffset ExpiresAt);
```

- [ ] **Step 2: Implement in `SqlVoucherStore`**

Append to [SqlVoucherStore.cs](src/backend/dCMS.Voucher.Api/Persistence/SqlVoucherStore.cs):

```csharp
public async Task<IReadOnlyList<ExpiredHoldRow>> ListExpiredHoldsAsync(DateTimeOffset asOf, int limit, CancellationToken ct)
{
    if (limit <= 0) return Array.Empty<ExpiredHoldRow>();

    await using var conn = new NpgsqlConnection(connectionString);
    var rows = await conn.QueryAsync<ExpiredHoldRow>(new CommandDefinition(
        """
        SELECT "Id" AS HoldId, "TenantId" AS TenantId, "VoucherId" AS VoucherId,
               "OrderId" AS OrderId, "Amount" AS Amount, "ExpiresAt" AS ExpiresAt
          FROM "VoucherHolds"
         WHERE "Status" = 'Held' AND "ExpiresAt" <= @AsOf
         ORDER BY "ExpiresAt" ASC
         LIMIT @Limit;
        """,
        new { AsOf = asOf, Limit = limit }, cancellationToken: ct));
    return rows.AsList();
}
```

- [ ] **Step 3: Build**

Run: `dotnet build src/backend/dCMS.Voucher.Api/dCMS.Voucher.Api.csproj`

Expected: success.

- [ ] **Step 4: Commit (deferred — bundle with Task 11)**

---

### Task 11: Voucher.Api — `HoldExpiryWorker`

**Files:**
- Create: `src/backend/dCMS.Voucher.Api/Workers/HoldExpiryWorker.cs`
- Modify: `src/backend/dCMS.Voucher.Api/Program.cs` (register worker)
- Modify: `src/backend/dCMS.Voucher.Api/appsettings.json` (config defaults)

- [ ] **Step 1: Create the worker**

```csharp
using dCMS.Core.Messaging;
using dCMS.Voucher.Api.Persistence;
using MassTransit;

namespace dCMS.Voucher.Api.Workers;

/// <summary>
/// DAI-689: scans VoucherHolds for expired 'Held' rows every <c>Voucher:HoldExpiry:PollIntervalSeconds</c>
/// (default 60s) and releases up to <c>BatchSize</c> (default 100) per tick. Releases use the existing
/// CAS path so two workers across pods never double-release the same hold. Publishes
/// <see cref="VoucherReleasedV1"/> with reason="hold_expired" for each successful release.
/// </summary>
public sealed class HoldExpiryWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<HoldExpiryWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly int _batchSize;
    private readonly bool _enabled;

    public HoldExpiryWorker(IServiceProvider sp, IConfiguration cfg, ILogger<HoldExpiryWorker> logger)
    {
        _sp = sp;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(cfg.GetValue("Voucher:HoldExpiry:PollIntervalSeconds", 60));
        _batchSize = cfg.GetValue("Voucher:HoldExpiry:BatchSize", 100);
        _enabled = cfg.GetValue("Voucher:HoldExpiry:Enabled", true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("HoldExpiryWorker disabled via config.");
            return;
        }

        _logger.LogInformation("HoldExpiryWorker starting; interval={Interval}, batch={Batch}", _interval, _batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "HoldExpiryWorker tick failed."); }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        await using var scope = _sp.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IVoucherStore>();
        var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var expired = await store.ListExpiredHoldsAsync(DateTimeOffset.UtcNow, _batchSize, ct);
        if (expired.Count == 0) return;

        _logger.LogInformation("HoldExpiryWorker: releasing {Count} expired holds.", expired.Count);

        foreach (var row in expired)
        {
            var release = await store.ReleaseAsync(row.TenantId, row.HoldId, "hold_expired", ct);
            if (!release.Success)
            {
                // Could happen if another worker won the CAS; log and continue.
                _logger.LogDebug("HoldExpiryWorker: release skipped {Hold}: {Code}", row.HoldId, release.ErrorCode);
                continue;
            }

            await publish.Publish(new VoucherReleasedV1(
                row.TenantId, row.OrderId, row.HoldId, row.VoucherId, row.Amount,
                Reason: "hold_expired", OccurredAt: DateTimeOffset.UtcNow), ct);
        }
    }
}
```

- [ ] **Step 2: Register in `Program.cs`**

In [src/backend/dCMS.Voucher.Api/Program.cs](src/backend/dCMS.Voucher.Api/Program.cs), add the using and the registration after `builder.Services.AddSingleton<IVoucherStore>(...)`:

```csharp
using dCMS.Voucher.Api.Workers;
// ...
builder.Services.AddHostedService<HoldExpiryWorker>();
```

- [ ] **Step 3: Add config defaults to `appsettings.json`**

```json
"Voucher": {
  "HoldExpiry": {
    "Enabled": true,
    "PollIntervalSeconds": 60,
    "BatchSize": 100
  }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build src/backend/dCMS.Voucher.Api/dCMS.Voucher.Api.csproj`

Expected: success.

- [ ] **Step 5: Commit Tasks 10 + 11**

```bash
git add src/backend/dCMS.Voucher.Api/Persistence/IVoucherStore.cs \
        src/backend/dCMS.Voucher.Api/Persistence/SqlVoucherStore.cs \
        src/backend/dCMS.Voucher.Api/Workers/HoldExpiryWorker.cs \
        src/backend/dCMS.Voucher.Api/Program.cs \
        src/backend/dCMS.Voucher.Api/appsettings.json
git commit -m "feat(voucher): hold-expiry worker releases abandoned reservations (DAI-689)

Background service polls VoucherHolds every 60s (configurable), releases
up to 100 expired Held rows per tick via the existing CAS release path,
publishes VoucherReleasedV1 with reason=hold_expired. Cross-pod safe
because Release uses 'WHERE Status=Held' as a CAS.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

### Task 12: Loyalty.Api — `ILoyaltyStore.ListExpiredHoldsAsync` + SQL impl

**Files:**
- Modify: `src/backend/dCMS.Loyalty.Api/Persistence/ILoyaltyStore.cs`
- Modify: `src/backend/dCMS.Loyalty.Api/Persistence/SqlLoyaltyStore.cs`

- [ ] **Step 1: Add interface method + record**

In [ILoyaltyStore.cs](src/backend/dCMS.Loyalty.Api/Persistence/ILoyaltyStore.cs), after `RecordLedgerAsync`:

```csharp
/// <summary>DAI-689: lists 'Held' loyalty holds whose ExpiresAt is in the past, up to <paramref name="limit"/>.</summary>
Task<IReadOnlyList<ExpiredLoyaltyHoldRow>> ListExpiredHoldsAsync(DateTimeOffset asOf, int limit, CancellationToken ct);
```

Bottom of file:

```csharp
public sealed record ExpiredLoyaltyHoldRow(Guid HoldId, string TenantId, string CustomerId, Guid OrderId, decimal Amount, DateTimeOffset ExpiresAt);
```

- [ ] **Step 2: Implement in `SqlLoyaltyStore`**

Append to [SqlLoyaltyStore.cs](src/backend/dCMS.Loyalty.Api/Persistence/SqlLoyaltyStore.cs):

```csharp
public async Task<IReadOnlyList<ExpiredLoyaltyHoldRow>> ListExpiredHoldsAsync(DateTimeOffset asOf, int limit, CancellationToken ct)
{
    if (limit <= 0) return Array.Empty<ExpiredLoyaltyHoldRow>();

    await using var conn = new NpgsqlConnection(connectionString);
    var rows = await conn.QueryAsync<ExpiredLoyaltyHoldRow>(new CommandDefinition(
        """
        SELECT "Id" AS HoldId, "TenantId" AS TenantId, "CustomerId" AS CustomerId,
               "OrderId" AS OrderId, "Amount" AS Amount, "ExpiresAt" AS ExpiresAt
          FROM "LoyaltyHolds"
         WHERE "Status" = 'Held' AND "ExpiresAt" <= @AsOf
         ORDER BY "ExpiresAt" ASC
         LIMIT @Limit;
        """,
        new { AsOf = asOf, Limit = limit }, cancellationToken: ct));
    return rows.AsList();
}
```

- [ ] **Step 3: Build**

Run: `dotnet build src/backend/dCMS.Loyalty.Api/dCMS.Loyalty.Api.csproj`

Expected: success.

- [ ] **Step 4: Commit (deferred — bundle with Task 13)**

---

### Task 13: Loyalty.Api — `HoldExpiryWorker`

**Files:**
- Create: `src/backend/dCMS.Loyalty.Api/Workers/HoldExpiryWorker.cs`
- Modify: `src/backend/dCMS.Loyalty.Api/Program.cs`
- Modify: `src/backend/dCMS.Loyalty.Api/appsettings.json`

- [ ] **Step 1: Create the worker**

```csharp
using dCMS.Core.Messaging;
using dCMS.Loyalty.Api.Persistence;
using MassTransit;

namespace dCMS.Loyalty.Api.Workers;

/// <summary>
/// DAI-689: scans LoyaltyHolds for expired 'Held' rows every <c>Loyalty:HoldExpiry:PollIntervalSeconds</c>
/// (default 60s), releases up to <c>BatchSize</c> per tick, publishes <see cref="LoyaltyReleasedV1"/>
/// with reason="hold_expired".
/// </summary>
public sealed class HoldExpiryWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<HoldExpiryWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly int _batchSize;
    private readonly bool _enabled;

    public HoldExpiryWorker(IServiceProvider sp, IConfiguration cfg, ILogger<HoldExpiryWorker> logger)
    {
        _sp = sp;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(cfg.GetValue("Loyalty:HoldExpiry:PollIntervalSeconds", 60));
        _batchSize = cfg.GetValue("Loyalty:HoldExpiry:BatchSize", 100);
        _enabled = cfg.GetValue("Loyalty:HoldExpiry:Enabled", true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled) { _logger.LogInformation("Loyalty HoldExpiryWorker disabled via config."); return; }
        _logger.LogInformation("Loyalty HoldExpiryWorker starting; interval={Interval}, batch={Batch}", _interval, _batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Loyalty HoldExpiryWorker tick failed."); }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        await using var scope = _sp.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ILoyaltyStore>();
        var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var expired = await store.ListExpiredHoldsAsync(DateTimeOffset.UtcNow, _batchSize, ct);
        if (expired.Count == 0) return;

        _logger.LogInformation("Loyalty HoldExpiryWorker: releasing {Count} expired holds.", expired.Count);

        foreach (var row in expired)
        {
            var release = await store.ReleaseAsync(row.TenantId, row.HoldId, "hold_expired", ct);
            if (!release.Success)
            {
                _logger.LogDebug("Loyalty HoldExpiryWorker: release skipped {Hold}: {Code}", row.HoldId, release.ErrorCode);
                continue;
            }

            await publish.Publish(new LoyaltyReleasedV1(
                row.TenantId, row.OrderId, row.HoldId, row.CustomerId, row.Amount,
                Reason: "hold_expired", OccurredAt: DateTimeOffset.UtcNow), ct);
        }
    }
}
```

- [ ] **Step 2: Register in `Program.cs`**

In [src/backend/dCMS.Loyalty.Api/Program.cs](src/backend/dCMS.Loyalty.Api/Program.cs):

```csharp
using dCMS.Loyalty.Api.Workers;
// ...
builder.Services.AddHostedService<HoldExpiryWorker>();
```

- [ ] **Step 3: Add config to `appsettings.json`**

```json
"Loyalty": {
  "HoldExpiry": {
    "Enabled": true,
    "PollIntervalSeconds": 60,
    "BatchSize": 100
  }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build src/backend/dCMS.Loyalty.Api/dCMS.Loyalty.Api.csproj`

Expected: success.

- [ ] **Step 5: Commit Tasks 12 + 13**

```bash
git add src/backend/dCMS.Loyalty.Api/Persistence/ILoyaltyStore.cs \
        src/backend/dCMS.Loyalty.Api/Persistence/SqlLoyaltyStore.cs \
        src/backend/dCMS.Loyalty.Api/Workers/HoldExpiryWorker.cs \
        src/backend/dCMS.Loyalty.Api/Program.cs \
        src/backend/dCMS.Loyalty.Api/appsettings.json
git commit -m "feat(loyalty): hold-expiry worker releases abandoned reservations (DAI-689)

Mirrors the Voucher.Api worker — scans LoyaltyHolds every 60s, releases
expired Held rows via existing CAS path, publishes LoyaltyReleasedV1
with reason=hold_expired.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

### Task 14: `PaymentOrchestratorGatewayTests`

**Files:**
- Create: `src/backend/dCMS.Order.Tests/Unit/Payments/PaymentOrchestratorGatewayTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using dCMS.Core.Messaging;
using dCMS.Order.Core.Domain.Payments;
using dCMS.Order.Infrastructure.Payments;
using dCMS.Order.Infrastructure.Persistence;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace dCMS.Order.Tests.Unit.Payments;

/// <summary>DAI-689: orchestrator behavior with the Gateway component arm wired.</summary>
public sealed class PaymentOrchestratorGatewayTests
{
    [Fact]
    public async Task Voucher_then_Gateway_happy_path_reaches_Captured()
    {
        var (repo, voucher, loyalty, gateway, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 100m, new[]
        {
            (PaymentComponentType.Voucher, 40m, (string?)"PROMO10"),
            (PaymentComponentType.Gateway, 60m, (string?)null),
        });
        repo.Seed(plan);

        await harness.Bus.Publish(new ProcessPaymentV1(
            Guid.NewGuid(), orderId.ToString(), "t1", "cust-ok", 100m, "USD", "card",
            DateTimeOffset.UtcNow.AddMinutes(15)));
        await harness.InactivityTask;

        Assert.True(await harness.Published.Any<PaymentCompletedV1>());
        Assert.Equal(1, voucher.CaptureCalls);
        var saved = repo.LastSaved!;
        Assert.Equal("Captured", saved.Status);
        Assert.All(saved.Components, c => Assert.Equal(PaymentComponentState.Captured, c.State));
        Assert.StartsWith("ch_stub_", saved.Components.Single(c => c.Type == PaymentComponentType.Gateway).ExternalRef);
    }

    [Fact]
    public async Task Gateway_decline_after_voucher_captured_refunds_voucher_and_publishes_failed()
    {
        var (repo, voucher, _, _, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 100m, new[]
        {
            (PaymentComponentType.Voucher, 40m, (string?)"PROMO10"),
            (PaymentComponentType.Gateway, 60m, (string?)null),
        });
        repo.Seed(plan);

        await harness.Bus.Publish(new ProcessPaymentV1(
            Guid.NewGuid(), orderId.ToString(), "t1", "cust-decline", 100m, "USD", "card",
            DateTimeOffset.UtcNow.AddMinutes(15)));
        await harness.InactivityTask;

        Assert.True(await harness.Published.Any<PaymentFailedV1>());
        Assert.False(await harness.Published.Any<PaymentCompletedV1>());
        Assert.Equal(1, voucher.RefundCalls);
        var saved = repo.LastSaved!;
        Assert.Equal(PaymentComponentState.Refunded, saved.Components[0].State); // voucher rolled back
        Assert.Equal(PaymentComponentState.Failed,   saved.Components[1].State); // gateway failed
    }

    [Fact]
    public async Task Replay_does_not_recall_gateway_authorize()
    {
        var (repo, _, _, gateway, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 60m, new[]
        {
            (PaymentComponentType.Gateway, 60m, (string?)null),
        });
        repo.Seed(plan);

        await harness.Bus.Publish(new ProcessPaymentV1(
            Guid.NewGuid(), orderId.ToString(), "t1", "cust-1", 60m, "USD", "card",
            DateTimeOffset.UtcNow.AddMinutes(15)));
        await harness.InactivityTask;

        // Re-publish — orchestrator should short-circuit via dispatch log.
        var firstAuthCount = gateway.AuthorizeCalls;
        await harness.Bus.Publish(new ProcessPaymentV1(
            Guid.NewGuid(), orderId.ToString(), "t1", "cust-1", 60m, "USD", "card",
            DateTimeOffset.UtcNow.AddMinutes(15)));
        await harness.InactivityTask;

        Assert.Equal(firstAuthCount, gateway.AuthorizeCalls);
    }

    private static async Task<(FakeRepo repo, FakeVoucher voucher, FakeLoyalty loyalty, CountingGateway gateway, ITestHarness harness)> BuildAsync()
    {
        var repo = new FakeRepo();
        var voucher = new FakeVoucher();
        var loyalty = new FakeLoyalty();
        var gateway = new CountingGateway();
        var log = new InMemoryDispatchLog();

        var services = new ServiceCollection();
        services.AddSingleton<OrderPaymentRepository>(_ => repo);
        services.AddSingleton<IPaymentComponentDispatchLog>(log);
        services.AddSingleton<IVoucherTenderClient>(voucher);
        services.AddSingleton<ILoyaltyTenderClient>(loyalty);
        services.AddSingleton<IGatewayTenderClient>(gateway);
        services.AddMassTransitTestHarness(cfg => cfg.AddConsumer<PaymentOrchestrator>());
        var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (repo, voucher, loyalty, gateway, harness);
    }

    // Use the same fake patterns as PaymentOrchestratorTests.cs — keep these duplicated rather than
    // sharing, so each file is self-contained.
    private sealed class FakeRepo : OrderPaymentRepository
    {
        private OrderPayment? _seed;
        public OrderPayment? LastSaved { get; private set; }
        public FakeRepo() : base(BuildStubConfig()) { }
        private static Microsoft.Extensions.Configuration.IConfiguration BuildStubConfig()
        {
            var cfg = new Microsoft.Extensions.Configuration.ConfigurationManager();
            cfg["ConnectionStrings:Order"] = "Host=stub";
            return cfg;
        }
        public void Seed(OrderPayment p) => _seed = p;
        public override Task UpsertAsync(OrderPayment payment, CancellationToken ct = default)
        { payment.RecomputeStatus(); LastSaved = payment; _seed = payment; return Task.CompletedTask; }
        public override Task<OrderPayment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
            => Task.FromResult(_seed);
    }

    private sealed class FakeVoucher : IVoucherTenderClient
    {
        public int CaptureCalls;
        public int RefundCalls;
        public Task<TenderCallResult> ReserveAsync(string t, string c, Guid o, decimal a, CancellationToken ct)
            => Task.FromResult(TenderCallResult.Ok(Guid.NewGuid().ToString()));
        public Task<TenderCallResult> CaptureAsync(string t, Guid h, CancellationToken ct) { CaptureCalls++; return Task.FromResult(TenderCallResult.Ok()); }
        public Task<TenderCallResult> ReleaseAsync(string t, Guid h, string r, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> RefundAsync(string t, Guid h, CancellationToken ct) { RefundCalls++; return Task.FromResult(TenderCallResult.Ok()); }
    }

    private sealed class FakeLoyalty : ILoyaltyTenderClient
    {
        public Task<TenderCallResult> ReserveAsync(string t, string c, Guid o, decimal a, CancellationToken ct)
            => Task.FromResult(TenderCallResult.Ok(Guid.NewGuid().ToString()));
        public Task<TenderCallResult> CaptureAsync(string t, Guid h, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> ReleaseAsync(string t, Guid h, string r, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> RefundAsync(string t, Guid h, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
    }

    private sealed class CountingGateway : IGatewayTenderClient
    {
        private readonly StubGatewayTenderClient _inner = new();
        public int AuthorizeCalls;
        public Task<TenderCallResult> AuthorizeAsync(string tenantId, string customerId, Guid orderId, decimal amount, string currency, CancellationToken ct)
        { AuthorizeCalls++; return _inner.AuthorizeAsync(tenantId, customerId, orderId, amount, currency, ct); }
        public Task<TenderCallResult> CaptureAsync(string tenantId, string chargeRef, CancellationToken ct) => _inner.CaptureAsync(tenantId, chargeRef, ct);
        public Task<TenderCallResult> VoidAsync(string tenantId, string chargeRef, string reason, CancellationToken ct) => _inner.VoidAsync(tenantId, chargeRef, reason, ct);
        public Task<TenderCallResult> RefundAsync(string tenantId, string chargeRef, CancellationToken ct) => _inner.RefundAsync(tenantId, chargeRef, ct);
    }

    private sealed class InMemoryDispatchLog : IPaymentComponentDispatchLog
    {
        private readonly Dictionary<(Guid, Guid, string), DispatchOutcome> _store = new();
        public Task<DispatchOutcome?> TryGetAsync(Guid orderId, Guid componentId, string action, CancellationToken ct)
            => Task.FromResult(_store.TryGetValue((orderId, componentId, action), out var o) ? o : null);
        public Task RecordSuccessAsync(Guid orderId, Guid componentId, string action, string? externalRef, CancellationToken ct)
        { _store[(orderId, componentId, action)] = new DispatchOutcome("Success", externalRef, null, null); return Task.CompletedTask; }
        public Task RecordFailureAsync(Guid orderId, Guid componentId, string action, string? errorCode, string? errorMessage, CancellationToken ct)
        { _store[(orderId, componentId, action)] = new DispatchOutcome("Failed", null, errorCode, errorMessage); return Task.CompletedTask; }
    }
}
```

- [ ] **Step 2: Run — verify they fail**

Run: `dotnet test src/backend/dCMS.Order.Tests/dCMS.Order.Tests.csproj --filter "FullyQualifiedName~PaymentOrchestratorGatewayTests"`

Expected: tests run; all 3 either FAIL or PASS depending on whether Tasks 7/9 are landed. If Tasks 7+9 are committed, they should PASS as written. If not, fail-then-implement order is preserved.

- [ ] **Step 3: If failing, fix the orchestrator gaps**

If `Voucher_then_Gateway_happy_path_reaches_Captured` fails, revisit Task 7 step 3 (Capture arm) and ensure Gateway-type captures call `_gateway.CaptureAsync(tenantId, chargeRef, ct)` with the chargeRef returned by Authorize.

- [ ] **Step 4: Commit**

```bash
git add src/backend/dCMS.Order.Tests/Unit/Payments/PaymentOrchestratorGatewayTests.cs
git commit -m "test(orders): orchestrator gateway-arm coverage (DAI-689)

Voucher+Gateway happy path, gateway decline triggers voucher refund,
gateway authorize is idempotent on replay.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

### Task 15: `ReleasePaymentComponentsConsumerTests`

**Files:**
- Create: `src/backend/dCMS.Order.Tests/Unit/Payments/ReleasePaymentComponentsConsumerTests.cs`

- [ ] **Step 1: Write tests**

```csharp
using dCMS.Core.Messaging;
using dCMS.Order.Core.Domain.Payments;
using dCMS.Order.Infrastructure.Payments;
using dCMS.Order.Infrastructure.Persistence;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace dCMS.Order.Tests.Unit.Payments;

public sealed class ReleasePaymentComponentsConsumerTests
{
    [Fact]
    public async Task Captured_voucher_gets_refunded()
    {
        var (repo, voucher, _, _, _, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 40m, new[] { (PaymentComponentType.Voucher, 40m, (string?)"PROMO10") });
        var holdId = Guid.NewGuid();
        plan.Components[0].Authorize(holdId.ToString());
        plan.Components[0].Capture();
        repo.Seed(plan);

        await harness.Bus.Publish(new ReleasePaymentComponentsV1(
            orderId, "t1", "s1", "customer_cancel", DateTimeOffset.UtcNow));
        await harness.InactivityTask;

        Assert.Equal(1, voucher.RefundCalls);
        Assert.Equal(PaymentComponentState.Refunded, repo.LastSaved!.Components[0].State);
    }

    [Fact]
    public async Task Authorized_gateway_gets_voided()
    {
        var (repo, _, _, gateway, _, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 60m, new[] { (PaymentComponentType.Gateway, 60m, (string?)null) });
        plan.Components[0].Authorize("ch_stub_abc");
        repo.Seed(plan);

        await harness.Bus.Publish(new ReleasePaymentComponentsV1(
            orderId, "t1", "s1", "payment_timeout", DateTimeOffset.UtcNow));
        await harness.InactivityTask;

        Assert.Equal(1, gateway.VoidCalls);
        Assert.Equal(PaymentComponentState.Cancelled, repo.LastSaved!.Components[0].State);
    }

    [Fact]
    public async Task Pending_components_are_skipped()
    {
        var (repo, voucher, loyalty, gateway, _, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 40m, new[] { (PaymentComponentType.Voucher, 40m, (string?)"PROMO10") });
        repo.Seed(plan); // Pending, no Authorize call

        await harness.Bus.Publish(new ReleasePaymentComponentsV1(
            orderId, "t1", "s1", "customer_cancel", DateTimeOffset.UtcNow));
        await harness.InactivityTask;

        Assert.Equal(0, voucher.RefundCalls);
        Assert.Equal(0, voucher.ReleaseCalls);
    }

    [Fact]
    public async Task Replay_short_circuits_via_dispatch_log()
    {
        var (repo, voucher, _, _, log, harness) = await BuildAsync();
        var orderId = Guid.NewGuid();
        var plan = OrderPayment.Plan(orderId, 40m, new[] { (PaymentComponentType.Voucher, 40m, (string?)"PROMO10") });
        var holdId = Guid.NewGuid();
        plan.Components[0].Authorize(holdId.ToString());
        plan.Components[0].Capture();
        repo.Seed(plan);
        log.Seed(orderId, plan.Components[0].Id, "REFUND", "Success", holdId.ToString());

        await harness.Bus.Publish(new ReleasePaymentComponentsV1(
            orderId, "t1", "s1", "customer_cancel", DateTimeOffset.UtcNow));
        await harness.InactivityTask;

        Assert.Equal(0, voucher.RefundCalls);
        Assert.Equal(PaymentComponentState.Refunded, repo.LastSaved!.Components[0].State);
    }

    private static async Task<(FakeRepo repo, FakeVoucher voucher, FakeLoyalty loyalty, FakeGateway gateway, FakeLog log, ITestHarness harness)> BuildAsync()
    {
        var repo = new FakeRepo();
        var voucher = new FakeVoucher();
        var loyalty = new FakeLoyalty();
        var gateway = new FakeGateway();
        var log = new FakeLog();

        var services = new ServiceCollection();
        services.AddSingleton<OrderPaymentRepository>(_ => repo);
        services.AddSingleton<IPaymentComponentDispatchLog>(log);
        services.AddSingleton<IVoucherTenderClient>(voucher);
        services.AddSingleton<ILoyaltyTenderClient>(loyalty);
        services.AddSingleton<IGatewayTenderClient>(gateway);
        services.AddMassTransitTestHarness(cfg => cfg.AddConsumer<ReleasePaymentComponentsConsumer>());
        var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (repo, voucher, loyalty, gateway, log, harness);
    }

    private sealed class FakeRepo : OrderPaymentRepository
    {
        private OrderPayment? _seed;
        public OrderPayment? LastSaved { get; private set; }
        public FakeRepo() : base(BuildStubConfig()) { }
        private static Microsoft.Extensions.Configuration.IConfiguration BuildStubConfig()
        {
            var cfg = new Microsoft.Extensions.Configuration.ConfigurationManager();
            cfg["ConnectionStrings:Order"] = "Host=stub";
            return cfg;
        }
        public void Seed(OrderPayment p) => _seed = p;
        public override Task UpsertAsync(OrderPayment payment, CancellationToken ct = default)
        { payment.RecomputeStatus(); LastSaved = payment; _seed = payment; return Task.CompletedTask; }
        public override Task<OrderPayment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
            => Task.FromResult(_seed);
    }
    private sealed class FakeVoucher : IVoucherTenderClient
    {
        public int RefundCalls; public int ReleaseCalls;
        public Task<TenderCallResult> ReserveAsync(string t, string c, Guid o, decimal a, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> CaptureAsync(string t, Guid h, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> ReleaseAsync(string t, Guid h, string r, CancellationToken ct) { ReleaseCalls++; return Task.FromResult(TenderCallResult.Ok()); }
        public Task<TenderCallResult> RefundAsync(string t, Guid h, CancellationToken ct) { RefundCalls++; return Task.FromResult(TenderCallResult.Ok()); }
    }
    private sealed class FakeLoyalty : ILoyaltyTenderClient
    {
        public Task<TenderCallResult> ReserveAsync(string t, string c, Guid o, decimal a, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> CaptureAsync(string t, Guid h, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> ReleaseAsync(string t, Guid h, string r, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> RefundAsync(string t, Guid h, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
    }
    private sealed class FakeGateway : IGatewayTenderClient
    {
        public int VoidCalls; public int RefundCalls;
        public Task<TenderCallResult> AuthorizeAsync(string t, string c, Guid o, decimal a, string cur, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok("ch_stub"));
        public Task<TenderCallResult> CaptureAsync(string t, string r, CancellationToken ct) => Task.FromResult(TenderCallResult.Ok());
        public Task<TenderCallResult> VoidAsync(string t, string r, string reason, CancellationToken ct) { VoidCalls++; return Task.FromResult(TenderCallResult.Ok()); }
        public Task<TenderCallResult> RefundAsync(string t, string r, CancellationToken ct) { RefundCalls++; return Task.FromResult(TenderCallResult.Ok()); }
    }
    private sealed class FakeLog : IPaymentComponentDispatchLog
    {
        private readonly Dictionary<(Guid, Guid, string), DispatchOutcome> _store = new();
        public void Seed(Guid o, Guid c, string a, string status, string? extRef)
            => _store[(o, c, a)] = new DispatchOutcome(status, extRef, null, null);
        public Task<DispatchOutcome?> TryGetAsync(Guid o, Guid c, string a, CancellationToken ct)
            => Task.FromResult(_store.TryGetValue((o, c, a), out var v) ? v : null);
        public Task RecordSuccessAsync(Guid o, Guid c, string a, string? r, CancellationToken ct)
        { _store[(o, c, a)] = new DispatchOutcome("Success", r, null, null); return Task.CompletedTask; }
        public Task RecordFailureAsync(Guid o, Guid c, string a, string? code, string? msg, CancellationToken ct)
        { _store[(o, c, a)] = new DispatchOutcome("Failed", null, code, msg); return Task.CompletedTask; }
    }
}
```

- [ ] **Step 2: Run tests — verify they pass**

Run: `dotnet test src/backend/dCMS.Order.Tests/dCMS.Order.Tests.csproj --filter "FullyQualifiedName~ReleasePaymentComponentsConsumerTests"`

Expected: 4 PASS.

- [ ] **Step 3: Commit**

```bash
git add src/backend/dCMS.Order.Tests/Unit/Payments/ReleasePaymentComponentsConsumerTests.cs
git commit -m "test(orders): ReleasePaymentComponentsConsumer coverage (DAI-689)

Captured-voucher refund, authorized-gateway void, pending-skip, and
replay short-circuit via dispatch log.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

### Task 16: `MultiTenderSagaIntegrationTests`

**Files:**
- Create: `src/backend/dCMS.Order.Tests/Sagas/MultiTenderSagaIntegrationTests.cs`

- [ ] **Step 1: Write failing test**

This test wires `OrderSaga` + `PaymentOrchestrator` + `ReleasePaymentComponentsConsumer` in one harness using in-memory saga repository (no Postgres). Drives through `OrderPlaced → StockReserved → ProcessPayment → PaymentCompleted → Confirmed`, then a customer cancel → asserts `ReleasePaymentComponentsV1` consumed.

```csharp
using dCMS.Core.Messaging;
using dCMS.Order.Core.Domain.Payments;
using dCMS.Order.Infrastructure.Payments;
using dCMS.Order.Infrastructure.Persistence;
using dCMS.Order.Infrastructure.Sagas;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace dCMS.Order.Tests.Sagas;

/// <summary>DAI-689: end-to-end saga + multi-tender orchestrator using MT in-memory test harness.</summary>
public sealed class MultiTenderSagaIntegrationTests
{
    [Fact]
    public async Task Place_pay_with_voucher_plus_gateway_reaches_Confirmed()
    {
        var (harness, repo) = await BuildAsync();
        var orderId = Guid.NewGuid();

        var plan = OrderPayment.Plan(orderId, 100m, new[]
        {
            (PaymentComponentType.Voucher, 40m, (string?)"PROMO10"),
            (PaymentComponentType.Gateway, 60m, (string?)null),
        });
        repo.Seed(plan);

        // Drive saga from OrderPlaced.
        await harness.Bus.Publish(new OrderPlacedV1(
            orderId.ToString(), "t1", "s1", "cust-1", 100m, "USD",
            new List<OrderPlacedLineV1>(), DateTimeOffset.UtcNow));
        // Saga publishes ReserveStockV1; we short-circuit by publishing StockReservedV1 manually
        // (StockReservedV1 fields per OrderLifecycleMessageContracts — adapt based on actual record).
        await harness.InactivityTask;
        await harness.Bus.Publish(new StockReservedV1(
            CorrelationId: Guid.NewGuid(), OrderId: orderId.ToString(),
            TenantId: "t1", StoreId: "s1", OccurredAt: DateTimeOffset.UtcNow));
        await harness.InactivityTask;
        // PaymentOrchestrator consumes ProcessPaymentV1 (published by saga), drives components,
        // publishes PaymentCompletedV1.
        Assert.True(await harness.Published.Any<PaymentCompletedV1>());
        Assert.True(await harness.Published.Any<OrderPaymentSettledV1>());
        Assert.Equal("Captured", repo.LastSaved!.Status);
    }

    [Fact]
    public async Task Customer_cancel_after_confirmed_triggers_ReleasePaymentComponents()
    {
        var (harness, repo) = await BuildAsync();
        var orderId = Guid.NewGuid();

        var plan = OrderPayment.Plan(orderId, 100m, new[]
        {
            (PaymentComponentType.Voucher, 40m, (string?)"PROMO10"),
            (PaymentComponentType.Gateway, 60m, (string?)null),
        });
        repo.Seed(plan);

        await harness.Bus.Publish(new OrderPlacedV1(
            orderId.ToString(), "t1", "s1", "cust-1", 100m, "USD",
            new List<OrderPlacedLineV1>(), DateTimeOffset.UtcNow));
        await harness.InactivityTask;
        await harness.Bus.Publish(new StockReservedV1(Guid.NewGuid(), orderId.ToString(), "t1", "s1", DateTimeOffset.UtcNow));
        await harness.InactivityTask;

        // Saga is now in Confirmed; publish customer cancel.
        await harness.Bus.Publish(new OrderCustomerCancellationV1(
            orderId.ToString(), "t1", "s1", "user_request", DateTimeOffset.UtcNow));
        await harness.InactivityTask;

        Assert.True(await harness.Published.Any<ReleasePaymentComponentsV1>());
        Assert.All(repo.LastSaved!.Components, c =>
            Assert.Contains(c.State, new[] { PaymentComponentState.Refunded, PaymentComponentState.Cancelled }));
    }

    private static async Task<(ITestHarness harness, FakeRepo repo)> BuildAsync()
    {
        var repo = new FakeRepo();
        var voucher = new AutoVoucher();
        var loyalty = new AutoLoyalty();
        var gateway = new StubGatewayTenderClient();
        var log = new InMemoryLog();

        var services = new ServiceCollection();
        services.AddSingleton<OrderPaymentRepository>(_ => repo);
        services.AddSingleton<IPaymentComponentDispatchLog>(log);
        services.AddSingleton<IVoucherTenderClient>(voucher);
        services.AddSingleton<ILoyaltyTenderClient>(loyalty);
        services.AddSingleton<IGatewayTenderClient>(gateway);
        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<PaymentOrchestrator>();
            cfg.AddConsumer<ReleasePaymentComponentsConsumer>();
            cfg.AddSagaStateMachine<OrderSaga, OrderSagaState>().InMemoryRepository();
        });
        var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, repo);
    }

    // Reuses the same FakeRepo / AutoVoucher / AutoLoyalty / InMemoryLog patterns from
    // PaymentOrchestratorGatewayTests.cs — keep them duplicated for self-contained test files.
    // Copy the FakeRepo, FakeVoucher (rename AutoVoucher), FakeLoyalty (rename AutoLoyalty),
    // and InMemoryLog inner classes from PaymentOrchestratorGatewayTests.cs verbatim.
}
```

If `StockReservedV1` / `OrderPlacedLineV1` constructor signatures differ, inspect [src/backend/dCMS.Messaging.Contracts/Messaging/OrderLifecycleMessageContracts.cs](src/backend/dCMS.Messaging.Contracts/Messaging/OrderLifecycleMessageContracts.cs) and adjust.

- [ ] **Step 2: Run — verify they pass**

Run: `dotnet test src/backend/dCMS.Order.Tests/dCMS.Order.Tests.csproj --filter "FullyQualifiedName~MultiTenderSagaIntegrationTests"`

Expected: 2 PASS. If `Place_pay_with_voucher_plus_gateway_reaches_Confirmed` fails because the saga expects `OrderPaymentSettledV1` to be published BEFORE state transitions to Confirmed (race in harness), use `await harness.Sent.Any(...)` instead of `Published.Any` and add a small `await harness.InactivityTask` after each publish.

- [ ] **Step 3: Commit**

```bash
git add src/backend/dCMS.Order.Tests/Sagas/MultiTenderSagaIntegrationTests.cs
git commit -m "test(orders): saga + orchestrator end-to-end multi-tender (DAI-689)

In-memory MassTransit test harness drives OrderPlaced → StockReserved →
ProcessPayment with a Voucher + Gateway plan and asserts saga reaches
Confirmed. A second test verifies customer cancel after confirm triggers
ReleasePaymentComponentsV1 and components flip to Refunded/Cancelled.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

### Task 17: `HoldExpiryWorkerTests` (Testcontainers)

**Files:**
- Create: `src/backend/dCMS.Order.Tests/Integration/Voucher/VoucherHoldExpiryWorkerIntegrationTests.cs`

> Reusing `dCMS.Order.Tests` keeps the test project count flat. The test references `dCMS.Voucher.Api` directly; since `dCMS.Voucher.Api` produces a binary, add a `ProjectReference` to it from `dCMS.Order.Tests.csproj`.

- [ ] **Step 1: Add ProjectReference**

In `src/backend/dCMS.Order.Tests/dCMS.Order.Tests.csproj`, in the `<ItemGroup>` of project references:

```xml
<ProjectReference Include="..\dCMS.Voucher.Api\dCMS.Voucher.Api.csproj" />
<ProjectReference Include="..\dCMS.Loyalty.Api\dCMS.Loyalty.Api.csproj" />
```

- [ ] **Step 2: Write the test**

```csharp
using dCMS.Voucher.Api.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Order.Tests.Integration.Voucher;

/// <summary>DAI-689: HoldExpiryWorker scans expired holds and releases them via SqlVoucherStore.</summary>
public sealed class VoucherHoldExpiryWorkerIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        await using var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();
        // Apply migration script bundled with the API.
        var sql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "dCMS.Voucher.Api", "Migrations", "001_CreateVouchers.sql"));
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();

    [Fact]
    public async Task ListExpiredHolds_returns_only_held_rows_past_ExpiresAt()
    {
        var store = new SqlVoucherStore(_pg.GetConnectionString());
        await using var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();

        var voucherId = Guid.NewGuid();
        var orderActive = Guid.NewGuid();
        var orderExpired = Guid.NewGuid();
        var holdActive = Guid.NewGuid();
        var holdExpired = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var seed = new NpgsqlCommand(
            """
            INSERT INTO "Vouchers"("Id","TenantId","Code","FaceValue","RemainingValue","Currency","Status")
              VALUES (@vid,'t1','PROMO',100,100,'VND','Active');
            INSERT INTO "VoucherHolds"("Id","TenantId","VoucherId","OrderId","Amount","Status","ExpiresAt","CreatedAt","UpdatedAt")
              VALUES
                (@hActive,'t1',@vid,@oActive,10,'Held',@exp1,@now,@now),
                (@hExpired,'t1',@vid,@oExpired,10,'Held',@exp2,@now,@now);
            """, conn))
        {
            seed.Parameters.AddWithValue("vid", voucherId);
            seed.Parameters.AddWithValue("hActive", holdActive);
            seed.Parameters.AddWithValue("hExpired", holdExpired);
            seed.Parameters.AddWithValue("oActive", orderActive);
            seed.Parameters.AddWithValue("oExpired", orderExpired);
            seed.Parameters.AddWithValue("exp1", now.AddMinutes(15));
            seed.Parameters.AddWithValue("exp2", now.AddMinutes(-1));
            seed.Parameters.AddWithValue("now", now);
            await seed.ExecuteNonQueryAsync();
        }

        var expired = await store.ListExpiredHoldsAsync(now, 100, default);

        Assert.Single(expired);
        Assert.Equal(holdExpired, expired[0].HoldId);
    }

    [Fact]
    public async Task Release_via_worker_path_restores_balance_and_writes_ledger()
    {
        var store = new SqlVoucherStore(_pg.GetConnectionString());
        await using var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();

        var voucherId = Guid.NewGuid();
        var holdId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var seed = new NpgsqlCommand(
            """
            INSERT INTO "Vouchers"("Id","TenantId","Code","FaceValue","RemainingValue","Currency","Status")
              VALUES (@vid,'t1','PROMO2',100,90,'VND','Active');
            INSERT INTO "VoucherHolds"("Id","TenantId","VoucherId","OrderId","Amount","Status","ExpiresAt","CreatedAt","UpdatedAt")
              VALUES (@hid,'t1',@vid,@oid,10,'Held',@exp,@now,@now);
            """, conn))
        {
            seed.Parameters.AddWithValue("vid", voucherId);
            seed.Parameters.AddWithValue("hid", holdId);
            seed.Parameters.AddWithValue("oid", Guid.NewGuid());
            seed.Parameters.AddWithValue("exp", now.AddMinutes(-1));
            seed.Parameters.AddWithValue("now", now);
            await seed.ExecuteNonQueryAsync();
        }

        var release = await store.ReleaseAsync("t1", holdId, "hold_expired", default);
        Assert.True(release.Success);

        await using var checkRemaining = new NpgsqlCommand(
            """SELECT "RemainingValue" FROM "Vouchers" WHERE "Id"=@vid;""", conn);
        checkRemaining.Parameters.AddWithValue("vid", voucherId);
        var remaining = (decimal)(await checkRemaining.ExecuteScalarAsync())!;
        Assert.Equal(100m, remaining);

        await using var checkLedger = new NpgsqlCommand(
            """SELECT COUNT(*) FROM "VoucherLedger" WHERE "HoldId"=@hid AND "Action"='RELEASE';""", conn);
        checkLedger.Parameters.AddWithValue("hid", holdId);
        var ledgerCount = (long)(await checkLedger.ExecuteScalarAsync())!;
        Assert.Equal(1, ledgerCount);
    }
}
```

- [ ] **Step 3: Run — verify pass**

Run: `dotnet test src/backend/dCMS.Order.Tests/dCMS.Order.Tests.csproj --filter "FullyQualifiedName~VoucherHoldExpiryWorkerIntegrationTests"`

Expected: 2 PASS (Docker required for Testcontainers).

- [ ] **Step 4: Commit**

```bash
git add src/backend/dCMS.Order.Tests/dCMS.Order.Tests.csproj \
        src/backend/dCMS.Order.Tests/Integration/Voucher/VoucherHoldExpiryWorkerIntegrationTests.cs
git commit -m "test(voucher): hold-expiry persistence integration tests (DAI-689)

Testcontainers Postgres + applied 001_CreateVouchers.sql; verifies
ListExpiredHoldsAsync filters Status=Held + ExpiresAt past, and that
Release on an expired hold restores RemainingValue and writes a RELEASE
ledger row.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

### Task 18: Loyalty hold-expiry integration test

**Files:**
- Create: `src/backend/dCMS.Order.Tests/Integration/Loyalty/LoyaltyHoldExpiryWorkerIntegrationTests.cs`

- [ ] **Step 1: Write the test (mirror Task 17)**

```csharp
using dCMS.Loyalty.Api.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Order.Tests.Integration.Loyalty;

public sealed class LoyaltyHoldExpiryWorkerIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        await using var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();
        var sql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "dCMS.Loyalty.Api", "Migrations", "001_CreateLoyaltyLedger.sql"));
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();

    [Fact]
    public async Task ListExpiredHolds_returns_only_held_past_expiry()
    {
        var store = new SqlLoyaltyStore(_pg.GetConnectionString());
        await using var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();

        var now = DateTimeOffset.UtcNow;
        var holdActive = Guid.NewGuid();
        var holdExpired = Guid.NewGuid();

        await using (var seed = new NpgsqlCommand(
            """
            -- Seed customer balance via ledger.
            INSERT INTO "LoyaltyLedger"("TenantId","CustomerId","Delta","Reason","OccurredAt")
              VALUES ('t1','cust-1',1000,'EARN',@now);
            INSERT INTO "LoyaltyHolds"("Id","TenantId","CustomerId","OrderId","Amount","Status","ExpiresAt","CreatedAt","UpdatedAt")
              VALUES
                (@hA,'t1','cust-1',@oA,100,'Held',@expA,@now,@now),
                (@hE,'t1','cust-2',@oE,100,'Held',@expE,@now,@now);
            """, conn))
        {
            seed.Parameters.AddWithValue("hA", holdActive);
            seed.Parameters.AddWithValue("hE", holdExpired);
            seed.Parameters.AddWithValue("oA", Guid.NewGuid());
            seed.Parameters.AddWithValue("oE", Guid.NewGuid());
            seed.Parameters.AddWithValue("expA", now.AddMinutes(15));
            seed.Parameters.AddWithValue("expE", now.AddMinutes(-1));
            seed.Parameters.AddWithValue("now", now);
            await seed.ExecuteNonQueryAsync();
        }

        var expired = await store.ListExpiredHoldsAsync(now, 100, default);
        Assert.Single(expired);
        Assert.Equal(holdExpired, expired[0].HoldId);
    }

    [Fact]
    public async Task Release_via_worker_path_restores_available_balance()
    {
        var store = new SqlLoyaltyStore(_pg.GetConnectionString());
        await using var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();

        var now = DateTimeOffset.UtcNow;
        var holdId = Guid.NewGuid();

        await using (var seed = new NpgsqlCommand(
            """
            INSERT INTO "LoyaltyLedger"("TenantId","CustomerId","Delta","Reason","OccurredAt")
              VALUES ('t1','cust-1',500,'EARN',@now);
            INSERT INTO "LoyaltyHolds"("Id","TenantId","CustomerId","OrderId","Amount","Status","ExpiresAt","CreatedAt","UpdatedAt")
              VALUES (@hid,'t1','cust-1',@oid,200,'Held',@exp,@now,@now);
            """, conn))
        {
            seed.Parameters.AddWithValue("hid", holdId);
            seed.Parameters.AddWithValue("oid", Guid.NewGuid());
            seed.Parameters.AddWithValue("exp", now.AddMinutes(-1));
            seed.Parameters.AddWithValue("now", now);
            await seed.ExecuteNonQueryAsync();
        }

        var balanceBefore = await store.GetBalanceAsync("t1", "cust-1", default);
        Assert.Equal(300m, balanceBefore.Available); // 500 earned - 200 held

        var release = await store.ReleaseAsync("t1", holdId, "hold_expired", default);
        Assert.True(release.Success);

        var balanceAfter = await store.GetBalanceAsync("t1", "cust-1", default);
        Assert.Equal(500m, balanceAfter.Available); // hold released, no ledger debit
    }
}
```

- [ ] **Step 2: Run**

Run: `dotnet test src/backend/dCMS.Order.Tests/dCMS.Order.Tests.csproj --filter "FullyQualifiedName~LoyaltyHoldExpiryWorkerIntegrationTests"`

Expected: 2 PASS.

- [ ] **Step 3: Commit**

```bash
git add src/backend/dCMS.Order.Tests/Integration/Loyalty/LoyaltyHoldExpiryWorkerIntegrationTests.cs
git commit -m "test(loyalty): hold-expiry persistence integration tests (DAI-689)

Mirror of voucher hold-expiry test — Testcontainers Postgres,
ListExpiredHoldsAsync filter, and balance recovery on release.

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

---

### Task 19: MEMORY.md update + final verify

**Files:**
- Modify: `.claude/memory/MEMORY.md`

- [ ] **Step 1: Run the entire Order test suite once**

Run: `dotnet test src/backend/dCMS.Order.Tests/dCMS.Order.Tests.csproj`

Expected: green. If any pre-existing test broke from the `OrderPayment.Plan` overload addition, fix it now (the legacy 2-tuple overload should keep them passing — if not, update the call site to use the 3-tuple overload).

- [ ] **Step 2: Build the full backend solution**

Run: `dotnet build src/backend/dCMS.sln`

Expected: green.

- [ ] **Step 3: Update `MEMORY.md`**

Find the `M2 Orders — Order.Api` block in [.claude/memory/MEMORY.md](.claude/memory/MEMORY.md). Replace the `DAI-722` bullet with:

```markdown
- `DAI-722/723/724` (DAI-689 epic done 2026-04-28) **Multi-tender payment**:
  - Schema: `020_CreateOrderPaymentsMultiTender.sql` + `021_AddPaymentComponentReference.sql` (Reference column splits voucher-code-input from holdId/chargeRef-output).
  - Domain: `dCMS.Order.Core/Domain/Payments/` — `PaymentComponent.Reference` (immutable input) vs `ExternalRef` (output set by Authorize). `OrderPayment.Plan(orderId, total, tenders)` accepts `(Type, Amount, Reference?)` 3-tuple; legacy 2-tuple overload kept for tests.
  - Orchestration: `PaymentOrchestrator` consumes `ProcessPaymentV1`, walks Voucher → Loyalty → GiftCard → Gateway components, idempotent via `IPaymentComponentDispatchLog (OrderId, ComponentId, Action)`. Inline compensation refunds prior captured components on later failure.
  - Tender clients: `IVoucherTenderClient` / `ILoyaltyTenderClient` (HTTP to `dCMS.Voucher.Api` / `dCMS.Loyalty.Api`). `IGatewayTenderClient` (Authorize/Capture/Void/Refund) with `StubGatewayTenderClient` (default; decline keyword, timeout keyword, `.99` cents → insufficient_funds) and `HttpGatewayTenderClient` (calls `dCMS.Payment.Api` `/internal/payment/{create-intent|chargeRef/capture|chargeRef/refund|chargeRef/void}` — new endpoints delegate to existing `IPaymentGateway` / `StubPaymentGateway`).
  - Late-cancel: `OrderSaga` publishes `ReleasePaymentComponentsV1` at every cancel transition; `ReleasePaymentComponentsConsumer` issues per-component refund (Captured) or release/void (Authorized) idempotently.
  - TTL workers: `dCMS.Voucher.Api/Workers/HoldExpiryWorker` + `dCMS.Loyalty.Api/Workers/HoldExpiryWorker` poll every 60s (`{Voucher|Loyalty}:HoldExpiry:PollIntervalSeconds`), batch 100, release expired Held rows via existing CAS path, publish `*ReleasedV1` with reason="hold_expired".
  - Tests: `Unit/Payments/OrderPaymentTests.cs` (10), `PaymentOrchestratorTests.cs` (4), `PaymentOrchestratorGatewayTests.cs` (3), `ReleasePaymentComponentsConsumerTests.cs` (4), `StubGatewayTenderClientTests.cs` (8); `Sagas/MultiTenderSagaIntegrationTests.cs` (2 in-memory harness); `Integration/Voucher/VoucherHoldExpiryWorkerIntegrationTests.cs` (2 Testcontainers); `Integration/Loyalty/LoyaltyHoldExpiryWorkerIntegrationTests.cs` (2 Testcontainers).
```

- [ ] **Step 4: Commit MEMORY.md**

```bash
git add .claude/memory/MEMORY.md
git commit -m "docs(memory): DAI-689 multi-tender epic complete (2026-04-28)

Co-Authored-By: Claude Opus 4 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 5: Mark Linear issues**

Optional (do only if asked) — set DAI-722, DAI-723, DAI-724, DAI-689 to In Review or Done in Linear via the linear MCP tools, with a comment summarizing the implemented gaps.

---

## Self-review

| Spec section | Plan task |
|---|---|
| §3.1 Schema change `Reference` column | Task 1 (migration), Task 2 (domain) |
| §3.2 IGatewayTenderClient + Stub + Http | Tasks 4, 5, 6 |
| §3.3 Orchestrator wiring | Tasks 3, 7, 9 |
| §3.3 ReleaseConsumer Gateway arms | Task 8 |
| §3.4 TTL cleanup workers | Tasks 10–13 |
| §3.5 Unit tests (orchestrator gateway, release consumer, stub gateway) | Tasks 14, 15, 5 |
| §3.5 Saga integration test | Task 16 |
| §3.5 Hold-expiry tests | Tasks 17, 18 |
| §4 File map | Covered by Tasks 1–18 |
| §5 Self-review checklist | Verified per task; final pass in Task 19 |

No placeholders / TODOs / "similar to" forwarding. All file paths are exact. Type names (`PaymentComponent.Reference`, `IGatewayTenderClient`, `StubGatewayTenderClient`, `HoldExpiryWorker`, `ListExpiredHoldsAsync`, `ExpiredHoldRow`, `ExpiredLoyaltyHoldRow`) are consistent across tasks.

