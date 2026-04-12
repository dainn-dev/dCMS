using dCMS.Core.Models;

namespace dCMS.Core.Search;

/// <summary>Everything needed to build a tenant-scoped <see cref="ProductDocument"/> for Elasticsearch.</summary>
public sealed record ProductIndexPayload(
    Product Product,
    IReadOnlyList<ProductVariant> Variants,
    IReadOnlyList<int> CategoryAncestors,
    string CategoryPath,
    IReadOnlyDictionary<string, VariantStockSummary> StockByVariantId,
    IReadOnlyDictionary<string, string> AttributesFlattened,
    int SnapshotVersion,
    string StoreCurrency,
    string? BrandId);
