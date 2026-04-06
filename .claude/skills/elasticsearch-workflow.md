# Elasticsearch Workflow Skill

Dùng khi làm việc với Elasticsearch — index setup, search queries, mapping updates.

## Connection

```csharp
// Infrastructure/Search/ElasticsearchClientFactory.cs
services.AddSingleton<ElasticsearchClient>(sp =>
{
    var settings = new ElasticsearchClientSettings(new Uri(configuration["Elasticsearch:Url"]))
        .DefaultIndex("products");
    return new ElasticsearchClient(settings);
});
```

## Index Naming Convention

```
dcms-{tenantId}-products        # Product catalog (shared cho tất cả Brands + Stores trong Siêu thị)
dcms-{tenantId}-categories      # Category tree
dcms-{tenantId}-pages           # CMS pages (content search)
```

**Tenant = Siêu thị** — index scoped theo Siêu thị. Brand/Store isolation được thực hiện bằng filter field trong query, không phải index riêng.

**Document shape phải include brandId và storeId:**
```json
{
  "id": "prod-123",
  "tenantId": "lotte-vn",
  "brandId": "lotte-mart",
  "storeId": "lotte-thu-duc",
  "name": "...",
  ...
}
```

**Lý do dùng 1 index per Tenant thay vì per Store:**
- Cross-brand search trong Siêu thị (ví dụ: search "áo" trên tất cả brands của Lotte) dễ dàng hơn
- Giảm số lượng indices cần quản lý
- Isolation giữa các Siêu thị vẫn đảm bảo qua index name prefix

## Product Index Mapping

```json
{
  "mappings": {
    "properties": {
      "id": { "type": "keyword" },
      "storeId": { "type": "keyword" },
      "name": {
        "type": "text",
        "analyzer": "standard",
        "fields": {
          "keyword": { "type": "keyword" },
          "suggest": { "type": "completion" }
        }
      },
      "description": { "type": "text" },
      "slug": { "type": "keyword" },
      "price": { "type": "long" },
      "currency": { "type": "keyword" },
      "categoryIds": { "type": "keyword" },
      "tags": { "type": "keyword" },
      "inStock": { "type": "boolean" },
      "createdAt": { "type": "date" }
    }
  }
}
```

## Search Query Pattern

```csharp
// Infrastructure/Search/ProductSearchRepository.cs
public async Task<SearchResult<ProductDocument>> SearchAsync(string storeId, ProductSearchQuery query)
{
    var response = await _client.SearchAsync<ProductDocument>(s => s
        .Index($"dcms-{tenantId}-products")
        .Query(q => q
            .Bool(b => b
                .Must(m => m
                    .MultiMatch(mm => mm
                        .Query(query.SearchTerm)
                        .Fields(f => f
                            .Field(p => p.Name, boost: 3)
                            .Field(p => p.Description)
                            .Field(p => p.Tags)
                        )
                    )
                )
                .Filter(f =>
                {
                    var filters = new List<Action<QueryDescriptor<ProductDocument>>>();
                    if (query.CategoryId.HasValue)
                        filters.Add(fq => fq.Term(t => t.Field(p => p.CategoryIds).Value(query.CategoryId.Value.ToString())));
                    if (query.InStockOnly)
                        filters.Add(fq => fq.Term(t => t.Field(p => p.InStock).Value(true)));
                    return f.Bool(fb => fb.Must(filters.ToArray()));
                })
            )
        )
        .Sort(so => so.Field(p => p.CreatedAt, SortOrder.Desc))
        .From(query.Page * query.PageSize)
        .Size(query.PageSize)
    );

    return new SearchResult<ProductDocument>
    {
        Items = response.Documents.ToList(),
        Total = response.HitsMetadata.Total.Value,
        Page = query.Page,
        PageSize = query.PageSize
    };
}
```

## Index Document khi Product thay đổi

```csharp
// Sau khi update product trong DB, sync lên ES
public async Task IndexProductAsync(string storeId, Product product)
{
    var document = _mapper.Map<ProductDocument>(product);
    await _client.IndexAsync(document,
        idx => idx.Index($"dcms-{storeId}-products").Id(product.Id.ToString()));
}

// Xóa khỏi index khi delete product
public async Task DeleteProductIndexAsync(string storeId, Guid productId)
{
    await _client.DeleteAsync<ProductDocument>(
        productId.ToString(),
        d => d.Index($"dcms-{storeId}-products"));
}
```

## Workflow khi thay đổi Mapping

1. **Không edit mapping đang có** — ES không cho phép thay đổi field type
2. Tạo index mới với mapping mới: `dcms-{storeId}-products-v2`
3. Reindex data từ index cũ sang mới
4. Update alias `dcms-{storeId}-products` → trỏ sang index mới
5. Xóa index cũ

```bash
# Reindex từ old sang new
POST /_reindex
{
  "source": { "index": "dcms-tenant1-products" },
  "dest": { "index": "dcms-tenant1-products-v2" }
}

# Update alias
POST /_aliases
{
  "actions": [
    { "remove": { "index": "dcms-tenant1-products", "alias": "products-alias" } },
    { "add": { "index": "dcms-tenant1-products-v2", "alias": "products-alias" } }
  ]
}
```

## Debug với Kibana

Local dev: Kibana chạy ở `http://localhost:5601`
- Dev Tools → Console: test queries trực tiếp
- Discover: xem documents trong index
- Index Management: quản lý indices và mappings
