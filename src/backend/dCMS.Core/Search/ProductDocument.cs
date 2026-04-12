using System.Text.Json.Serialization;

namespace dCMS.Core.Search;

/// <summary>Elasticsearch product document (spec: Section 3 — dcms-&#123;tenant&#125;-products).</summary>
public sealed class ProductDocument
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("tenantId")]
    public required string TenantId { get; init; }

    [JsonPropertyName("storeId")]
    public required string StoreId { get; init; }

    [JsonPropertyName("brandId")]
    public string? BrandId { get; init; }

    [JsonPropertyName("categoryId")]
    public required string CategoryId { get; init; }

    [JsonPropertyName("categoryPath")]
    public required string CategoryPath { get; init; }

    [JsonPropertyName("categoryAncestors")]
    public required IReadOnlyList<int> CategoryAncestors { get; init; }

    /// <summary>Localized display names (parsed from catalog <c>Name</c> JSON).</summary>
    [JsonPropertyName("name")]
    public required Dictionary<string, string> Name { get; init; }

    [JsonPropertyName("slug")]
    public required string Slug { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("storeCurrency")]
    public required string StoreCurrency { get; init; }

    [JsonPropertyName("salesCount30d")]
    public int SalesCount30d { get; init; }

    [JsonPropertyName("attributes")]
    public required Dictionary<string, string> Attributes { get; init; }

    [JsonPropertyName("variants")]
    public required IReadOnlyList<VariantDocument> Variants { get; init; }

    [JsonPropertyName("hasInStockVariant")]
    public bool HasInStockVariant { get; init; }

    [JsonPropertyName("minBasePrice")]
    public required MoneyAmount MinBasePrice { get; init; }

    [JsonPropertyName("snapshotVersion")]
    public int SnapshotVersion { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }

    [JsonPropertyName("activeSalePrice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MoneyAmount? ActiveSalePrice { get; init; }
}

public sealed class VariantDocument
{
    [JsonPropertyName("variantId")]
    public required string VariantId { get; init; }

    [JsonPropertyName("sku")]
    public required string Sku { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("inStock")]
    public bool InStock { get; init; }

    [JsonPropertyName("availableQty")]
    public int AvailableQty { get; init; }

    [JsonPropertyName("basePrice")]
    public required MoneyAmount BasePrice { get; init; }

    [JsonPropertyName("attributes")]
    public required Dictionary<string, object> Attributes { get; init; }
}
