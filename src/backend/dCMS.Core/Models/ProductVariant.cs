namespace dCMS.Core.Models;

/// <summary>SKU row (variant) — Inventory references VariantId only across services.</summary>
public sealed class ProductVariant
{
    private ProductVariant(string id, string productId, string sku, string combinationHash, string combinationCanonical,
        string status, int sortOrder)
    {
        Id = id;
        ProductId = productId;
        Sku = sku;
        CombinationHash = combinationHash;
        CombinationCanonical = combinationCanonical;
        Status = status;
        SortOrder = sortOrder;
    }

    public string Id { get; }
    public string ProductId { get; }
    public string Sku { get; }
    /// <summary>64-char lowercase hex SHA-256 of canonical <c>attrId=valueId|...</c> sorted by attrId.</summary>
    public string CombinationHash { get; }
    /// <summary>Canonical matrix key, e.g. <c>1=5|2=8</c>. Empty for legacy rows → storefront may fall back to <see cref="CombinationHash"/>.</summary>
    public string CombinationCanonical { get; }
    public string Status { get; }
    public int SortOrder { get; }

    public static ProductVariant Create(string productId, string sku, string combinationHash, int sortOrder,
        string combinationCanonical = "") =>
        new("var_" + Guid.NewGuid().ToString("N"), productId, sku, combinationHash, combinationCanonical, "active",
            sortOrder);

    public static ProductVariant Restore(string id, string productId, string sku, string combinationHash, string status,
        int sortOrder, string combinationCanonical = "") =>
        new(id, productId, sku, combinationHash, combinationCanonical, status, sortOrder);

    /// <summary>Immutable update for editable fields (SKU, status, sort order). Combination hash is unchanged.</summary>
    public ProductVariant With(string? sku = null, string? status = null, int? sortOrder = null) =>
        new(Id, ProductId, sku ?? Sku, CombinationHash, CombinationCanonical, status ?? Status, sortOrder ?? SortOrder);
}
