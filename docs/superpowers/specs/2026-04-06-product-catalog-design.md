# Product Catalog & Inventory Design — dCMS

**Date:** 2026-04-06 (revised: microservices pivot)
**Status:** Approved
**Scope:** Catalog Service + Inventory Service — two separate ASP.NET Core microservices with independent databases.

> **Phase boundary:**
> - **Phase 1 (current):** Catalog Service + Inventory Service as independent microservices. Deliverable = REST APIs + ES indexing pipeline.
> - **Phase 2 (future):** Next.js storefront consuming Phase 1 APIs. Out of scope.
>
> **Microservices pivot:** This spec was originally designed as Umbraco-embedded. It has been revised to reflect physical microservice boundaries. Umbraco = Backoffice UI + Content Service only. Catalog and Inventory are pure ASP.NET Core services with separate SQL Server databases.

---

## Constraints & Key Decisions

| Dimension | Decision |
|---|---|
| Architecture | Physical microservices — Catalog Service (:5001) + Inventory Service (:5002) |
| Product scope | **Store level** — catalog independent per Store |
| Category/Attribute | **Hybrid** — Category tree at Tenant level, Store adds custom attributes |
| Pricing | **basePrice** on SKU, Promotions system separate (out of scope) |
| Stock | **Multi-warehouse** — per SKU per warehouse, in Inventory Service |
| Images | **External storage** (S3/Azure Blob), URL stored in Catalog DB |
| Backoffice | **Custom Umbraco Section** calling Catalog API + Inventory API directly |
| Sync pattern | **Event-driven async** — OutboxProcessor → RabbitMQ (MassTransit) |
| Currency | 1 store = 1 base currency; multi-currency display at render time |

---

## Section 1: Database Schema

### Catalog Service DB

```sql
-- CATEGORIES (Tenant-level, materialized path)
Categories
  Id            int PK
  TenantId      nvarchar
  ParentId      int? FK → Categories
  Path          nvarchar      -- materialized path: "/1/5/12/"
  Depth         int
  Name          nvarchar(max) -- JSON: {"vi":"Thời trang","en":"Fashion"}
  Slug          nvarchar
  SortOrder     int

-- ATTRIBUTE DEFINITIONS
Attributes
  Id            int PK
  TenantId      nvarchar
  Name          nvarchar(max) -- JSON multilang
  Type          nvarchar      -- select | text | number | boolean
  IsVariant     bit           -- true = variant axis (SKU-level)
  Scope         nvarchar      -- tenant | store

CategoryAttributes
  CategoryId    int FK → Categories
  AttributeId   int FK → Attributes
  IsRequired    bit
  SortOrder     int

AttributeValues               -- predefined options for Type=select
  Id            int PK
  AttributeId   int FK → Attributes
  Value         nvarchar(max) -- JSON multilang
  SortOrder     int

-- STORE OVERRIDES
StoreAttributeOverrides
  Id            int PK
  StoreId       nvarchar
  AttributeId   int FK → Attributes
  IsEnabled     bit           -- store can disable tenant attribute
  IsRequired    bit           -- override CategoryAttribute.IsRequired
  SortOrder     int           -- override display order
  CustomLabel   nvarchar(max)? -- optional relabel (JSON)

-- PRODUCTS (SPU — Store level)
Products
  Id            nvarchar PK   -- prod_{uuid}
  TenantId      nvarchar
  StoreId       nvarchar
  CategoryId    int FK → Categories
  Name          nvarchar(max) -- JSON multilang
  Description   nvarchar(max) -- JSON multilang
  Slug          nvarchar
  Status        nvarchar      -- draft|pending_approval|active|hidden|archived
  SalesCount30d int           -- updated daily by analytics job (read by IndexingWorker)
  CreatedAt     datetime2
  UpdatedAt     datetime2

  CONSTRAINT UQ_Products_StoreSlug UNIQUE (StoreId, Slug)
  INDEX IX_Products_Tenant_Store_Status (TenantId, StoreId, Status)
  INDEX IX_Products_Category_Status (CategoryId, Status)

-- ProductStatus lifecycle:
--   draft → pending_approval → active → hidden | archived
--   "out_of_stock" is NOT a status — computed from VariantStock (Inventory Service)

-- PRODUCT VARIANTS (SKU)
ProductVariants
  Id              nvarchar PK   -- var_{uuid}
  ProductId       nvarchar FK → Products
  SKU             nvarchar
  CombinationHash char(64)      -- SHA-256 of canonical "attrId=valueId|..." sorted by attrId
  Status          nvarchar      -- active | inactive
  SortOrder       int

  CONSTRAINT UQ_Variants_ProductCombination UNIQUE (ProductId, CombinationHash)
  -- SKU unique per store: enforced at app layer (via Products.StoreId join)

-- CombinationHash canonical format:
--   Sort attribute assignments by AttributeId ASC
--   Join as "attrId=valueId" pairs separated by "|"
--   Example: color(2)=red(5), size(7)=M(12) → "2=5|7=12" → SHA-256

-- ATTRIBUTE VALUES (normalized, source of truth)
ProductAttributeValues
  Id               int PK
  ProductId        nvarchar FK → Products
  VariantId        nvarchar? FK → ProductVariants  -- null = SPU-level attribute
  AttributeId      int FK → Attributes
  AttributeValueId int? FK → AttributeValues       -- when Type=select
  RawValue         nvarchar?                        -- when Type=text|number|boolean

ProductAttributeSnapshot      -- denormalized, rebuilt on every attribute write
  ProductId   nvarchar FK → Products
  VariantId   nvarchar? FK → ProductVariants
  Snapshot    nvarchar(max)  -- JSON: {"color":{"id":5,"label":"Red"},"size":{"id":8,"label":"M"}}
  Version     int            -- increment each rebuild; used for ETag + stale detection
  UpdatedAt   datetime2

-- PRICING (extensible)
VariantPrices
  Id           int PK
  VariantId    nvarchar FK → ProductVariants
  PriceType    nvarchar     -- base | sale | member | wholesale
  Amount       bigint       -- smallest currency unit (VND=đồng, USD=cents)
  CurrencyCode char(3)      -- ISO 4217
  StartAt      datetime2?
  EndAt        datetime2?
  -- Overlap rule: "latest StartAt wins" when same PriceType+Currency overlap
  -- Active price query:
  --   WHERE PriceType='sale' AND (StartAt IS NULL OR StartAt<=NOW())
  --             AND (EndAt IS NULL OR EndAt>=NOW())
  --   ORDER BY StartAt DESC LIMIT 1

-- IMAGES
ProductImages
  Id        int PK
  ProductId nvarchar FK → Products
  VariantId nvarchar? FK → ProductVariants   -- null = product-level image
  Url       nvarchar        -- CDN URL
  CdnKey    nvarchar        -- S3/Blob object key
  Checksum  char(64)        -- SHA-256 of file content (dedup check before upload)
  Type      nvarchar        -- main | gallery | swatch
  AltText   nvarchar(max)   -- JSON multilang
  SortOrder int
  IsPrimary bit

-- OUTBOX (Catalog Service)
OutboxEvents
  Id          bigint PK identity
  EventType   nvarchar     -- ProductCreated | ProductUpdated | ProductArchived
  Payload     nvarchar(max) -- JSON (includes snapshotVersion)
  CreatedAt   datetime2
  ProcessedAt datetime2?
  RetryCount  int
  Error       nvarchar?

DeadLetterEvents
  Id, OriginalEventId, EventType, Payload
  FailureReason, FailedAt, ReprocessedAt?

-- AUDIT & APPROVAL
AuditLogs
  Id         bigint PK identity
  TenantId   nvarchar
  StoreId    nvarchar
  UserId     nvarchar
  UserRole   nvarchar
  Action     nvarchar  -- create|update|publish|archive|price_change|stock_adjust
  EntityType nvarchar  -- product|variant|price|image
  EntityId   nvarchar
  Diff       nvarchar(max)?
  IpAddress  nvarchar
  CreatedAt  datetime2

  INDEX IX_AuditLogs_Entity (EntityType, EntityId)
  INDEX IX_AuditLogs_User (UserId, CreatedAt)

ApprovalComments
  Id        int PK
  ProductId nvarchar FK → Products
  UserId    nvarchar
  Role      nvarchar
  Message   nvarchar(max)
  Type      nvarchar  -- comment | approve | reject | request_change
  CreatedAt datetime2

NotificationEvents
  Id        bigint PK identity
  TenantId  nvarchar
  UserId    nvarchar
  Type      nvarchar  -- product_submitted|product_approved|product_rejected|stock_low
  EntityId  nvarchar
  Message   nvarchar
  ReadAt    datetime2?
  CreatedAt datetime2
```

### Inventory Service DB  ← separate database, separate service

```sql
-- WAREHOUSES
Warehouses
  Id        nvarchar PK
  TenantId  nvarchar
  StoreId   nvarchar
  Name      nvarchar
  Address   nvarchar?
  IsActive  bit

-- VARIANT STOCK
VariantStock
  Id                int PK
  VariantId         nvarchar      -- reference only, no cross-service FK
  WarehouseId       nvarchar FK → Warehouses
  Quantity          int           -- total physical stock
  ReservedQuantity  int           -- held by pending orders
  RowVersion        rowversion    -- optimistic concurrency

  CONSTRAINT UQ_VariantStock UNIQUE (VariantId, WarehouseId)
  INDEX IX_VariantStock (VariantId, WarehouseId)
  -- AvailableQuantity = Quantity - ReservedQuantity (computed)
  -- Invariant: ReservedQuantity <= Quantity (enforced in domain)

-- STOCK MOVEMENTS (immutable audit log)
StockMovements
  Id          bigint PK identity
  VariantId   nvarchar
  WarehouseId nvarchar FK → Warehouses
  Delta       int       -- positive=in, negative=out
  Type        nvarchar  -- import | order | cancel | adjustment | return
  ReferenceId nvarchar? -- orderId, adjustmentId, etc.
  CreatedAt   datetime2
  CreatedBy   nvarchar

  INDEX IX_StockMovements_Variant (VariantId, WarehouseId, CreatedAt)

-- OUTBOX (Inventory Service — own copy)
OutboxEvents
  Id          bigint PK identity
  EventType   nvarchar     -- StockUpdated | StockReserved | StockReleased
  Payload     nvarchar(max)
  CreatedAt   datetime2
  ProcessedAt datetime2?
  RetryCount  int
  Error       nvarchar?

DeadLetterEvents
  Id, OriginalEventId, EventType, Payload
  FailureReason, FailedAt, ReprocessedAt?
```

---

## Section 2: Domain Layer & Service Architecture

### Catalog Service — Domain (`Catalog.Core`)

```csharp
// Product — aggregate root (NO Variants list embedded)
Product
  ProductId, TenantId, StoreId, CategoryId
  Name (multilang), Slug, Description (multilang), Status
  List<ProductAttributeValue> Attributes   // SPU-level only

  // Domain events:
  ProductCreated, ProductUpdated, ProductPublished, ProductArchived

  void Publish()        // Status transition check
  void Archive()        // Status transition check
  void ChangeSlug(slug)

// ProductVariant — loaded separately
ProductVariant
  VariantId, ProductId, SKU, CombinationHash, Status, SortOrder
  List<ProductAttributeValue> Attributes
  List<VariantPrice> Prices   // value objects

// Value Objects
Money
  Amount (long), Currency (string)  // ISO 4217

VariantPrice
  Money Price, PriceType, StartAt?, EndAt?
  Money? GetActivePrice(string currency, PriceType type, DateTime now)
```

### Inventory Service — Domain (`Inventory.Core`)

```csharp
// VariantStock — aggregate (own lifecycle)
VariantStock
  VariantId, WarehouseId
  Quantity (int), ReservedQuantity (int), RowVersion
  AvailableQuantity => Quantity - ReservedQuantity

  void Reserve(int qty)   // throws OutOfStockException if qty > Available
  void Release(int qty)   // Math.Max(0, Reserved - qty)
  void Adjust(int delta)  // throws StockInvariantException if Quantity < Reserved after

// StockMovement — immutable Entity (NOT value object)
StockMovement
  Id, VariantId, WarehouseId
  Delta (int), Type, ReferenceId
  CreatedAt, CreatedBy
  // append-only: no Update/Delete
```

### Domain Service (Catalog)

```csharp
ProductVariantGeneratorService
  List<VariantAttributeSet> GenerateCombinations(List<AttributeWithValues> axes)
  string ComputeCombinationHash(VariantAttributeSet combination)
  // canonical: sort by AttributeId ASC → "attrId=valueId|..." → SHA-256
```

### Interfaces

```csharp
// Catalog.Core
IProductRepository
  Task<Product> GetByIdAsync(ProductId id, TenantId tenantId)
  Task<Product> GetBySlugAsync(string slug, StoreId storeId)
  Task<List<ProductVariant>> GetVariantsByProductIdAsync(ProductId id)
  Task SaveAsync(Product product)
  Task SaveVariantAsync(ProductVariant variant)
  Task SaveVariantsBulkAsync(List<ProductVariant> variants)

IOutboxRepository (Catalog)
  Task AppendAsync(OutboxEvent evt)  // atomic with saves via UoW

IUnitOfWork (Catalog)
  Task<ITransaction> BeginTransactionAsync()

// Inventory.Core
IStockRepository
  Task<VariantStock> GetAsync(VariantId id, WarehouseId warehouseId)
  Task UpdateStockAsync(VariantStock stock)        // RowVersion check
  Task AppendMovementAsync(StockMovement movement) // append-only

IOutboxRepository (Inventory)
  Task AppendAsync(OutboxEvent evt)

IUnitOfWork (Inventory)
  Task<ITransaction> BeginTransactionAsync()
```

### Application Services

```csharp
// Catalog Service
ProductService
  CreateProduct(CreateProductCommand)
    → validate slug uniqueness
    → Product.Create() → raises ProductCreated
    → UoW: SaveAsync + OutboxRepo.AppendAsync(ProductCreated.v1)

  UpdateProduct(UpdateProductCommand)
    → load → mutate → raises ProductUpdated
    → rebuild ProductAttributeSnapshot
    → UoW: Save + Outbox(ProductUpdated.v1)

  PublishProduct(ProductId)
    → status: draft|pending_approval → active
    → raises ProductPublished → UoW

  GenerateVariants(ProductId, axes[])
    → GenerateCombinations() + ComputeCombinationHash()
    → filter existing hashes
    → SaveVariantsBulkAsync
    → Outbox(ProductUpdated.v1)

// Inventory Service
StockService
  AdjustStock(AdjustStockCommand)
    → load VariantStock (with RowVersion)
    → stock.Adjust(delta)
    → append StockMovement (immutable)
    → UpdateStockAsync with RowVersion check
    → retry max 3x on conflict → throw StockConcurrencyException
    → UoW: Update + Outbox(StockUpdated.v1)

  ReserveStock(ReserveStockCommand)
    → stock.Reserve(qty)  // throws OutOfStockException
    → same optimistic concurrency + retry
    → UoW: Update + Outbox(StockReserved.v1)

  ReleaseStock(ReleaseStockCommand)
    → stock.Release(qty)
    → UoW: Update + Outbox(StockReleased.v1)
```

### Event / Outbox Pipeline (both services, same pattern)

```
Command received
  └─► DB Transaction (UoW):
        ├─ Save aggregate
        └─ INSERT OutboxEvents

OutboxProcessor (IHostedService, every 1s) — per service
  └─► SELECT TOP 100 FROM OutboxEvents WITH (UPDLOCK, READPAST)
        WHERE ProcessedAt IS NULL ORDER BY CreatedAt
  └─► foreach event:
        → wrap in MessageEnvelope (MessageEnvelopeMiddleware)
        → MassTransit.Publish() → RabbitMQ   ← CHANGED: was MediatR in-process
  └─► success: ProcessedAt = now
      failure: RetryCount++  → after 5: move to DeadLetterEvents

// Catalog Service consumers (on RabbitMQ):
IndexingWorker.Handle(ProductUpdated.v1 | ProductCreated.v1 | ProductPublished.v1)
  → Load Product + Variants + AttributeSnapshots from Catalog DB
  → Fetch VariantStock from Inventory Service API (or cache)
  → Build full ProductDocument
  → ES upsert by ProductId (full overwrite — idempotent)

StockSyncWorker.Handle(StockUpdated.v1)   ← Inventory Service publishes, Catalog consumes
  → buffer by ProductId (debounce 500ms)
  → compute AvailableQty per VariantId (all warehouses)
  → full ProductDocument re-fetch + ES upsert
  // Full re-index (NOT partial nested update — avoids ES nested mismatch)

IndexingWorker.Handle(ProductArchived.v1)
  → ES delete by ProductId
  → Redis DELETE product:{storeId}:{slug}
```

---

## Section 3: Elasticsearch Document Design & Query Layer

*(Unchanged from original spec — ES design is service-agnostic)*

### Index naming

```
dcms-{tenantId}-products-v{N}  ← versioned index
dcms-{tenantId}-products       ← alias (always points to active version)

Reindex (zero downtime):
  1. Create v{N+1} with new mapping
  2. Reindex v{N} → v{N+1}
  3. Atomic alias switch
  4. Delete v{N} after verify
```

### Document structure

```json
{
  "id": "prod_abc123",
  "tenantId": "lotte",
  "storeId": "store_001",
  "brandId": "brand_01",
  "categoryId": 10,
  "categoryPath": "/1/5/10/",
  "categoryAncestors": [1, 5, 10],
  "name": { "vi": "Áo thun Nike basic", "en": "Nike Basic T-Shirt" },
  "slug": "ao-thun-nike-basic",
  "status": "active",
  "storeCurrency": "VND",
  "salesCount30d": 142,
  "attributes": { "material": "cotton", "origin": "Vietnam" },
  "variants": [
    {
      "variantId": "var_001",
      "sku": "NIKE-M-RED",
      "status": "active",
      "inStock": true,
      "availableQty": 42,
      "basePrice": { "amount": 250000, "currency": "VND" },
      "activeSalePrice": { "amount": 199000, "currency": "VND" },
      "attributes": {
        "color": { "id": 5, "label": { "vi": "Đỏ", "en": "Red" } },
        "size":  { "id": 8, "label": { "vi": "M",  "en": "M"  } }
      }
    }
  ],
  "hasInStockVariant": true,
  "minBasePrice": { "amount": 199000, "currency": "VND" },
  "snapshotVersion": 3,
  "updatedAt": "2026-04-06T10:00:00Z"
}
```

**Key field notes:**
- `categoryAncestors` — integer array, term query (faster than prefix on categoryPath)
- `hasInStockVariant` — top-level bool, fast filter without nested query
- `inStock` / `availableQty` on variants — sourced from Inventory Service at index time
- `salesCount30d` — analytics job writes to Catalog DB (Products.SalesCount30d), IndexingWorker reads it
- `activeSalePrice` — computed at index time from VariantPrices time range
- `storeCurrency` — 1 store = 1 currency; multi-currency at render time only
- `snapshotVersion` — stale document detection

### Index mapping (key fields)

```json
{
  "settings": {
    "analysis": {
      "analyzer": {
        "vietnamese": { "type": "custom", "tokenizer": "standard", "filter": ["lowercase", "vi_stop"] },
        "ngram_analyzer": { "type": "custom", "tokenizer": "ngram_tokenizer", "filter": ["lowercase"] }
      },
      "tokenizer": {
        "ngram_tokenizer": { "type": "ngram", "min_gram": 2, "max_gram": 3 }
      }
    }
  },
  "mappings": {
    "properties": {
      "tenantId":          { "type": "keyword" },
      "storeId":           { "type": "keyword" },
      "brandId":           { "type": "keyword" },
      "categoryId":        { "type": "keyword" },
      "categoryAncestors": { "type": "integer" },
      "status":            { "type": "keyword" },
      "hasInStockVariant": { "type": "boolean" },
      "storeCurrency":     { "type": "keyword" },
      "name": {
        "properties": {
          "vi": { "type": "text", "analyzer": "vietnamese",
                  "fields": { "ngram": { "type": "text", "analyzer": "ngram_analyzer" } } },
          "en": { "type": "text", "analyzer": "standard" }
        }
      },
      "attributes":  { "type": "flattened" },
      "minBasePrice": {
        "properties": { "amount": { "type": "long" }, "currency": { "type": "keyword" } }
      },
      "variants": {
        "type": "nested",
        "properties": {
          "variantId":    { "type": "keyword" },
          "sku":          { "type": "keyword" },
          "status":       { "type": "keyword" },
          "inStock":      { "type": "boolean" },
          "availableQty": { "type": "integer" },
          "basePrice": {
            "properties": { "amount": { "type": "long" }, "currency": { "type": "keyword" } }
          },
          "attributes": { "type": "flattened" }
        }
      }
    }
  }
}
```

### Query patterns

**Search + category subtree + inStock:**
```json
{
  "query": {
    "bool": {
      "filter": [
        { "term": { "storeId": "store_001" } },
        { "term": { "status": "active" } },
        { "term": { "categoryAncestors": 5 } },
        { "term": { "hasInStockVariant": true } }
      ],
      "must": [{
        "multi_match": {
          "query": "áo thun",
          "fields": ["name.vi^3", "name.en^1", "name.vi.ngram"],
          "fuzziness": "AUTO",
          "type": "best_fields"
        }
      }]
    }
  },
  "sort": [
    { "minBasePrice.amount": { "order": "asc", "missing": "_last" } },
    { "id": "asc" }
  ],
  "size": 20,
  "search_after": ["250000", "prod_abc123"]
}
```

**Faceted aggregation:**
```json
{
  "aggs": {
    "by_color": {
      "nested": { "path": "variants" },
      "aggs": { "color_values": { "terms": { "field": "variants.attributes.color", "size": 20 } } }
    },
    "price_range": {
      "range": { "field": "minBasePrice.amount",
        "ranges": [{ "to": 100000 }, { "from": 100000, "to": 500000 }, { "from": 500000 }] }
    }
  }
}
```

### Pagination — search_after

```csharp
record ProductSearchQuery(
    StoreId StoreId, string? Keyword, int[]? CategoryAncestors,
    Dictionary<string, object>? Filters, SortField SortBy,
    string? CurrencyCode, int PageSize, object[]? SearchAfterCursor
);

record SearchResult(List<ProductSearchItem> Items, long TotalCount, object[]? NextCursor);
```

### Query cache key normalization

```csharp
string NormalizeSearchKey(ProductSearchQuery q)
{
    var parts = new SortedDictionary<string, string>
    {
        ["store"]   = q.StoreId,
        ["q"]       = q.Keyword?.ToLowerInvariant().Trim() ?? "",
        ["cat"]     = q.CategoryAncestors != null
                      ? string.Join(",", q.CategoryAncestors.OrderBy(x => x)) : "",
        ["instock"] = q.InStock?.ToString() ?? "",
        ["min"]     = q.MinPrice?.ToString() ?? "",
        ["max"]     = q.MaxPrice?.ToString() ?? "",
        ["sort"]    = q.SortBy.ToString(),
        ["size"]    = q.PageSize.ToString(),
        ["cursor"]  = q.SearchAfterCursor ?? ""
    };
    foreach (var f in q.Filters?.OrderBy(x => x.Key) ?? [])
        parts[$"f.{f.Key}"] = f.Value.ToString();

    var canonical = string.Join("&", parts.Select(p => $"{p.Key}={p.Value}"));
    return $"search:{q.StoreId}:{SHA256(canonical)}";
}
// Redis TTL: 30s
```

---

## Section 4: API Layer

### Catalog Service endpoints (:5001)

```
Products
  GET    /api/v1/products                           search (ES)
  GET    /api/v1/products/{slug}                    detail (SQL + Redis cache)
  POST   /api/v1/products                           create
  PUT    /api/v1/products/{id}                      update
  POST   /api/v1/products/{id}/publish              status → active
  POST   /api/v1/products/{id}/archive              status → archived
  DELETE /api/v1/products/{id}                      soft delete (archived)
  POST   /api/v1/products/bulk                      bulk create (max 100)
  PUT    /api/v1/products/bulk                      bulk update (max 100)
  GET    /api/v1/products/{slug}/slug-check         availability check

Variants
  GET    /api/v1/products/{id}/variants
  POST   /api/v1/products/{id}/variants/generate    cartesian product
  PUT    /api/v1/products/{id}/variants/{variantId}

Attributes
  GET    /api/v1/attributes                         list (tenant scope)
  GET    /api/v1/categories/{id}/attributes
```

### Inventory Service endpoints (:5002)

```
Stock (public/backoffice)
  GET    /api/v1/variants/{variantId}/stock         available qty per warehouse
  POST   /api/v1/variants/{variantId}/stock/adjust  manual adjustment
  POST   /api/v1/stock/bulk-adjust                  bulk import (max 500 items)

Warehouses
  GET    /api/v1/warehouses                         list (store scope)
  POST   /api/v1/warehouses                         create

Internal (service-to-service only — API key required)
  POST   /internal/inventory/check                  sync read-only stock check (Order Service pre-check)
  POST   /internal/inventory/reserve               saga ReserveStock command handler
  POST   /internal/inventory/release               saga ReleaseStock compensation handler
```

**Note:** `/internal/inventory/check` = read-only (no lock). Actual stock reservation = `ReserveStock.v1` saga command via RabbitMQ. The `/internal/inventory/reserve` endpoint is the MassTransit consumer endpoint, called by the saga orchestrator — not a raw HTTP endpoint exposed to clients.

### RBAC per endpoint

```
Catalog Service
  GET /products (search)                Public | StoreStaff+
  GET /products/{slug}                  Public | StoreStaff+
  POST /products                        StoreManager (own store)
  PUT /products/{id}                    StoreManager (own store)
  POST /products/{id}/publish           StoreManager (no approval) | BrandManager | ChainAdmin
  DELETE /products/{id}                 StoreManager (own store)
  POST|PUT /products/bulk               StoreManager
  POST /variants/generate               StoreManager

Inventory Service
  GET  /stock                           StoreStaff+
  POST /stock/adjust                    StoreManager | StoreStaff
  POST /stock/bulk-adjust               StoreManager
  POST /warehouses                      ChainAdmin | BrandManager
  POST /internal/*                      Internal API key only
```

### Caching strategy

```
Product detail (GET /products/{slug}):
  Cache key: dcms:product:{storeId}:{slug}   TTL 10m
  Populate:  cache miss → SQL → cache
  Invalidate: on ProductUpdated.v1 / ProductArchived.v1 consumed from RabbitMQ
              → Redis DEL dcms:product:{storeId}:{slug} + dcms:product:{id}

  ETag: "product-{id}-v{snapshotVersion}"
  Cache-Control: public, max-age=60
  If-None-Match → 304 Not Modified

Search results:
  Cache key: NormalizeSearchKey(query) → SHA-256 hash
  TTL: 30s
  Invalidate on ProductUpdated: SCAN + DEL dcms:search:{storeId}:*

Stock endpoints:
  Cache-Control: no-store
```

### Idempotency

```
Header: Idempotency-Key: <uuid>
Applied to: POST /products, POST /variants/generate, POST /stock/adjust, POST /products/bulk
Redis: dcms:idempotency:{key}  TTL 24h
```

### Request / Response shapes

**POST /api/v1/products**
```json
// Request
{
  "categoryId": 10,
  "name": { "vi": "Áo thun Nike basic", "en": "Nike Basic T-Shirt" },
  "description": { "vi": "...", "en": "..." },
  "slug": "ao-thun-nike-basic",
  "attributes": [{ "attributeId": 3, "rawValue": "cotton" }]
}
// Response 201
{ "data": { "id": "prod_abc123", "slug": "ao-thun-nike-basic", "status": "draft" }, "meta": null, "error": null }
```

**GET /api/v1/products (search)**
```json
{
  "data": [{ "id": "prod_abc123", "name": "Áo thun Nike basic",
             "minBasePrice": { "amount": 250000, "currency": "VND" },
             "hasInStockVariant": true }],
  "meta": { "totalCount": 142, "pageSize": 20, "nextCursor": "WyIy..." },
  "error": null
}
```

**GET /api/v1/products/{slug} — with variantMatrix**
```json
{
  "data": {
    "id": "prod_abc123",
    "name": { "vi": "Áo thun Nike basic" },
    "defaultVariant": { "variantId": "var_001", "basePrice": { "amount": 250000, "currency": "VND" }, "inStock": true },
    "variantMatrix": {
      "axes": [
        { "attributeId": 1, "name": "Màu", "values": [{ "id": 5, "label": "Đỏ" }, { "id": 6, "label": "Xanh" }] },
        { "attributeId": 2, "name": "Size", "values": [{ "id": 8, "label": "M" }, { "id": 9, "label": "L" }] }
      ],
      "combinations": {
        "1=5|2=8": { "variantId": "var_001", "inStock": true },
        "1=5|2=9": { "variantId": "var_002", "inStock": false }
      }
    }
  }
}
```

**Error shapes**
```json
// 422 Business rule
{ "data": null, "meta": null, "error": { "code": "OUT_OF_STOCK",
  "message": "Insufficient stock for variant var_001",
  "details": [{ "variantId": "var_001", "requested": 5, "available": 2 }] }}
```

### Rate limiting

```
storefront policy:  200 req/min per tenantId
backoffice policy:  500 req/min per API key | userId
internal policy:    no limit (API key required)

Tenant plan tiers:
  Starter:    storefront 100/min, backoffice 200/min
  Pro:        storefront 300/min, backoffice 500/min
  Enterprise: storefront 1000/min, backoffice 2000/min

TenantPlanConfig cached: dcms:tenant:plan:{tenantId}  TTL 5m
```

### API versioning

```
Current: /api/v1/
Breaking changes → /api/v2/
Non-breaking → stays v1
v1 + v2 run in parallel during 6-month transition
```

---

## Section 5: Backoffice UI Flow (Custom Umbraco Section)

Umbraco backoffice hosts a custom "Products" Section. It calls **Catalog Service API** and **Inventory Service API** directly — Umbraco is the shell only, not the data layer.

```
Umbraco Backoffice
  └── Custom Section: "Products"
        ├── Product CRUD → Catalog Service API (:5001)
        ├── Stock management → Inventory Service API (:5002)
        ├── Authenticated via JWT (same RBAC token)
        └── Scoped to storeId from JWT claims
```

### Product creation — 5-step wizard

```
Step 1: Select Category
  → GET /api/v1/categories (Catalog)

Step 2: Fill SPU details
  → Name (VI required), Description, Slug
  → Slug: debounce 400ms → GET /slug-check (Catalog, client-side cache 60s)
  → SPU-level attributes (IsVariant=false)

Step 3: Define variant axes
  → IsVariant=true attributes from CategoryAttributes (Catalog)
  → StoreAttributeOverrides applied
  → "Preview" button: client-side cartesian product (no API call)
      "Sẽ tạo 12 variants (4×3)" — warning if > 200
  → Confirm → POST /variants/generate (Catalog)

Step 4: Configure SKUs (virtualized grid)
  → ≤ 100 rows: standard table | > 100 rows: react-window virtualized
  → Inline edit: price, status per row
  → Bulk edit: select rows → same price → Undo toast (10s window)

Step 5: Review & Publish
  → "Save as draft" | "Submit for approval" | "Publish directly"
```

### Tabs after creation

```
Info tab:     edit SPU details, slug, category — multi-language tabs [VI ⚠] [EN]
Variants tab: virtualized table, add/deactivate/bulk price
Images tab:   drag-drop upload → S3/Blob direct; Checksum dedup check;
              drag-to-reorder (dnd-kit); IsPrimary toggle; Type badge
Stock tab:    calls Inventory Service API
  → Warehouse dropdown
  → Table: SKU | HN | HCM | Total Available | Reserved
  → Adjust modal: Delta, Type, Note, Preview → confirm
  → Bulk import: CSV → preview → POST /stock/bulk-adjust (Inventory)
  → 409 Concurrency: toast → auto-refresh
```

### Approval flow

```
StoreManager submits → pending_approval
  → notification to BrandManager + ChainAdmin

Approval view: comment thread (ApprovalComments, Catalog DB)
  → Approve | Request Changes | Reject (comment required)

Approval not required (config per store):
  → StoreManager publishes directly: draft → active
```

### Notification system (v1)

```
Polling: GET /api/v1/notifications/unread-count every 30s (count only)
  on count change → GET /api/v1/notifications?limit=20

Triggers: product_submitted | product_approved | product_rejected | stock_low
v2 upgrade: SignalR push (same contract, swap transport)
```

---

## Section 6: Cross-Cutting Concerns

### 6.1 Message Contracts (RabbitMQ via MassTransit)

All messages use the standard envelope from `dCMS.Messaging.Contracts`:

```
Catalog Service publishes:
  ProductCreated.v1   { ProductId, TenantId, StoreId, Slug, CategoryId, CreatedAt }
  ProductUpdated.v1   { ProductId, TenantId, StoreId, SnapshotVersion, UpdatedAt }
  ProductPublished.v1 { ProductId, TenantId, StoreId }
  ProductArchived.v1  { ProductId, TenantId, StoreId }

Inventory Service publishes:
  StockUpdated.v1     { VariantId, TenantId, StoreId, WarehouseId,
                        AvailableQty, ReservedQty, UpdatedAt }
  StockReserved.v1    { OrderId, ReservedItems: [{VariantId, WarehouseId, Qty}] }
  StockReleased.v1    { OrderId }
  StockReservationFailed.v1 { OrderId, Reason, FailedItems: [{VariantId, Requested, Available}] }

Consumers:
  ES IndexingWorker    ← ProductCreated.v1, ProductUpdated.v1, ProductPublished.v1
  ES StockSyncWorker   ← StockUpdated.v1
  ES ArchiveWorker     ← ProductArchived.v1
  Analytics Worker     ← ProductCreated.v1, StockUpdated.v1, StockReserved.v1
```

### 6.2 Redis Architecture

```
Key schema (prefix convention — NOT multiple Redis DBs):
  dcms:product:{storeId}:{slug}     TTL 10m    product detail (storefront)
  dcms:product:{id}                 TTL 10m    product detail (backoffice)
  dcms:search:{hash}                TTL 30s    search result page
  dcms:idempotency:{key}            TTL 24h    idempotent POST responses
  dcms:hotproduct:{storeId}:{id}    TTL 30m    hot product cache
  dcms:notification:unread:{userId} TTL 5m     unread count
  dcms:ratelimit:{tenantId}:*                  rate limit counters
  dcms:tenant:plan:{tenantId}       TTL 5m     tenant plan config
  dcms:domain:{domain}              TTL 5m     domain routing (Platform)

Eviction: allkeys-lru
Never write without TTL
Invalidation on ProductUpdated.v1:
  → DEL dcms:product:{storeId}:{slug} + dcms:product:{id}
  → SCAN + DEL dcms:search:{storeId}:*
```

### 6.3 Hot Product Strategy

```
HotProductsJob (IHostedService, every 15m):
  → top 100 products per store by (views + orders) in last 24h
  → pre-populate Redis: full product + variantMatrix
  → key: dcms:hotproduct:{storeId}:{productId}  TTL 30m
Invalidation: same as standard product cache (ProductUpdated.v1)
```

### 6.4 Search Relevance

```json
{
  "query": {
    "function_score": {
      "query": { "bool": { "...filters..." } },
      "functions": [
        { "filter": { "term": { "hasInStockVariant": true } }, "weight": 1.5 },
        { "field_value_factor": { "field": "salesCount30d", "factor": 0.1,
                                  "modifier": "log1p", "missing": 0 } }
      ],
      "score_mode": "sum", "boost_mode": "multiply"
    }
  }
}
```

Vietnamese synonyms: loaded from `config/synonyms-vi.txt`, applied in search analyzer only (no reindex needed).

### 6.5 Monitoring & Observability

```
Distributed tracing: OpenTelemetry → Jaeger / Azure Monitor
  correlationId from message envelope propagated across all spans

Structured logging: Serilog → Seq / ELK
  - API request: tenantId, storeId, userId, action, latency
  - ES query: latency, hits, index name
  - Outbox event: eventType, processingTime, retryCount
  - Stock mutation: variantId, delta, type, newQty

Metrics (Prometheus):
  es_query_latency_ms (p50, p95, p99)
  outbox_pending_count (alert if > 500 for > 2min)
  stock_movement_rate
  cache_hit_rate (product detail, search)
  dlq_depth{queue} (alert if > 0)

Alerts:
  outbox_pending > 500 sustained 2m → PagerDuty
  stock_mismatch (reconciliation) → Slack
  price_change > 30% → Slack + AuditLog
  ES cluster yellow/red → PagerDuty
```

### 6.6 DLQ & Retry

```
OutboxProcessor retry:
  Attempt 1-3: immediate
  Attempt 4-5: backoff 30s
  Attempt 6+:  → DeadLetterEvents

DLQ management via Admin UI (Umbraco "System Health" tab):
  Reprocess | Reprocess All | Discard
  API: POST /api/v1/admin/dlq/{id}/reprocess (SuperAdmin only)

RabbitMQ DLQ:
  After 3 retries (1s → 5s → 30s backoff)
  → dlq.{exchange}.{routingKey}
  Message retains full envelope + x-death-count, x-first-death, x-death-reason
```

### 6.7 Stock Reconciliation

```
Daily job:
  → compare VariantStock.Quantity with SUM(StockMovements.Delta)
  → log discrepancies to AuditLog (Type=reconciliation_mismatch)
  → Slack alert if discrepancy > 0

Auto-fix: Admin-initiated only (NOT automatic)
  → "Fix Discrepancy" button per variant
  → write corrective StockMovement (Type=reconciliation_fix)
  → update VariantStock.Quantity
  → AuditLog entry (userId=admin)
```

### 6.8 Migration Strategy

```
DB (DbUp — forward-only):
  Phase 1: ADD nullable column → deploy
  Phase 2: backfill → deploy
  Phase 3: add constraint / DROP old column → deploy
  Backward compatibility window: 2 deploy cycles

ES (zero downtime):
  1. Create v{N+1} 2. Reindex 3. Atomic alias switch 4. Delete v{N} after 24h
  Breaking mapping changes → full reindex during low-traffic window
```

### 6.9 Security

```
Internal endpoints (Inventory /internal/*):
  mTLS or shared API key + IP whitelist

Price change threshold alert:
  → if basePrice diff > 30%: AuditLog + Slack alert
  → threshold configurable per store (default 30%)

Slug check: no auth, rate-limited per IP (20 req/min — enumeration prevention)
All write endpoints: AuditLog entry (userId, action, diff)
```

---

## Out of Scope (v1)

- Promotions / discount engine
- Multi-currency conversion service
- SignalR real-time notifications
- Async bulk import > 100 products
- Auto-translate for multi-language fields
- Product reviews / ratings
- Related products / recommendations
- Returns / refunds (Order Service — future iteration)
