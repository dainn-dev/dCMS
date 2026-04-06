# Product Catalog — Plan 1: Database Schema + Domain Layer

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the database schema, domain models, repositories, and application services for the Product Catalog system (SPU/SKU model with multi-warehouse stock).

**Architecture:** Clean Architecture with 3 projects — dCMS.Core (domain models, interfaces, services — no infrastructure dependencies), dCMS.Infrastructure (SQL Server repositories via Dapper, OutboxProcessor background service), dCMS.Tests (xUnit unit + integration tests). Domain entities raise events; Application services persist events to OutboxEvents table atomically via Unit of Work.

**Tech Stack:** C# 12 / .NET 8, xUnit 2.x, FluentAssertions, Dapper (SQL Server), MediatR 12, DbUp (migrations), Moq (unit test mocks), Testcontainers (SQL Server integration tests)

**Spec:** `docs/superpowers/specs/2026-04-06-product-catalog-design.md` — Sections 1 and 2

---

## File Map

### dCMS.Core (new project)
```
src/backend/dCMS.Core/
  Exceptions/
    OutOfStockException.cs
    StockInvariantException.cs
    StockConcurrencyException.cs
  ValueObjects/
    Money.cs
    VariantPrice.cs
  Events/
    IDomainEvent.cs
    ProductCreated.cs
    ProductUpdated.cs
    ProductPublished.cs
    ProductArchived.cs
    StockUpdated.cs
  Models/
    Product.cs
    ProductVariant.cs
    ProductAttributeValue.cs
    ProductAttributeSnapshot.cs
    VariantStock.cs
    StockMovement.cs
    OutboxEvent.cs
  Interfaces/
    IProductRepository.cs
    IStockRepository.cs
    IOutboxRepository.cs
    IUnitOfWork.cs
  Services/
    ProductVariantGeneratorService.cs
    ProductService.cs
    StockService.cs
```

### dCMS.Infrastructure (new project)
```
src/backend/dCMS.Infrastructure/
  Database/
    UnitOfWork.cs
    DapperExtensions.cs
  Migrations/
    001_CreateCategories.sql
    002_CreateAttributes.sql
    003_CreateProducts.sql
    004_CreateVariants.sql
    005_CreateAttributeValues.sql
    006_CreatePricing.sql
    007_CreateImages.sql
    008_CreateInventory.sql
    009_CreateOutboxAndAudit.sql
  Repositories/
    ProductRepository.cs
    StockRepository.cs
    OutboxRepository.cs
  Background/
    OutboxProcessor.cs
  MigrationRunner.cs
```

### dCMS.Tests (new project)
```
src/backend/dCMS.Tests/
  Unit/
    ValueObjects/
      MoneyTests.cs
      VariantPriceTests.cs
    Models/
      ProductTests.cs
      VariantStockTests.cs
    Services/
      ProductVariantGeneratorServiceTests.cs
      ProductServiceTests.cs
      StockServiceTests.cs
  Integration/
    Fixtures/
      SqlServerFixture.cs
    Repositories/
      ProductRepositoryTests.cs
      StockRepositoryTests.cs
      OutboxRepositoryTests.cs
```

### Solution files
```
src/backend/
  dCMS.Core/dCMS.Core.csproj
  dCMS.Infrastructure/dCMS.Infrastructure.csproj
  dCMS.Tests/dCMS.Tests.csproj
  dCMS.sln
```

---

## Task 1: Create .NET solution and projects

**Files:**
- Create: `src/backend/dCMS.sln`
- Create: `src/backend/dCMS.Core/dCMS.Core.csproj`
- Create: `src/backend/dCMS.Infrastructure/dCMS.Infrastructure.csproj`
- Create: `src/backend/dCMS.Tests/dCMS.Tests.csproj`

- [ ] **Step 1: Scaffold solution and projects**

```bash
cd src/backend
dotnet new sln -n dCMS
dotnet new classlib -n dCMS.Core --framework net8.0
dotnet new classlib -n dCMS.Infrastructure --framework net8.0
dotnet new xunit -n dCMS.Tests --framework net8.0
dotnet sln add dCMS.Core/dCMS.Core.csproj
dotnet sln add dCMS.Infrastructure/dCMS.Infrastructure.csproj
dotnet sln add dCMS.Tests/dCMS.Tests.csproj
```

- [ ] **Step 2: Add project references**

```bash
dotnet add dCMS.Infrastructure/dCMS.Infrastructure.csproj reference dCMS.Core/dCMS.Core.csproj
dotnet add dCMS.Tests/dCMS.Tests.csproj reference dCMS.Core/dCMS.Core.csproj
dotnet add dCMS.Tests/dCMS.Tests.csproj reference dCMS.Infrastructure/dCMS.Infrastructure.csproj
```

- [ ] **Step 3: Add NuGet packages**

```bash
# Core
dotnet add dCMS.Core/dCMS.Core.csproj package MediatR --version 12.4.1

# Infrastructure
dotnet add dCMS.Infrastructure/dCMS.Infrastructure.csproj package Dapper --version 2.1.35
dotnet add dCMS.Infrastructure/dCMS.Infrastructure.csproj package Microsoft.Data.SqlClient --version 5.2.2
dotnet add dCMS.Infrastructure/dCMS.Infrastructure.csproj package dbup-sqlserver --version 5.0.17
dotnet add dCMS.Infrastructure/dCMS.Infrastructure.csproj package MediatR --version 12.4.1

# Tests
dotnet add dCMS.Tests/dCMS.Tests.csproj package FluentAssertions --version 6.12.2
dotnet add dCMS.Tests/dCMS.Tests.csproj package Moq --version 4.20.72
dotnet add dCMS.Tests/dCMS.Tests.csproj package Testcontainers.MsSql --version 3.10.0
dotnet add dCMS.Tests/dCMS.Tests.csproj package Microsoft.Data.SqlClient --version 5.2.2
```

- [ ] **Step 4: Delete boilerplate Class1.cs files**

```bash
rm src/backend/dCMS.Core/Class1.cs
rm src/backend/dCMS.Infrastructure/Class1.cs
```

- [ ] **Step 5: Verify solution builds**

```bash
cd src/backend
dotnet build
```
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 6: Commit**

```bash
git add src/backend/
git commit -m "chore: scaffold dCMS.Core, Infrastructure, Tests projects"
```

---

## Task 2: Domain exceptions and IDomainEvent

**Files:**
- Create: `src/backend/dCMS.Core/Exceptions/OutOfStockException.cs`
- Create: `src/backend/dCMS.Core/Exceptions/StockInvariantException.cs`
- Create: `src/backend/dCMS.Core/Exceptions/StockConcurrencyException.cs`
- Create: `src/backend/dCMS.Core/Events/IDomainEvent.cs`
- Create: `src/backend/dCMS.Core/Events/ProductCreated.cs`
- Create: `src/backend/dCMS.Core/Events/ProductUpdated.cs`
- Create: `src/backend/dCMS.Core/Events/ProductPublished.cs`
- Create: `src/backend/dCMS.Core/Events/ProductArchived.cs`
- Create: `src/backend/dCMS.Core/Events/StockUpdated.cs`

- [ ] **Step 1: Create IDomainEvent**

`src/backend/dCMS.Core/Events/IDomainEvent.cs`:
```csharp
namespace dCMS.Core.Events;

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}
```

- [ ] **Step 2: Create domain events**

`src/backend/dCMS.Core/Events/ProductCreated.cs`:
```csharp
namespace dCMS.Core.Events;

public record ProductCreated(
    string ProductId,
    string TenantId,
    string StoreId,
    DateTime OccurredAt) : IDomainEvent
{
    public ProductCreated(string productId, string tenantId, string storeId)
        : this(productId, tenantId, storeId, DateTime.UtcNow) { }
}
```

`src/backend/dCMS.Core/Events/ProductUpdated.cs`:
```csharp
namespace dCMS.Core.Events;

public record ProductUpdated(
    string ProductId,
    string TenantId,
    string StoreId,
    int SnapshotVersion,
    DateTime OccurredAt) : IDomainEvent
{
    public ProductUpdated(string productId, string tenantId, string storeId, int snapshotVersion)
        : this(productId, tenantId, storeId, snapshotVersion, DateTime.UtcNow) { }
}
```

`src/backend/dCMS.Core/Events/ProductPublished.cs`:
```csharp
namespace dCMS.Core.Events;

public record ProductPublished(
    string ProductId,
    string TenantId,
    string StoreId,
    DateTime OccurredAt) : IDomainEvent
{
    public ProductPublished(string productId, string tenantId, string storeId)
        : this(productId, tenantId, storeId, DateTime.UtcNow) { }
}
```

`src/backend/dCMS.Core/Events/ProductArchived.cs`:
```csharp
namespace dCMS.Core.Events;

public record ProductArchived(
    string ProductId,
    string TenantId,
    string StoreId,
    DateTime OccurredAt) : IDomainEvent
{
    public ProductArchived(string productId, string tenantId, string storeId)
        : this(productId, tenantId, storeId, DateTime.UtcNow) { }
}
```

`src/backend/dCMS.Core/Events/StockUpdated.cs`:
```csharp
namespace dCMS.Core.Events;

public record StockUpdated(
    string VariantId,
    string WarehouseId,
    int NewQuantity,
    DateTime OccurredAt) : IDomainEvent
{
    public StockUpdated(string variantId, string warehouseId, int newQuantity)
        : this(variantId, warehouseId, newQuantity, DateTime.UtcNow) { }
}
```

- [ ] **Step 3: Create exceptions**

`src/backend/dCMS.Core/Exceptions/OutOfStockException.cs`:
```csharp
namespace dCMS.Core.Exceptions;

public class OutOfStockException : Exception
{
    public string VariantId { get; }
    public int Requested { get; }
    public int Available { get; }

    public OutOfStockException(string variantId, int requested, int available)
        : base($"Insufficient stock for variant {variantId}: requested {requested}, available {available}")
    {
        VariantId = variantId;
        Requested = requested;
        Available = available;
    }
}
```

`src/backend/dCMS.Core/Exceptions/StockInvariantException.cs`:
```csharp
namespace dCMS.Core.Exceptions;

public class StockInvariantException : Exception
{
    public StockInvariantException(string message) : base(message) { }
}
```

`src/backend/dCMS.Core/Exceptions/StockConcurrencyException.cs`:
```csharp
namespace dCMS.Core.Exceptions;

public class StockConcurrencyException : Exception
{
    public string VariantId { get; }

    public StockConcurrencyException(string variantId)
        : base($"Concurrent stock modification detected for variant {variantId}. Please retry.")
    {
        VariantId = variantId;
    }
}
```

- [ ] **Step 4: Build to verify**

```bash
cd src/backend && dotnet build dCMS.Core/dCMS.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/backend/dCMS.Core/
git commit -m "feat: add domain events and exceptions"
```

---

## Task 3: Value objects — Money and VariantPrice

**Files:**
- Create: `src/backend/dCMS.Core/ValueObjects/Money.cs`
- Create: `src/backend/dCMS.Core/ValueObjects/VariantPrice.cs`
- Test: `src/backend/dCMS.Tests/Unit/ValueObjects/MoneyTests.cs`
- Test: `src/backend/dCMS.Tests/Unit/ValueObjects/VariantPriceTests.cs`

- [ ] **Step 1: Write failing tests for Money**

`src/backend/dCMS.Tests/Unit/ValueObjects/MoneyTests.cs`:
```csharp
using dCMS.Core.ValueObjects;
using FluentAssertions;

namespace dCMS.Tests.Unit.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Constructor_StoresAmountAndCurrency()
    {
        var money = new Money(250000L, "VND");
        money.Amount.Should().Be(250000L);
        money.Currency.Should().Be("VND");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ThrowsWhenAmountNotPositive(long amount)
    {
        var act = () => new Money(amount, "VND");
        act.Should().Throw<ArgumentException>().WithMessage("*amount*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("INVALID")]
    public void Constructor_ThrowsWhenCurrencyInvalid(string currency)
    {
        var act = () => new Money(1000L, currency);
        act.Should().Throw<ArgumentException>().WithMessage("*currency*");
    }

    [Fact]
    public void Equality_TwoMoneyWithSameAmountAndCurrency_AreEqual()
    {
        var a = new Money(100L, "VND");
        var b = new Money(100L, "VND");
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentCurrency_NotEqual()
    {
        var a = new Money(100L, "VND");
        var b = new Money(100L, "USD");
        a.Should().NotBe(b);
    }
}
```

- [ ] **Step 2: Run to verify fails**

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj --filter "FullyQualifiedName~MoneyTests" -v minimal
```
Expected: FAIL — `Money` type not found.

- [ ] **Step 3: Implement Money**

`src/backend/dCMS.Core/ValueObjects/Money.cs`:
```csharp
namespace dCMS.Core.ValueObjects;

public record Money
{
    private static readonly HashSet<string> ValidCurrencies = ["VND", "USD", "EUR", "JPY", "SGD", "THB"];

    public long Amount { get; }
    public string Currency { get; }

    public Money(long amount, string currency)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));
        if (string.IsNullOrWhiteSpace(currency) || !ValidCurrencies.Contains(currency.ToUpperInvariant()))
            throw new ArgumentException($"Invalid ISO 4217 currency code: '{currency}'.", nameof(currency));

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }
}
```

- [ ] **Step 4: Run tests — verify pass**

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj --filter "FullyQualifiedName~MoneyTests" -v minimal
```
Expected: All PASS.

- [ ] **Step 5: Write failing tests for VariantPrice**

`src/backend/dCMS.Tests/Unit/ValueObjects/VariantPriceTests.cs`:
```csharp
using dCMS.Core.ValueObjects;
using FluentAssertions;

namespace dCMS.Tests.Unit.ValueObjects;

public class VariantPriceTests
{
    private static readonly Money BasePrice = new(250000L, "VND");
    private static readonly DateTime Now = new(2026, 4, 6, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetActivePrice_BasePrice_NoTimeRange_ReturnsAlways()
    {
        var price = new VariantPrice(BasePrice, PriceType.Base, null, null);
        price.GetActivePrice(PriceType.Base, Now).Should().Be(BasePrice);
    }

    [Fact]
    public void GetActivePrice_SalePrice_WithinRange_Returns()
    {
        var start = Now.AddHours(-1);
        var end = Now.AddHours(1);
        var sale = new VariantPrice(new Money(199000L, "VND"), PriceType.Sale, start, end);

        sale.GetActivePrice(PriceType.Sale, Now).Should().Be(new Money(199000L, "VND"));
    }

    [Fact]
    public void GetActivePrice_SalePrice_BeforeRange_ReturnsNull()
    {
        var start = Now.AddHours(1);
        var end = Now.AddHours(2);
        var sale = new VariantPrice(new Money(199000L, "VND"), PriceType.Sale, start, end);

        sale.GetActivePrice(PriceType.Sale, Now).Should().BeNull();
    }

    [Fact]
    public void GetActivePrice_SalePrice_AfterRange_ReturnsNull()
    {
        var start = Now.AddHours(-2);
        var end = Now.AddHours(-1);
        var sale = new VariantPrice(new Money(199000L, "VND"), PriceType.Sale, start, end);

        sale.GetActivePrice(PriceType.Sale, Now).Should().BeNull();
    }

    [Fact]
    public void GetActivePrice_WrongType_ReturnsNull()
    {
        var price = new VariantPrice(BasePrice, PriceType.Base, null, null);
        price.GetActivePrice(PriceType.Sale, Now).Should().BeNull();
    }
}
```

- [ ] **Step 6: Run to verify fails**

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj --filter "FullyQualifiedName~VariantPriceTests" -v minimal
```
Expected: FAIL — `VariantPrice` not found.

- [ ] **Step 7: Implement VariantPrice and PriceType**

`src/backend/dCMS.Core/ValueObjects/VariantPrice.cs`:
```csharp
namespace dCMS.Core.ValueObjects;

public enum PriceType { Base, Sale, Member, Wholesale }

public record VariantPrice(
    Money Price,
    PriceType Type,
    DateTime? StartAt,
    DateTime? EndAt)
{
    public Money? GetActivePrice(PriceType requestedType, DateTime at)
    {
        if (Type != requestedType)
            return null;

        var afterStart = StartAt == null || at >= StartAt.Value;
        var beforeEnd = EndAt == null || at <= EndAt.Value;

        return afterStart && beforeEnd ? Price : null;
    }
}
```

- [ ] **Step 8: Run tests — verify pass**

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj --filter "FullyQualifiedName~ValueObjects" -v minimal
```
Expected: All PASS.

- [ ] **Step 9: Commit**

```bash
git add src/backend/dCMS.Core/ src/backend/dCMS.Tests/
git commit -m "feat: add Money and VariantPrice value objects with tests"
```

---

## Task 4: Domain models — ProductAttributeValue, ProductAttributeSnapshot, OutboxEvent

**Files:**
- Create: `src/backend/dCMS.Core/Models/ProductAttributeValue.cs`
- Create: `src/backend/dCMS.Core/Models/ProductAttributeSnapshot.cs`
- Create: `src/backend/dCMS.Core/Models/OutboxEvent.cs`

- [ ] **Step 1: Create ProductAttributeValue**

`src/backend/dCMS.Core/Models/ProductAttributeValue.cs`:
```csharp
namespace dCMS.Core.Models;

public class ProductAttributeValue
{
    public int Id { get; init; }
    public string ProductId { get; init; } = default!;
    public string? VariantId { get; init; }  // null = SPU-level
    public int AttributeId { get; init; }
    public int? AttributeValueId { get; init; }  // when Type=select
    public string? RawValue { get; init; }        // when Type=text|number|boolean
}
```

- [ ] **Step 2: Create ProductAttributeSnapshot**

`src/backend/dCMS.Core/Models/ProductAttributeSnapshot.cs`:
```csharp
namespace dCMS.Core.Models;

public class ProductAttributeSnapshot
{
    public string ProductId { get; init; } = default!;
    public string? VariantId { get; init; }
    public string Snapshot { get; set; } = "{}";  // JSON
    public int Version { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

- [ ] **Step 3: Create OutboxEvent**

`src/backend/dCMS.Core/Models/OutboxEvent.cs`:
```csharp
using System.Text.Json;
using dCMS.Core.Events;

namespace dCMS.Core.Models;

public class OutboxEvent
{
    public long Id { get; init; }
    public string EventType { get; init; } = default!;
    public string Payload { get; init; } = default!;
    public DateTime CreatedAt { get; init; }
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public string? Error { get; set; }

    public static OutboxEvent From(IDomainEvent domainEvent)
    {
        return new OutboxEvent
        {
            EventType = domainEvent.GetType().Name,
            Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            CreatedAt = domainEvent.OccurredAt
        };
    }
}
```

- [ ] **Step 4: Build**

```bash
cd src/backend && dotnet build dCMS.Core/dCMS.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/backend/dCMS.Core/
git commit -m "feat: add ProductAttributeValue, Snapshot, OutboxEvent models"
```

---

## Task 5: Domain model — VariantStock aggregate

**Files:**
- Create: `src/backend/dCMS.Core/Models/VariantStock.cs`
- Create: `src/backend/dCMS.Core/Models/StockMovement.cs`
- Test: `src/backend/dCMS.Tests/Unit/Models/VariantStockTests.cs`

- [ ] **Step 1: Write failing tests**

`src/backend/dCMS.Tests/Unit/Models/VariantStockTests.cs`:
```csharp
using dCMS.Core.Exceptions;
using dCMS.Core.Models;
using FluentAssertions;

namespace dCMS.Tests.Unit.Models;

public class VariantStockTests
{
    private static VariantStock MakeStock(int qty, int reserved = 0) =>
        new() { VariantId = "var_1", WarehouseId = "wh_1", Quantity = qty, ReservedQuantity = reserved };

    [Fact]
    public void AvailableQuantity_IsQuantityMinusReserved()
    {
        MakeStock(50, 10).AvailableQuantity.Should().Be(40);
    }

    [Fact]
    public void Reserve_WithSufficientStock_IncrementsReserved()
    {
        var stock = MakeStock(50, 10);
        stock.Reserve(5);
        stock.ReservedQuantity.Should().Be(15);
    }

    [Fact]
    public void Reserve_ExceedsAvailable_ThrowsOutOfStockException()
    {
        var stock = MakeStock(10, 8);  // available = 2
        var act = () => stock.Reserve(3);
        act.Should().Throw<OutOfStockException>()
            .Which.Available.Should().Be(2);
    }

    [Fact]
    public void Release_DecrementsReserved()
    {
        var stock = MakeStock(50, 10);
        stock.Release(4);
        stock.ReservedQuantity.Should().Be(6);
    }

    [Fact]
    public void Release_MoreThanReserved_ClampsToZero()
    {
        var stock = MakeStock(50, 5);
        stock.Release(10);
        stock.ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public void Adjust_PositiveDelta_IncreasesQuantity()
    {
        var stock = MakeStock(50);
        stock.Adjust(20);
        stock.Quantity.Should().Be(70);
    }

    [Fact]
    public void Adjust_WouldMakeQuantityLessThanReserved_ThrowsStockInvariantException()
    {
        var stock = MakeStock(50, 40);  // reserved=40, available=10
        var act = () => stock.Adjust(-20);  // would make qty=30, which is < reserved 40
        act.Should().Throw<StockInvariantException>();
    }

    [Fact]
    public void Adjust_ToExactlyReservedAmount_IsAllowed()
    {
        var stock = MakeStock(50, 40);
        stock.Adjust(-10);  // qty=40, reserved=40 → ok
        stock.Quantity.Should().Be(40);
    }
}
```

- [ ] **Step 2: Run to verify fails**

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj --filter "FullyQualifiedName~VariantStockTests" -v minimal
```
Expected: FAIL — `VariantStock` not found.

- [ ] **Step 3: Implement VariantStock**

`src/backend/dCMS.Core/Models/VariantStock.cs`:
```csharp
using dCMS.Core.Exceptions;

namespace dCMS.Core.Models;

public class VariantStock
{
    public int Id { get; init; }
    public string VariantId { get; init; } = default!;
    public string WarehouseId { get; init; } = default!;
    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public byte[] RowVersion { get; init; } = [];

    public int AvailableQuantity => Quantity - ReservedQuantity;

    public void Reserve(int qty)
    {
        if (qty > AvailableQuantity)
            throw new OutOfStockException(VariantId, qty, AvailableQuantity);
        ReservedQuantity += qty;
    }

    public void Release(int qty)
    {
        ReservedQuantity = Math.Max(0, ReservedQuantity - qty);
    }

    public void Adjust(int delta)
    {
        var newQuantity = Quantity + delta;
        if (newQuantity < ReservedQuantity)
            throw new StockInvariantException(
                $"Cannot adjust stock for {VariantId}: resulting quantity {newQuantity} would be less than reserved {ReservedQuantity}.");
        Quantity = newQuantity;
    }
}
```

- [ ] **Step 4: Implement StockMovement**

`src/backend/dCMS.Core/Models/StockMovement.cs`:
```csharp
namespace dCMS.Core.Models;

public enum StockMovementType { Import, Order, Cancel, Adjustment, Return, ReconciliationFix }

public class StockMovement
{
    public long Id { get; init; }
    public string VariantId { get; init; } = default!;
    public string WarehouseId { get; init; } = default!;
    public int Delta { get; init; }
    public StockMovementType Type { get; init; }
    public string? ReferenceId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string CreatedBy { get; init; } = default!;
}
```

- [ ] **Step 5: Run tests — verify pass**

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj --filter "FullyQualifiedName~VariantStockTests" -v minimal
```
Expected: All PASS.

- [ ] **Step 6: Commit**

```bash
git add src/backend/dCMS.Core/ src/backend/dCMS.Tests/
git commit -m "feat: add VariantStock aggregate with invariant enforcement and tests"
```

---

## Task 6: Domain model — Product aggregate

**Files:**
- Create: `src/backend/dCMS.Core/Models/Product.cs`
- Create: `src/backend/dCMS.Core/Models/ProductVariant.cs`
- Test: `src/backend/dCMS.Tests/Unit/Models/ProductTests.cs`

- [ ] **Step 1: Write failing tests**

`src/backend/dCMS.Tests/Unit/Models/ProductTests.cs`:
```csharp
using dCMS.Core.Events;
using dCMS.Core.Models;
using FluentAssertions;

namespace dCMS.Tests.Unit.Models;

public class ProductTests
{
    [Fact]
    public void Create_RaisesProductCreatedEvent()
    {
        var product = Product.Create("tenant1", "store1", 10, "Áo thun", "ao-thun");
        product.DomainEvents.Should().ContainSingle(e => e is ProductCreated);
        var evt = (ProductCreated)product.DomainEvents[0];
        evt.TenantId.Should().Be("tenant1");
        evt.StoreId.Should().Be("store1");
    }

    [Fact]
    public void Create_DefaultStatus_IsDraft()
    {
        var product = Product.Create("t1", "s1", 10, "Name", "name");
        product.Status.Should().Be(ProductStatus.Draft);
    }

    [Fact]
    public void Publish_FromDraft_SetsActiveAndRaisesProductPublished()
    {
        var product = Product.Create("t1", "s1", 10, "Name", "name");
        product.ClearDomainEvents();
        product.Publish();
        product.Status.Should().Be(ProductStatus.Active);
        product.DomainEvents.Should().ContainSingle(e => e is ProductPublished);
    }

    [Fact]
    public void Publish_FromArchived_ThrowsInvalidOperation()
    {
        var product = Product.Create("t1", "s1", 10, "Name", "name");
        product.Publish();
        product.Archive();
        product.ClearDomainEvents();
        var act = () => product.Publish();
        act.Should().Throw<InvalidOperationException>().WithMessage("*archived*");
    }

    [Fact]
    public void Archive_SetsArchivedStatusAndRaisesEvent()
    {
        var product = Product.Create("t1", "s1", 10, "Name", "name");
        product.Publish();
        product.ClearDomainEvents();
        product.Archive();
        product.Status.Should().Be(ProductStatus.Archived);
        product.DomainEvents.Should().ContainSingle(e => e is ProductArchived);
    }

    [Fact]
    public void ClearDomainEvents_EmptiesEventList()
    {
        var product = Product.Create("t1", "s1", 10, "Name", "name");
        product.ClearDomainEvents();
        product.DomainEvents.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run to verify fails**

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj --filter "FullyQualifiedName~ProductTests" -v minimal
```
Expected: FAIL — `Product` not found.

- [ ] **Step 3: Implement Product**

`src/backend/dCMS.Core/Models/Product.cs`:
```csharp
using dCMS.Core.Events;

namespace dCMS.Core.Models;

public enum ProductStatus { Draft, PendingApproval, Active, Hidden, Archived }

public class Product
{
    private readonly List<IDomainEvent> _events = [];

    public string Id { get; private set; } = default!;
    public string TenantId { get; private set; } = default!;
    public string StoreId { get; private set; } = default!;
    public int CategoryId { get; private set; }
    public string Name { get; private set; } = default!;  // JSON multilang
    public string Slug { get; private set; } = default!;
    public string? Description { get; private set; }  // JSON multilang
    public ProductStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public IReadOnlyList<IDomainEvent> DomainEvents => _events.AsReadOnly();

    private Product() { }

    public static Product Create(string tenantId, string storeId, int categoryId, string name, string slug)
    {
        var p = new Product
        {
            Id = $"prod_{Guid.NewGuid():N}",
            TenantId = tenantId,
            StoreId = storeId,
            CategoryId = categoryId,
            Name = name,
            Slug = slug,
            Status = ProductStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        p._events.Add(new ProductCreated(p.Id, tenantId, storeId));
        return p;
    }

    public void Publish()
    {
        if (Status == ProductStatus.Archived)
            throw new InvalidOperationException("Cannot publish an archived product.");
        Status = ProductStatus.Active;
        UpdatedAt = DateTime.UtcNow;
        _events.Add(new ProductPublished(Id, TenantId, StoreId));
    }

    public void Archive()
    {
        Status = ProductStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
        _events.Add(new ProductArchived(Id, TenantId, StoreId));
    }

    public void ClearDomainEvents() => _events.Clear();
}
```

- [ ] **Step 4: Implement ProductVariant**

`src/backend/dCMS.Core/Models/ProductVariant.cs`:
```csharp
using dCMS.Core.ValueObjects;

namespace dCMS.Core.Models;

public enum VariantStatus { Active, Inactive }

public class ProductVariant
{
    public string Id { get; init; } = default!;
    public string ProductId { get; init; } = default!;
    public string SKU { get; init; } = default!;
    public string CombinationHash { get; init; } = default!;
    public VariantStatus Status { get; set; } = VariantStatus.Active;
    public int SortOrder { get; set; }
    public List<ProductAttributeValue> Attributes { get; init; } = [];
    public List<VariantPrice> Prices { get; init; } = [];

    public Money? GetActivePrice(PriceType type, DateTime at) =>
        Prices
            .Where(p => p.Type == type)
            .OrderByDescending(p => p.StartAt ?? DateTime.MinValue)
            .Select(p => p.GetActivePrice(type, at))
            .FirstOrDefault(p => p != null);
}
```

- [ ] **Step 5: Run tests — verify pass**

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj --filter "FullyQualifiedName~ProductTests" -v minimal
```
Expected: All PASS.

- [ ] **Step 6: Commit**

```bash
git add src/backend/dCMS.Core/ src/backend/dCMS.Tests/
git commit -m "feat: add Product aggregate and ProductVariant model with tests"
```

---

## Task 7: Repository interfaces and IUnitOfWork

**Files:**
- Create: `src/backend/dCMS.Core/Interfaces/IProductRepository.cs`
- Create: `src/backend/dCMS.Core/Interfaces/IStockRepository.cs`
- Create: `src/backend/dCMS.Core/Interfaces/IOutboxRepository.cs`
- Create: `src/backend/dCMS.Core/Interfaces/IUnitOfWork.cs`

- [ ] **Step 1: Create IProductRepository**

`src/backend/dCMS.Core/Interfaces/IProductRepository.cs`:
```csharp
using dCMS.Core.Models;

namespace dCMS.Core.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(string id, string tenantId, CancellationToken ct = default);
    Task<Product?> GetBySlugAsync(string slug, string storeId, CancellationToken ct = default);
    Task<List<ProductVariant>> GetVariantsByProductIdAsync(string productId, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, string storeId, string? excludeProductId = null, CancellationToken ct = default);
    Task SaveAsync(Product product, CancellationToken ct = default);
    Task SaveVariantAsync(ProductVariant variant, CancellationToken ct = default);
    Task SaveVariantsBulkAsync(List<ProductVariant> variants, CancellationToken ct = default);
    Task SaveAttributeValuesAsync(List<ProductAttributeValue> values, CancellationToken ct = default);
    Task UpsertSnapshotAsync(ProductAttributeSnapshot snapshot, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create IStockRepository**

`src/backend/dCMS.Core/Interfaces/IStockRepository.cs`:
```csharp
using dCMS.Core.Models;

namespace dCMS.Core.Interfaces;

public interface IStockRepository
{
    Task<VariantStock?> GetAsync(string variantId, string warehouseId, CancellationToken ct = default);
    Task<int> GetTotalAvailableAsync(string variantId, string storeId, CancellationToken ct = default);
    Task UpdateStockAsync(VariantStock stock, CancellationToken ct = default);  // throws StockConcurrencyException on rowversion mismatch
    Task AppendMovementAsync(StockMovement movement, CancellationToken ct = default);
}
```

- [ ] **Step 3: Create IOutboxRepository**

`src/backend/dCMS.Core/Interfaces/IOutboxRepository.cs`:
```csharp
using dCMS.Core.Models;

namespace dCMS.Core.Interfaces;

public interface IOutboxRepository
{
    Task AppendAsync(OutboxEvent outboxEvent, CancellationToken ct = default);
}
```

- [ ] **Step 4: Create IUnitOfWork**

`src/backend/dCMS.Core/Interfaces/IUnitOfWork.cs`:
```csharp
namespace dCMS.Core.Interfaces;

public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

public interface IUnitOfWork
{
    Task<ITransaction> BeginTransactionAsync(CancellationToken ct = default);
}
```

- [ ] **Step 5: Build**

```bash
cd src/backend && dotnet build dCMS.Core/dCMS.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/backend/dCMS.Core/
git commit -m "feat: add repository interfaces and IUnitOfWork"
```

---

## Task 8: ProductVariantGeneratorService

**Files:**
- Create: `src/backend/dCMS.Core/Services/ProductVariantGeneratorService.cs`
- Test: `src/backend/dCMS.Tests/Unit/Services/ProductVariantGeneratorServiceTests.cs`

- [ ] **Step 1: Write failing tests**

`src/backend/dCMS.Tests/Unit/Services/ProductVariantGeneratorServiceTests.cs`:
```csharp
using dCMS.Core.Services;
using FluentAssertions;

namespace dCMS.Tests.Unit.Services;

public class ProductVariantGeneratorServiceTests
{
    private readonly ProductVariantGeneratorService _sut = new();

    [Fact]
    public void GenerateCombinations_TwoAxesTwoValues_ReturnsFourCombinations()
    {
        var axes = new List<AttributeWithValues>
        {
            new(AttributeId: 1, ValueIds: [5, 6]),
            new(AttributeId: 2, ValueIds: [8, 9])
        };

        var result = _sut.GenerateCombinations(axes);

        result.Should().HaveCount(4);
    }

    [Fact]
    public void GenerateCombinations_SingleAxis_ReturnsThatManyCombinations()
    {
        var axes = new List<AttributeWithValues>
        {
            new(AttributeId: 1, ValueIds: [5, 6, 7])
        };

        var result = _sut.GenerateCombinations(axes);

        result.Should().HaveCount(3);
    }

    [Fact]
    public void ComputeCombinationHash_SameAttributesDifferentOrder_ReturnsSameHash()
    {
        var combo1 = new VariantAttributeSet([new(1, 5), new(2, 8)]);
        var combo2 = new VariantAttributeSet([new(2, 8), new(1, 5)]);

        var hash1 = _sut.ComputeCombinationHash(combo1);
        var hash2 = _sut.ComputeCombinationHash(combo2);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeCombinationHash_DifferentValues_ReturnsDifferentHash()
    {
        var combo1 = new VariantAttributeSet([new(1, 5), new(2, 8)]);
        var combo2 = new VariantAttributeSet([new(1, 5), new(2, 9)]);

        _sut.ComputeCombinationHash(combo1)
            .Should().NotBe(_sut.ComputeCombinationHash(combo2));
    }

    [Fact]
    public void ComputeCombinationHash_CanonicalFormat_IsSortedByAttributeId()
    {
        // canonical: sort by AttrId → "attrId=valueId|attrId=valueId"
        // color(2)=red(5), size(7)=M(12) → "2=5|7=12"
        var combo = new VariantAttributeSet([new(7, 12), new(2, 5)]);
        var hash = _sut.ComputeCombinationHash(combo);
        hash.Should().NotBeNullOrEmpty().And.HaveLength(64);  // SHA-256 hex
    }
}
```

- [ ] **Step 2: Run to verify fails**

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj --filter "FullyQualifiedName~ProductVariantGeneratorServiceTests" -v minimal
```
Expected: FAIL.

- [ ] **Step 3: Implement service and supporting types**

`src/backend/dCMS.Core/Services/ProductVariantGeneratorService.cs`:
```csharp
using System.Security.Cryptography;
using System.Text;

namespace dCMS.Core.Services;

public record AttributeWithValues(int AttributeId, List<int> ValueIds);
public record AttributeAssignment(int AttributeId, int ValueId);
public record VariantAttributeSet(List<AttributeAssignment> Assignments);

public class ProductVariantGeneratorService
{
    public List<VariantAttributeSet> GenerateCombinations(List<AttributeWithValues> axes)
    {
        if (axes.Count == 0)
            return [];

        var result = new List<List<AttributeAssignment>> { [] };

        foreach (var axis in axes)
        {
            result = result
                .SelectMany(existing =>
                    axis.ValueIds.Select(valueId =>
                        existing.Append(new AttributeAssignment(axis.AttributeId, valueId)).ToList()))
                .ToList();
        }

        return result.Select(assignments => new VariantAttributeSet(assignments)).ToList();
    }

    public string ComputeCombinationHash(VariantAttributeSet combination)
    {
        var canonical = string.Join("|",
            combination.Assignments
                .OrderBy(a => a.AttributeId)
                .Select(a => $"{a.AttributeId}={a.ValueId}"));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
```

- [ ] **Step 4: Run tests — verify pass**

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj --filter "FullyQualifiedName~ProductVariantGeneratorServiceTests" -v minimal
```
Expected: All PASS.

- [ ] **Step 5: Commit**

```bash
git add src/backend/dCMS.Core/ src/backend/dCMS.Tests/
git commit -m "feat: add ProductVariantGeneratorService with cartesian product and hash"
```

---

## Task 9: Database migrations

**Files:**
- Create: `src/backend/dCMS.Infrastructure/Migrations/001_CreateCategories.sql` through `009_CreateOutboxAndAudit.sql`
- Create: `src/backend/dCMS.Infrastructure/MigrationRunner.cs`

- [ ] **Step 1: Create migration SQL files**

`src/backend/dCMS.Infrastructure/Migrations/001_CreateCategories.sql`:
```sql
CREATE TABLE Categories (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    TenantId    NVARCHAR(100) NOT NULL,
    ParentId    INT NULL REFERENCES Categories(Id),
    Path        NVARCHAR(500) NOT NULL DEFAULT '/',
    Depth       INT NOT NULL DEFAULT 0,
    Name        NVARCHAR(MAX) NOT NULL,
    Slug        NVARCHAR(200) NOT NULL,
    SortOrder   INT NOT NULL DEFAULT 0,
    INDEX IX_Categories_Tenant (TenantId),
    INDEX IX_Categories_Path (Path)
);
```

`src/backend/dCMS.Infrastructure/Migrations/002_CreateAttributes.sql`:
```sql
CREATE TABLE Attributes (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    TenantId    NVARCHAR(100) NOT NULL,
    Name        NVARCHAR(MAX) NOT NULL,
    Type        NVARCHAR(50) NOT NULL,
    IsVariant   BIT NOT NULL DEFAULT 0,
    Scope       NVARCHAR(20) NOT NULL DEFAULT 'tenant'
);

CREATE TABLE AttributeValues (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    AttributeId INT NOT NULL REFERENCES Attributes(Id),
    Value       NVARCHAR(MAX) NOT NULL,
    SortOrder   INT NOT NULL DEFAULT 0
);

CREATE TABLE CategoryAttributes (
    CategoryId  INT NOT NULL REFERENCES Categories(Id),
    AttributeId INT NOT NULL REFERENCES Attributes(Id),
    IsRequired  BIT NOT NULL DEFAULT 0,
    SortOrder   INT NOT NULL DEFAULT 0,
    PRIMARY KEY (CategoryId, AttributeId)
);

CREATE TABLE StoreAttributeOverrides (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    StoreId     NVARCHAR(100) NOT NULL,
    AttributeId INT NOT NULL REFERENCES Attributes(Id),
    IsEnabled   BIT NOT NULL DEFAULT 1,
    IsRequired  BIT NOT NULL DEFAULT 0,
    SortOrder   INT NOT NULL DEFAULT 0,
    CustomLabel NVARCHAR(MAX) NULL,
    UNIQUE (StoreId, AttributeId)
);
```

`src/backend/dCMS.Infrastructure/Migrations/003_CreateProducts.sql`:
```sql
CREATE TABLE Products (
    Id          NVARCHAR(50) PRIMARY KEY,
    TenantId    NVARCHAR(100) NOT NULL,
    StoreId     NVARCHAR(100) NOT NULL,
    CategoryId  INT NOT NULL REFERENCES Categories(Id),
    Name        NVARCHAR(MAX) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Slug        NVARCHAR(200) NOT NULL,
    Status      NVARCHAR(30) NOT NULL DEFAULT 'draft',
    SalesCount30d INT NOT NULL DEFAULT 0,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_Products_StoreSlug UNIQUE (StoreId, Slug),
    INDEX IX_Products_Tenant_Store_Status (TenantId, StoreId, Status),
    INDEX IX_Products_Category_Status (CategoryId, Status)
);
```

`src/backend/dCMS.Infrastructure/Migrations/004_CreateVariants.sql`:
```sql
CREATE TABLE ProductVariants (
    Id              NVARCHAR(50) PRIMARY KEY,
    ProductId       NVARCHAR(50) NOT NULL REFERENCES Products(Id),
    SKU             NVARCHAR(100) NOT NULL,
    CombinationHash CHAR(64) NOT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'active',
    SortOrder       INT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_Variants_ProductCombination UNIQUE (ProductId, CombinationHash),
    INDEX IX_Variants_Product (ProductId)
);

CREATE TABLE ProductAttributeValues (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    ProductId        NVARCHAR(50) NOT NULL REFERENCES Products(Id),
    VariantId        NVARCHAR(50) NULL REFERENCES ProductVariants(Id),
    AttributeId      INT NOT NULL REFERENCES Attributes(Id),
    AttributeValueId INT NULL REFERENCES AttributeValues(Id),
    RawValue         NVARCHAR(MAX) NULL,
    INDEX IX_AttrValues_Product (ProductId, VariantId)
);

CREATE TABLE ProductAttributeSnapshots (
    ProductId   NVARCHAR(50) NOT NULL REFERENCES Products(Id),
    VariantId   NVARCHAR(50) NULL REFERENCES ProductVariants(Id),
    Snapshot    NVARCHAR(MAX) NOT NULL DEFAULT '{}',
    Version     INT NOT NULL DEFAULT 1,
    UpdatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    PRIMARY KEY (ProductId, VariantId)
);
```

`src/backend/dCMS.Infrastructure/Migrations/005_CreatePricing.sql`:
```sql
CREATE TABLE VariantPrices (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    VariantId    NVARCHAR(50) NOT NULL REFERENCES ProductVariants(Id),
    PriceType    NVARCHAR(20) NOT NULL,
    Amount       BIGINT NOT NULL,
    CurrencyCode CHAR(3) NOT NULL,
    StartAt      DATETIME2 NULL,
    EndAt        DATETIME2 NULL,
    INDEX IX_VariantPrices_Variant (VariantId, PriceType)
);
```

`src/backend/dCMS.Infrastructure/Migrations/006_CreateImages.sql`:
```sql
CREATE TABLE ProductImages (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    ProductId NVARCHAR(50) NOT NULL REFERENCES Products(Id),
    VariantId NVARCHAR(50) NULL REFERENCES ProductVariants(Id),
    Url       NVARCHAR(1000) NOT NULL,
    CdnKey    NVARCHAR(500) NOT NULL,
    Checksum  CHAR(64) NOT NULL,
    Type      NVARCHAR(20) NOT NULL DEFAULT 'gallery',
    AltText   NVARCHAR(MAX) NULL,
    SortOrder INT NOT NULL DEFAULT 0,
    IsPrimary BIT NOT NULL DEFAULT 0,
    INDEX IX_Images_Product (ProductId)
);
```

`src/backend/dCMS.Infrastructure/Migrations/007_CreateInventory.sql`:
```sql
CREATE TABLE Warehouses (
    Id          NVARCHAR(50) PRIMARY KEY,
    TenantId    NVARCHAR(100) NOT NULL,
    StoreId     NVARCHAR(100) NOT NULL,
    Name        NVARCHAR(200) NOT NULL,
    Address     NVARCHAR(500) NULL,
    IsActive    BIT NOT NULL DEFAULT 1,
    INDEX IX_Warehouses_Store (TenantId, StoreId)
);

CREATE TABLE VariantStock (
    Id                INT IDENTITY(1,1) PRIMARY KEY,
    VariantId         NVARCHAR(50) NOT NULL REFERENCES ProductVariants(Id),
    WarehouseId       NVARCHAR(50) NOT NULL REFERENCES Warehouses(Id),
    Quantity          INT NOT NULL DEFAULT 0,
    ReservedQuantity  INT NOT NULL DEFAULT 0,
    RowVersion        ROWVERSION NOT NULL,
    CONSTRAINT UQ_VariantStock UNIQUE (VariantId, WarehouseId),
    INDEX IX_VariantStock_Variant (VariantId, WarehouseId)
);

CREATE TABLE StockMovements (
    Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
    VariantId   NVARCHAR(50) NOT NULL REFERENCES ProductVariants(Id),
    WarehouseId NVARCHAR(50) NOT NULL REFERENCES Warehouses(Id),
    Delta       INT NOT NULL,
    Type        NVARCHAR(30) NOT NULL,
    ReferenceId NVARCHAR(200) NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy   NVARCHAR(200) NOT NULL,
    INDEX IX_StockMovements_Variant (VariantId, WarehouseId, CreatedAt)
);
```

`src/backend/dCMS.Infrastructure/Migrations/008_CreateOutbox.sql`:
```sql
CREATE TABLE OutboxEvents (
    Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
    EventType   NVARCHAR(100) NOT NULL,
    Payload     NVARCHAR(MAX) NOT NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ProcessedAt DATETIME2 NULL,
    RetryCount  INT NOT NULL DEFAULT 0,
    Error       NVARCHAR(MAX) NULL,
    INDEX IX_Outbox_Pending (ProcessedAt, CreatedAt) WHERE ProcessedAt IS NULL
);

CREATE TABLE DeadLetterEvents (
    Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
    OriginalEventId BIGINT NOT NULL,
    EventType       NVARCHAR(100) NOT NULL,
    Payload         NVARCHAR(MAX) NOT NULL,
    FailureReason   NVARCHAR(MAX) NULL,
    FailedAt        DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ReprocessedAt   DATETIME2 NULL
);
```

`src/backend/dCMS.Infrastructure/Migrations/009_CreateAuditAndNotifications.sql`:
```sql
CREATE TABLE AuditLogs (
    Id         BIGINT IDENTITY(1,1) PRIMARY KEY,
    TenantId   NVARCHAR(100) NOT NULL,
    StoreId    NVARCHAR(100) NOT NULL,
    UserId     NVARCHAR(200) NOT NULL,
    UserRole   NVARCHAR(50) NOT NULL,
    Action     NVARCHAR(50) NOT NULL,
    EntityType NVARCHAR(50) NOT NULL,
    EntityId   NVARCHAR(100) NOT NULL,
    Diff       NVARCHAR(MAX) NULL,
    IpAddress  NVARCHAR(50) NULL,
    CreatedAt  DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    INDEX IX_AuditLogs_Entity (EntityType, EntityId),
    INDEX IX_AuditLogs_User (UserId, CreatedAt)
);

CREATE TABLE ApprovalComments (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    ProductId NVARCHAR(50) NOT NULL REFERENCES Products(Id),
    UserId    NVARCHAR(200) NOT NULL,
    Role      NVARCHAR(50) NOT NULL,
    Message   NVARCHAR(MAX) NOT NULL,
    Type      NVARCHAR(30) NOT NULL DEFAULT 'comment',
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    INDEX IX_ApprovalComments_Product (ProductId)
);

CREATE TABLE NotificationEvents (
    Id        BIGINT IDENTITY(1,1) PRIMARY KEY,
    TenantId  NVARCHAR(100) NOT NULL,
    UserId    NVARCHAR(200) NOT NULL,
    Type      NVARCHAR(50) NOT NULL,
    EntityId  NVARCHAR(100) NOT NULL,
    Message   NVARCHAR(500) NOT NULL,
    ReadAt    DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    INDEX IX_Notifications_User (UserId, ReadAt)
);
```

- [ ] **Step 2: Create MigrationRunner**

`src/backend/dCMS.Infrastructure/MigrationRunner.cs`:
```csharp
using DbUp;
using System.Reflection;

namespace dCMS.Infrastructure;

public static class MigrationRunner
{
    public static void Run(string connectionString)
    {
        EnsureDatabase.For.SqlDatabase(connectionString);

        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .WithTransaction()
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
            throw new InvalidOperationException("Database migration failed.", result.Error);
    }
}
```

- [ ] **Step 3: Mark SQL files as embedded resources**

Edit `src/backend/dCMS.Infrastructure/dCMS.Infrastructure.csproj` — add inside `<Project>`:
```xml
<ItemGroup>
  <EmbeddedResource Include="Migrations\*.sql" />
</ItemGroup>
```

- [ ] **Step 4: Build**

```bash
cd src/backend && dotnet build dCMS.Infrastructure/dCMS.Infrastructure.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/backend/dCMS.Infrastructure/
git commit -m "feat: add database migrations for product catalog schema"
```

---

## Task 10: Infrastructure — UnitOfWork and repositories

**Files:**
- Create: `src/backend/dCMS.Infrastructure/Database/UnitOfWork.cs`
- Create: `src/backend/dCMS.Infrastructure/Repositories/OutboxRepository.cs`
- Create: `src/backend/dCMS.Infrastructure/Repositories/ProductRepository.cs`
- Create: `src/backend/dCMS.Infrastructure/Repositories/StockRepository.cs`

- [ ] **Step 1: Create UnitOfWork**

`src/backend/dCMS.Infrastructure/Database/UnitOfWork.cs`:
```csharp
using System.Data;
using dCMS.Core.Interfaces;
using Microsoft.Data.SqlClient;

namespace dCMS.Infrastructure.Database;

public class SqlTransaction(IDbTransaction inner) : ITransaction
{
    public Task CommitAsync(CancellationToken ct = default)
    {
        inner.Commit();
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken ct = default)
    {
        inner.Rollback();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        inner.Dispose();
        return ValueTask.CompletedTask;
    }
}

public class UnitOfWork(string connectionString) : IUnitOfWork
{
    private SqlConnection? _connection;

    public SqlConnection GetConnection()
    {
        if (_connection == null)
        {
            _connection = new SqlConnection(connectionString);
            _connection.Open();
        }
        return _connection;
    }

    public async Task<ITransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var conn = GetConnection();
        var tx = conn.BeginTransaction();
        return new SqlTransaction(tx);
    }
}
```

- [ ] **Step 2: Create OutboxRepository**

`src/backend/dCMS.Infrastructure/Repositories/OutboxRepository.cs`:
```csharp
using Dapper;
using dCMS.Core.Interfaces;
using dCMS.Core.Models;
using dCMS.Infrastructure.Database;

namespace dCMS.Infrastructure.Repositories;

public class OutboxRepository(UnitOfWork uow) : IOutboxRepository
{
    public async Task AppendAsync(OutboxEvent outboxEvent, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO OutboxEvents (EventType, Payload, CreatedAt)
            VALUES (@EventType, @Payload, @CreatedAt)
            """;

        await uow.GetConnection().ExecuteAsync(sql, new
        {
            outboxEvent.EventType,
            outboxEvent.Payload,
            outboxEvent.CreatedAt
        });
    }
}
```

- [ ] **Step 3: Create ProductRepository**

`src/backend/dCMS.Infrastructure/Repositories/ProductRepository.cs`:
```csharp
using Dapper;
using dCMS.Core.Interfaces;
using dCMS.Core.Models;
using dCMS.Infrastructure.Database;

namespace dCMS.Infrastructure.Repositories;

public class ProductRepository(UnitOfWork uow) : IProductRepository
{
    public async Task<Product?> GetByIdAsync(string id, string tenantId, CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM Products WHERE Id = @Id AND TenantId = @TenantId";
        var row = await uow.GetConnection().QuerySingleOrDefaultAsync<ProductRow>(sql, new { Id = id, TenantId = tenantId });
        return row == null ? null : MapToProduct(row);
    }

    public async Task<Product?> GetBySlugAsync(string slug, string storeId, CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM Products WHERE Slug = @Slug AND StoreId = @StoreId";
        var row = await uow.GetConnection().QuerySingleOrDefaultAsync<ProductRow>(sql, new { Slug = slug, StoreId = storeId });
        return row == null ? null : MapToProduct(row);
    }

    public async Task<List<ProductVariant>> GetVariantsByProductIdAsync(string productId, CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM ProductVariants WHERE ProductId = @ProductId ORDER BY SortOrder";
        var rows = await uow.GetConnection().QueryAsync<VariantRow>(sql, new { ProductId = productId });
        return rows.Select(MapToVariant).ToList();
    }

    public async Task<bool> SlugExistsAsync(string slug, string storeId, string? excludeProductId = null, CancellationToken ct = default)
    {
        const string sql = """
            SELECT COUNT(1) FROM Products 
            WHERE Slug = @Slug AND StoreId = @StoreId
              AND (@ExcludeId IS NULL OR Id != @ExcludeId)
            """;
        var count = await uow.GetConnection().QuerySingleAsync<int>(sql, new { Slug = slug, StoreId = storeId, ExcludeId = excludeProductId });
        return count > 0;
    }

    public async Task SaveAsync(Product product, CancellationToken ct = default)
    {
        const string upsert = """
            MERGE Products AS target
            USING (VALUES (@Id, @TenantId, @StoreId, @CategoryId, @Name, @Slug, @Status, @CreatedAt, @UpdatedAt))
                AS source (Id, TenantId, StoreId, CategoryId, Name, Slug, Status, CreatedAt, UpdatedAt)
            ON target.Id = source.Id
            WHEN MATCHED THEN UPDATE SET
                Name = source.Name, Slug = source.Slug, Status = source.Status, UpdatedAt = source.UpdatedAt
            WHEN NOT MATCHED THEN INSERT (Id, TenantId, StoreId, CategoryId, Name, Slug, Status, CreatedAt, UpdatedAt)
                VALUES (source.Id, source.TenantId, source.StoreId, source.CategoryId, source.Name, source.Slug, source.Status, source.CreatedAt, source.UpdatedAt);
            """;

        await uow.GetConnection().ExecuteAsync(upsert, new
        {
            product.Id, product.TenantId, product.StoreId, product.CategoryId,
            product.Name, product.Slug,
            Status = product.Status.ToString().ToLowerInvariant(),
            product.CreatedAt, product.UpdatedAt
        });
    }

    public async Task SaveVariantAsync(ProductVariant variant, CancellationToken ct = default)
    {
        await SaveVariantsBulkAsync([variant], ct);
    }

    public async Task SaveVariantsBulkAsync(List<ProductVariant> variants, CancellationToken ct = default)
    {
        if (variants.Count == 0) return;
        const string sql = """
            INSERT INTO ProductVariants (Id, ProductId, SKU, CombinationHash, Status, SortOrder)
            VALUES (@Id, @ProductId, @SKU, @CombinationHash, @Status, @SortOrder)
            """;
        await uow.GetConnection().ExecuteAsync(sql, variants.Select(v => new
        {
            v.Id, v.ProductId, v.SKU, v.CombinationHash,
            Status = v.Status.ToString().ToLowerInvariant(),
            v.SortOrder
        }));
    }

    public async Task SaveAttributeValuesAsync(List<ProductAttributeValue> values, CancellationToken ct = default)
    {
        if (values.Count == 0) return;
        const string sql = """
            INSERT INTO ProductAttributeValues (ProductId, VariantId, AttributeId, AttributeValueId, RawValue)
            VALUES (@ProductId, @VariantId, @AttributeId, @AttributeValueId, @RawValue)
            """;
        await uow.GetConnection().ExecuteAsync(sql, values);
    }

    public async Task UpsertSnapshotAsync(ProductAttributeSnapshot snapshot, CancellationToken ct = default)
    {
        const string sql = """
            MERGE ProductAttributeSnapshots AS target
            USING (VALUES (@ProductId, @VariantId, @Snapshot, @Version, @UpdatedAt))
                AS source (ProductId, VariantId, Snapshot, Version, UpdatedAt)
            ON target.ProductId = source.ProductId AND (target.VariantId = source.VariantId OR (target.VariantId IS NULL AND source.VariantId IS NULL))
            WHEN MATCHED THEN UPDATE SET Snapshot = source.Snapshot, Version = source.Version, UpdatedAt = source.UpdatedAt
            WHEN NOT MATCHED THEN INSERT (ProductId, VariantId, Snapshot, Version, UpdatedAt)
                VALUES (source.ProductId, source.VariantId, source.Snapshot, source.Version, source.UpdatedAt);
            """;
        await uow.GetConnection().ExecuteAsync(sql, snapshot);
    }

    // --- Private mapping helpers ---

    private sealed class ProductRow
    {
        public string Id { get; init; } = default!;
        public string TenantId { get; init; } = default!;
        public string StoreId { get; init; } = default!;
        public int CategoryId { get; init; }
        public string Name { get; init; } = default!;
        public string Slug { get; init; } = default!;
        public string Status { get; init; } = default!;
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    private sealed class VariantRow
    {
        public string Id { get; init; } = default!;
        public string ProductId { get; init; } = default!;
        public string SKU { get; init; } = default!;
        public string CombinationHash { get; init; } = default!;
        public string Status { get; init; } = default!;
        public int SortOrder { get; init; }
    }

    private static Product MapToProduct(ProductRow row)
    {
        // Use reflection to populate internal setters (bypasses constructor for reconstitution)
        var p = (Product)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Product));
        typeof(Product).GetProperty(nameof(Product.Id))!.SetValue(p, row.Id);
        typeof(Product).GetProperty(nameof(Product.TenantId))!.SetValue(p, row.TenantId);
        typeof(Product).GetProperty(nameof(Product.StoreId))!.SetValue(p, row.StoreId);
        typeof(Product).GetProperty(nameof(Product.CategoryId))!.SetValue(p, row.CategoryId);
        typeof(Product).GetProperty(nameof(Product.Name))!.SetValue(p, row.Name);
        typeof(Product).GetProperty(nameof(Product.Slug))!.SetValue(p, row.Slug);
        typeof(Product).GetProperty(nameof(Product.Status))!.SetValue(p, Enum.Parse<ProductStatus>(row.Status, ignoreCase: true));
        typeof(Product).GetProperty(nameof(Product.CreatedAt))!.SetValue(p, row.CreatedAt);
        typeof(Product).GetProperty(nameof(Product.UpdatedAt))!.SetValue(p, row.UpdatedAt);
        return p;
    }

    private static ProductVariant MapToVariant(VariantRow row) => new()
    {
        Id = row.Id,
        ProductId = row.ProductId,
        SKU = row.SKU,
        CombinationHash = row.CombinationHash,
        Status = Enum.Parse<VariantStatus>(row.Status, ignoreCase: true),
        SortOrder = row.SortOrder
    };
}
```

- [ ] **Step 4: Create StockRepository**

`src/backend/dCMS.Infrastructure/Repositories/StockRepository.cs`:
```csharp
using Dapper;
using dCMS.Core.Exceptions;
using dCMS.Core.Interfaces;
using dCMS.Core.Models;
using dCMS.Infrastructure.Database;

namespace dCMS.Infrastructure.Repositories;

public class StockRepository(UnitOfWork uow) : IStockRepository
{
    public async Task<VariantStock?> GetAsync(string variantId, string warehouseId, CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM VariantStock WHERE VariantId = @VariantId AND WarehouseId = @WarehouseId";
        return await uow.GetConnection().QuerySingleOrDefaultAsync<VariantStock>(sql, new { VariantId = variantId, WarehouseId = warehouseId });
    }

    public async Task<int> GetTotalAvailableAsync(string variantId, string storeId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ISNULL(SUM(vs.Quantity - vs.ReservedQuantity), 0)
            FROM VariantStock vs
            JOIN Warehouses w ON vs.WarehouseId = w.Id
            WHERE vs.VariantId = @VariantId AND w.StoreId = @StoreId AND w.IsActive = 1
            """;
        return await uow.GetConnection().QuerySingleAsync<int>(sql, new { VariantId = variantId, StoreId = storeId });
    }

    public async Task UpdateStockAsync(VariantStock stock, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE VariantStock
            SET Quantity = @Quantity, ReservedQuantity = @ReservedQuantity
            WHERE VariantId = @VariantId AND WarehouseId = @WarehouseId AND RowVersion = @RowVersion
            """;
        var affected = await uow.GetConnection().ExecuteAsync(sql, new
        {
            stock.Quantity, stock.ReservedQuantity,
            stock.VariantId, stock.WarehouseId, stock.RowVersion
        });

        if (affected == 0)
            throw new StockConcurrencyException(stock.VariantId);
    }

    public async Task AppendMovementAsync(StockMovement movement, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO StockMovements (VariantId, WarehouseId, Delta, Type, ReferenceId, CreatedAt, CreatedBy)
            VALUES (@VariantId, @WarehouseId, @Delta, @Type, @ReferenceId, @CreatedAt, @CreatedBy)
            """;
        await uow.GetConnection().ExecuteAsync(sql, new
        {
            movement.VariantId, movement.WarehouseId, movement.Delta,
            Type = movement.Type.ToString(),
            movement.ReferenceId, movement.CreatedAt, movement.CreatedBy
        });
    }
}
```

- [ ] **Step 5: Build**

```bash
cd src/backend && dotnet build dCMS.Infrastructure/dCMS.Infrastructure.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/backend/dCMS.Infrastructure/
git commit -m "feat: add UnitOfWork, ProductRepository, StockRepository, OutboxRepository"
```

---

## Task 11: Application services — ProductService and StockService

**Files:**
- Create: `src/backend/dCMS.Core/Services/ProductService.cs`
- Create: `src/backend/dCMS.Core/Services/StockService.cs`
- Test: `src/backend/dCMS.Tests/Unit/Services/ProductServiceTests.cs`
- Test: `src/backend/dCMS.Tests/Unit/Services/StockServiceTests.cs`

- [ ] **Step 1: Write failing tests for ProductService**

`src/backend/dCMS.Tests/Unit/Services/ProductServiceTests.cs`:
```csharp
using dCMS.Core.Events;
using dCMS.Core.Interfaces;
using dCMS.Core.Models;
using dCMS.Core.Services;
using FluentAssertions;
using Moq;

namespace dCMS.Tests.Unit.Services;

public class ProductServiceTests
{
    private readonly Mock<IProductRepository> _productRepo = new();
    private readonly Mock<IOutboxRepository> _outboxRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ITransaction> _tx = new();
    private readonly ProductService _sut;

    public ProductServiceTests()
    {
        _uow.Setup(u => u.BeginTransactionAsync(default)).ReturnsAsync(_tx.Object);
        _sut = new ProductService(_productRepo.Object, _outboxRepo.Object, _uow.Object);
    }

    [Fact]
    public async Task CreateProduct_SlugNotTaken_SavesProductAndOutboxEvent()
    {
        _productRepo.Setup(r => r.SlugExistsAsync("ao-thun", "store1", null, default)).ReturnsAsync(false);

        var cmd = new CreateProductCommand("tenant1", "store1", 10, "{\"vi\":\"Áo thun\"}", "ao-thun");
        var result = await _sut.CreateProductAsync(cmd);

        result.Should().NotBeNull();
        _productRepo.Verify(r => r.SaveAsync(It.Is<Product>(p => p.Slug == "ao-thun"), default), Times.Once);
        _outboxRepo.Verify(r => r.AppendAsync(It.Is<OutboxEvent>(e => e.EventType == nameof(ProductCreated)), default), Times.Once);
        _tx.Verify(t => t.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task CreateProduct_SlugTaken_ThrowsInvalidOperationException()
    {
        _productRepo.Setup(r => r.SlugExistsAsync("ao-thun", "store1", null, default)).ReturnsAsync(true);

        var cmd = new CreateProductCommand("tenant1", "store1", 10, "{\"vi\":\"Áo thun\"}", "ao-thun");
        var act = async () => await _sut.CreateProductAsync(cmd);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*slug*");
    }

    [Fact]
    public async Task PublishProduct_DraftProduct_ChangesStatusAndEmitsEvent()
    {
        var product = Product.Create("tenant1", "store1", 10, "Name", "name");
        _productRepo.Setup(r => r.GetByIdAsync(product.Id, "tenant1", default)).ReturnsAsync(product);

        await _sut.PublishProductAsync(product.Id, "tenant1");

        _productRepo.Verify(r => r.SaveAsync(It.Is<Product>(p => p.Status == ProductStatus.Active), default), Times.Once);
        _outboxRepo.Verify(r => r.AppendAsync(It.Is<OutboxEvent>(e => e.EventType == nameof(ProductPublished)), default), Times.Once);
    }
}
```

- [ ] **Step 2: Run to verify fails**

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj --filter "FullyQualifiedName~ProductServiceTests" -v minimal
```
Expected: FAIL.

- [ ] **Step 3: Implement ProductService**

`src/backend/dCMS.Core/Services/ProductService.cs`:
```csharp
using dCMS.Core.Events;
using dCMS.Core.Interfaces;
using dCMS.Core.Models;

namespace dCMS.Core.Services;

public record CreateProductCommand(
    string TenantId,
    string StoreId,
    int CategoryId,
    string Name,
    string Slug,
    string? Description = null);

public class ProductService(
    IProductRepository productRepo,
    IOutboxRepository outboxRepo,
    IUnitOfWork uow)
{
    public async Task<Product> CreateProductAsync(CreateProductCommand cmd, CancellationToken ct = default)
    {
        if (await productRepo.SlugExistsAsync(cmd.Slug, cmd.StoreId, null, ct))
            throw new InvalidOperationException($"A product with slug '{cmd.Slug}' already exists in this store.");

        var product = Product.Create(cmd.TenantId, cmd.StoreId, cmd.CategoryId, cmd.Name, cmd.Slug);

        await using var tx = await uow.BeginTransactionAsync(ct);
        await productRepo.SaveAsync(product, ct);
        foreach (var evt in product.DomainEvents)
            await outboxRepo.AppendAsync(OutboxEvent.From(evt), ct);
        product.ClearDomainEvents();
        await tx.CommitAsync(ct);

        return product;
    }

    public async Task PublishProductAsync(string productId, string tenantId, CancellationToken ct = default)
    {
        var product = await productRepo.GetByIdAsync(productId, tenantId, ct)
            ?? throw new InvalidOperationException($"Product {productId} not found.");

        product.Publish();

        await using var tx = await uow.BeginTransactionAsync(ct);
        await productRepo.SaveAsync(product, ct);
        foreach (var evt in product.DomainEvents)
            await outboxRepo.AppendAsync(OutboxEvent.From(evt), ct);
        product.ClearDomainEvents();
        await tx.CommitAsync(ct);
    }

    public async Task ArchiveProductAsync(string productId, string tenantId, CancellationToken ct = default)
    {
        var product = await productRepo.GetByIdAsync(productId, tenantId, ct)
            ?? throw new InvalidOperationException($"Product {productId} not found.");

        product.Archive();

        await using var tx = await uow.BeginTransactionAsync(ct);
        await productRepo.SaveAsync(product, ct);
        foreach (var evt in product.DomainEvents)
            await outboxRepo.AppendAsync(OutboxEvent.From(evt), ct);
        product.ClearDomainEvents();
        await tx.CommitAsync(ct);
    }
}
```

- [ ] **Step 4: Write failing tests for StockService**

`src/backend/dCMS.Tests/Unit/Services/StockServiceTests.cs`:
```csharp
using dCMS.Core.Events;
using dCMS.Core.Exceptions;
using dCMS.Core.Interfaces;
using dCMS.Core.Models;
using dCMS.Core.Services;
using FluentAssertions;
using Moq;

namespace dCMS.Tests.Unit.Services;

public class StockServiceTests
{
    private readonly Mock<IStockRepository> _stockRepo = new();
    private readonly Mock<IOutboxRepository> _outboxRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ITransaction> _tx = new();
    private readonly StockService _sut;

    public StockServiceTests()
    {
        _uow.Setup(u => u.BeginTransactionAsync(default)).ReturnsAsync(_tx.Object);
        _sut = new StockService(_stockRepo.Object, _outboxRepo.Object, _uow.Object);
    }

    [Fact]
    public async Task AdjustStock_WithSufficientStock_SavesMovementAndEmitsEvent()
    {
        var stock = new VariantStock { VariantId = "var1", WarehouseId = "wh1", Quantity = 50, ReservedQuantity = 0 };
        _stockRepo.Setup(r => r.GetAsync("var1", "wh1", default)).ReturnsAsync(stock);

        var cmd = new AdjustStockCommand("var1", "wh1", 20, StockMovementType.Import, "user1");
        await _sut.AdjustStockAsync(cmd);

        _stockRepo.Verify(r => r.AppendMovementAsync(It.Is<StockMovement>(m => m.Delta == 20 && m.Type == StockMovementType.Import), default), Times.Once);
        _stockRepo.Verify(r => r.UpdateStockAsync(It.Is<VariantStock>(s => s.Quantity == 70), default), Times.Once);
        _outboxRepo.Verify(r => r.AppendAsync(It.Is<OutboxEvent>(e => e.EventType == nameof(StockUpdated)), default), Times.Once);
    }

    [Fact]
    public async Task ReserveStock_ExceedsAvailable_ThrowsOutOfStockException()
    {
        var stock = new VariantStock { VariantId = "var1", WarehouseId = "wh1", Quantity = 5, ReservedQuantity = 4 };
        _stockRepo.Setup(r => r.GetAsync("var1", "wh1", default)).ReturnsAsync(stock);

        var cmd = new ReserveStockCommand("var1", "wh1", 3, "order123");
        var act = async () => await _sut.ReserveStockAsync(cmd);

        await act.Should().ThrowAsync<OutOfStockException>();
    }

    [Fact]
    public async Task ReserveStock_StockNotFound_ThrowsInvalidOperationException()
    {
        _stockRepo.Setup(r => r.GetAsync("var1", "wh1", default)).ReturnsAsync((VariantStock?)null);

        var cmd = new ReserveStockCommand("var1", "wh1", 1, "order1");
        var act = async () => await _sut.ReserveStockAsync(cmd);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }
}
```

- [ ] **Step 5: Run to verify fails**

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj --filter "FullyQualifiedName~StockServiceTests" -v minimal
```
Expected: FAIL.

- [ ] **Step 6: Implement StockService**

`src/backend/dCMS.Core/Services/StockService.cs`:
```csharp
using dCMS.Core.Events;
using dCMS.Core.Exceptions;
using dCMS.Core.Interfaces;
using dCMS.Core.Models;

namespace dCMS.Core.Services;

public record AdjustStockCommand(string VariantId, string WarehouseId, int Delta, StockMovementType Type, string UserId, string? ReferenceId = null);
public record ReserveStockCommand(string VariantId, string WarehouseId, int Quantity, string OrderId);
public record ReleaseStockCommand(string VariantId, string WarehouseId, int Quantity, string OrderId);

public class StockService(
    IStockRepository stockRepo,
    IOutboxRepository outboxRepo,
    IUnitOfWork uow)
{
    private const int MaxRetries = 3;

    public async Task AdjustStockAsync(AdjustStockCommand cmd, CancellationToken ct = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            var stock = await stockRepo.GetAsync(cmd.VariantId, cmd.WarehouseId, ct)
                ?? throw new InvalidOperationException($"Stock record not found for variant {cmd.VariantId} in warehouse {cmd.WarehouseId}.");

            stock.Adjust(cmd.Delta);

            var movement = new StockMovement
            {
                VariantId = cmd.VariantId,
                WarehouseId = cmd.WarehouseId,
                Delta = cmd.Delta,
                Type = cmd.Type,
                ReferenceId = cmd.ReferenceId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = cmd.UserId
            };

            await using var tx = await uow.BeginTransactionAsync(ct);
            await stockRepo.AppendMovementAsync(movement, ct);
            await stockRepo.UpdateStockAsync(stock, ct);
            await outboxRepo.AppendAsync(OutboxEvent.From(new StockUpdated(cmd.VariantId, cmd.WarehouseId, stock.Quantity)), ct);
            await tx.CommitAsync(ct);
        }, cmd.VariantId);
    }

    public async Task ReserveStockAsync(ReserveStockCommand cmd, CancellationToken ct = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            var stock = await stockRepo.GetAsync(cmd.VariantId, cmd.WarehouseId, ct)
                ?? throw new InvalidOperationException($"Stock record not found for variant {cmd.VariantId}.");

            stock.Reserve(cmd.Quantity);

            var movement = new StockMovement
            {
                VariantId = cmd.VariantId,
                WarehouseId = cmd.WarehouseId,
                Delta = -cmd.Quantity,
                Type = StockMovementType.Order,
                ReferenceId = cmd.OrderId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            await using var tx = await uow.BeginTransactionAsync(ct);
            await stockRepo.AppendMovementAsync(movement, ct);
            await stockRepo.UpdateStockAsync(stock, ct);
            await outboxRepo.AppendAsync(OutboxEvent.From(new StockUpdated(cmd.VariantId, cmd.WarehouseId, stock.Quantity)), ct);
            await tx.CommitAsync(ct);
        }, cmd.VariantId);
    }

    public async Task ReleaseStockAsync(ReleaseStockCommand cmd, CancellationToken ct = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            var stock = await stockRepo.GetAsync(cmd.VariantId, cmd.WarehouseId, ct)
                ?? throw new InvalidOperationException($"Stock record not found for variant {cmd.VariantId}.");

            stock.Release(cmd.Quantity);

            var movement = new StockMovement
            {
                VariantId = cmd.VariantId,
                WarehouseId = cmd.WarehouseId,
                Delta = cmd.Quantity,
                Type = StockMovementType.Cancel,
                ReferenceId = cmd.OrderId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            await using var tx = await uow.BeginTransactionAsync(ct);
            await stockRepo.AppendMovementAsync(movement, ct);
            await stockRepo.UpdateStockAsync(stock, ct);
            await outboxRepo.AppendAsync(OutboxEvent.From(new StockUpdated(cmd.VariantId, cmd.WarehouseId, stock.Quantity)), ct);
            await tx.CommitAsync(ct);
        }, cmd.VariantId);
    }

    private async Task ExecuteWithRetryAsync(Func<Task> operation, string variantId)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (StockConcurrencyException) when (attempt < MaxRetries)
            {
                await Task.Delay(50 * attempt);
            }
            catch (StockConcurrencyException)
            {
                throw new StockConcurrencyException(variantId);
            }
        }
    }
}
```

- [ ] **Step 7: Run all unit tests — verify pass**

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj --filter "FullyQualifiedName~Unit" -v minimal
```
Expected: All PASS.

- [ ] **Step 8: Commit**

```bash
git add src/backend/dCMS.Core/ src/backend/dCMS.Tests/
git commit -m "feat: add ProductService and StockService with unit tests"
```

---

## Task 12: OutboxProcessor background service

**Files:**
- Create: `src/backend/dCMS.Infrastructure/Background/OutboxProcessor.cs`
- Test: `src/backend/dCMS.Tests/Unit/Background/OutboxProcessorTests.cs`

- [ ] **Step 1: Write failing test**

`src/backend/dCMS.Tests/Unit/Background/OutboxProcessorTests.cs`:
```csharp
using dCMS.Infrastructure.Background;
using FluentAssertions;
using MediatR;
using Microsoft.Data.SqlClient;
using Moq;

namespace dCMS.Tests.Unit.Background;

public class OutboxProcessorTests
{
    [Fact]
    public void OutboxProcessor_IsRegisteredAsIHostedService()
    {
        // Verify it implements IHostedService
        typeof(OutboxProcessor)
            .GetInterfaces()
            .Should().Contain(typeof(Microsoft.Extensions.Hosting.IHostedService));
    }
}
```

- [ ] **Step 2: Run to verify fails**

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj --filter "FullyQualifiedName~OutboxProcessorTests" -v minimal
```
Expected: FAIL.

- [ ] **Step 3: Implement OutboxProcessor**

First add the required package:
```bash
dotnet add dCMS.Infrastructure/dCMS.Infrastructure.csproj package Microsoft.Extensions.Hosting.Abstractions --version 8.0.1
```

`src/backend/dCMS.Infrastructure/Background/OutboxProcessor.cs`:
```csharp
using Dapper;
using dCMS.Core.Models;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace dCMS.Infrastructure.Background;

public class OutboxProcessor(
    string connectionString,
    IMediator mediator,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private const int MaxRetries = 5;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OutboxProcessor: unhandled error in batch");
            }
            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        // UPDLOCK + READPAST = select with row lock, skip locked rows (multi-instance safe)
        const string fetchSql = """
            SELECT TOP (@BatchSize) Id, EventType, Payload, CreatedAt, RetryCount
            FROM OutboxEvents WITH (UPDLOCK, READPAST)
            WHERE ProcessedAt IS NULL
            ORDER BY CreatedAt ASC
            """;

        var events = (await conn.QueryAsync<OutboxEvent>(fetchSql, new { BatchSize })).ToList();

        foreach (var evt in events)
        {
            try
            {
                var notification = DeserializeEvent(evt.EventType, evt.Payload);
                if (notification != null)
                    await mediator.Publish(notification, ct);

                await conn.ExecuteAsync(
                    "UPDATE OutboxEvents SET ProcessedAt = @Now WHERE Id = @Id",
                    new { Now = DateTime.UtcNow, evt.Id });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "OutboxProcessor: failed to process event {Id} (attempt {Retry})", evt.Id, evt.RetryCount + 1);

                if (evt.RetryCount + 1 >= MaxRetries)
                {
                    await conn.ExecuteAsync("""
                        INSERT INTO DeadLetterEvents (OriginalEventId, EventType, Payload, FailureReason, FailedAt)
                        VALUES (@OriginalEventId, @EventType, @Payload, @FailureReason, @FailedAt)
                        """,
                        new { OriginalEventId = evt.Id, evt.EventType, evt.Payload, FailureReason = ex.Message, FailedAt = DateTime.UtcNow });

                    await conn.ExecuteAsync(
                        "UPDATE OutboxEvents SET ProcessedAt = @Now, Error = @Error WHERE Id = @Id",
                        new { Now = DateTime.UtcNow, Error = $"DEAD_LETTERED: {ex.Message}", evt.Id });
                }
                else
                {
                    await conn.ExecuteAsync(
                        "UPDATE OutboxEvents SET RetryCount = RetryCount + 1, Error = @Error WHERE Id = @Id",
                        new { Error = ex.Message, evt.Id });
                }
            }
        }
    }

    private static INotification? DeserializeEvent(string eventType, string payload)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == eventType && typeof(INotification).IsAssignableFrom(t));

        if (type == null) return null;
        return (INotification?)JsonSerializer.Deserialize(payload, type);
    }
}
```

- [ ] **Step 4: Run tests — verify pass**

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj --filter "FullyQualifiedName~OutboxProcessorTests" -v minimal
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/backend/dCMS.Infrastructure/ src/backend/dCMS.Tests/
git commit -m "feat: add OutboxProcessor background service"
```

---

## Task 13: Integration tests

**Files:**
- Create: `src/backend/dCMS.Tests/Integration/Fixtures/SqlServerFixture.cs`
- Create: `src/backend/dCMS.Tests/Integration/Repositories/ProductRepositoryTests.cs`
- Create: `src/backend/dCMS.Tests/Integration/Repositories/StockRepositoryTests.cs`

- [ ] **Step 1: Create SQL Server test fixture (Testcontainers)**

`src/backend/dCMS.Tests/Integration/Fixtures/SqlServerFixture.cs`:
```csharp
using dCMS.Infrastructure;
using dCMS.Infrastructure.Database;
using Testcontainers.MsSql;

namespace dCMS.Tests.Integration.Fixtures;

public class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString { get; private set; } = default!;
    public UnitOfWork UnitOfWork { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        MigrationRunner.Run(ConnectionString);
        UnitOfWork = new UnitOfWork(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
```

- [ ] **Step 2: Write integration tests for ProductRepository**

`src/backend/dCMS.Tests/Integration/Repositories/ProductRepositoryTests.cs`:
```csharp
using dCMS.Core.Models;
using dCMS.Infrastructure.Repositories;
using dCMS.Tests.Integration.Fixtures;
using FluentAssertions;

namespace dCMS.Tests.Integration.Repositories;

[Collection("SqlServer")]
public class ProductRepositoryTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private ProductRepository MakeSut() => new(fixture.UnitOfWork);

    [Fact]
    public async Task SaveAsync_ThenGetById_ReturnsProduct()
    {
        // Arrange — need a category first
        await fixture.UnitOfWork.GetConnection().ExecuteAsync(
            "INSERT INTO Categories (TenantId, Name, Slug) VALUES ('t1', '{\"vi\":\"Cat\"}', 'cat')");
        var catId = await fixture.UnitOfWork.GetConnection().QuerySingleAsync<int>("SELECT MAX(Id) FROM Categories");

        var product = Product.Create("t1", "store1", catId, "{\"vi\":\"Áo thun\"}", "ao-thun-test");
        product.ClearDomainEvents();
        var sut = MakeSut();

        // Act
        await sut.SaveAsync(product);
        var loaded = await sut.GetByIdAsync(product.Id, "t1");

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(product.Id);
        loaded.Slug.Should().Be("ao-thun-test");
        loaded.Status.Should().Be(ProductStatus.Draft);
    }

    [Fact]
    public async Task SlugExistsAsync_ExistingSlug_ReturnsTrue()
    {
        var sut = MakeSut();
        var catId = await fixture.UnitOfWork.GetConnection().QuerySingleAsync<int>("SELECT MAX(Id) FROM Categories");
        var product = Product.Create("t1", "store_slug", catId, "Name", "unique-slug-123");
        product.ClearDomainEvents();
        await sut.SaveAsync(product);

        var exists = await sut.SlugExistsAsync("unique-slug-123", "store_slug");

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task SlugExistsAsync_DifferentStore_ReturnsFalse()
    {
        var sut = MakeSut();
        var catId = await fixture.UnitOfWork.GetConnection().QuerySingleAsync<int>("SELECT MAX(Id) FROM Categories");
        var product = Product.Create("t1", "store_a", catId, "Name", "shared-slug");
        product.ClearDomainEvents();
        await sut.SaveAsync(product);

        var exists = await sut.SlugExistsAsync("shared-slug", "store_b");

        exists.Should().BeFalse();
    }
}
```

- [ ] **Step 3: Write integration tests for StockRepository**

`src/backend/dCMS.Tests/Integration/Repositories/StockRepositoryTests.cs`:
```csharp
using dCMS.Core.Exceptions;
using dCMS.Core.Models;
using dCMS.Infrastructure.Repositories;
using dCMS.Tests.Integration.Fixtures;
using FluentAssertions;

namespace dCMS.Tests.Integration.Repositories;

[Collection("SqlServer")]
public class StockRepositoryTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private StockRepository MakeSut() => new(fixture.UnitOfWork);

    [Fact]
    public async Task UpdateStockAsync_WrongRowVersion_ThrowsStockConcurrencyException()
    {
        // Setup: insert a warehouse, product, variant, and stock row
        var conn = fixture.UnitOfWork.GetConnection();
        await conn.ExecuteAsync("INSERT INTO Warehouses (Id, TenantId, StoreId, Name) VALUES ('wh_test', 't1', 's1', 'Test')");
        var catId = await conn.QuerySingleAsync<int>("SELECT MAX(Id) FROM Categories");
        var prodId = $"prod_{Guid.NewGuid():N}";
        var varId = $"var_{Guid.NewGuid():N}";
        await conn.ExecuteAsync("INSERT INTO Products (Id, TenantId, StoreId, CategoryId, Name, Slug) VALUES (@Id, 't1', 's1', @Cat, 'N', @Slug)",
            new { Id = prodId, Cat = catId, Slug = $"slug-{varId}" });
        await conn.ExecuteAsync("INSERT INTO ProductVariants (Id, ProductId, SKU, CombinationHash) VALUES (@Id, @Pid, 'SKU1', 'hash1')",
            new { Id = varId, Pid = prodId });
        await conn.ExecuteAsync("INSERT INTO VariantStock (VariantId, WarehouseId, Quantity, ReservedQuantity) VALUES (@Vid, 'wh_test', 100, 0)",
            new { Vid = varId });

        var sut = MakeSut();
        var stock = await sut.GetAsync(varId, "wh_test");
        stock.Should().NotBeNull();

        // Simulate stale RowVersion by using wrong bytes
        var staleStock = new VariantStock
        {
            VariantId = varId,
            WarehouseId = "wh_test",
            Quantity = 200,
            ReservedQuantity = 0,
            RowVersion = new byte[8]  // wrong rowversion
        };

        var act = async () => await sut.UpdateStockAsync(staleStock);
        await act.Should().ThrowAsync<StockConcurrencyException>();
    }
}
```

- [ ] **Step 4: Add Dapper to Tests project and run**

```bash
dotnet add dCMS.Tests/dCMS.Tests.csproj package Dapper --version 2.1.35
```

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj --filter "FullyQualifiedName~Integration" -v normal
```
Expected: All PASS. (Testcontainers will pull SQL Server image on first run — may take ~2 minutes.)

- [ ] **Step 5: Run full test suite**

```bash
cd src/backend && dotnet test dCMS.Tests/dCMS.Tests.csproj -v minimal
```
Expected: All PASS. 0 failures.

- [ ] **Step 6: Commit**

```bash
git add src/backend/dCMS.Tests/
git commit -m "test: add integration tests for ProductRepository and StockRepository"
```

---

## Self-Review

### Spec coverage check

| Spec requirement | Covered in task |
|---|---|
| Categories with materialized path | Task 9 (migration 001) |
| Attributes with scope/type | Task 9 (migration 002) |
| Products with UNIQUE(StoreId,Slug) | Task 9 (migration 003) |
| ProductVariants with CombinationHash | Task 9 (migration 004) |
| ProductAttributeValues (normalized) | Task 9 + Task 10 (repo) |
| ProductAttributeSnapshot with Version | Task 9 + Task 10 (repo) |
| VariantPrices extensible model | Task 9 (migration 005) |
| ProductImages with Checksum | Task 9 (migration 006) |
| Warehouses + VariantStock + RowVersion | Task 9 (migration 007) |
| StockMovements (immutable, append-only) | Task 9 + Task 10 |
| OutboxEvents + DeadLetterEvents | Task 9 (migration 008) |
| AuditLogs + ApprovalComments + Notifications | Task 9 (migration 009) |
| Money value object | Task 3 |
| VariantPrice value object | Task 3 |
| Product aggregate with domain events | Task 6 |
| VariantStock with Reserve/Release/Adjust invariants | Task 5 |
| StockMovement as immutable Entity | Task 5 |
| ProductVariantGeneratorService (cartesian + hash) | Task 8 |
| Repository interfaces + IUnitOfWork | Task 7 |
| ProductService (create/publish/archive) + atomic outbox | Task 11 |
| StockService (adjust/reserve/release) + retry | Task 11 |
| OutboxProcessor (batch + UPDLOCK/READPAST + DLQ) | Task 12 |
| Integration tests with Testcontainers | Task 13 |

**Not in Plan 1 (covered in Plan 2/3/4):**
- Elasticsearch indexing pipeline (IndexingWorker, StockSyncWorker)
- Commerce API controllers
- Redis caching layer
- Backoffice UI

All Section 1 and Section 2 requirements from the spec are covered.
