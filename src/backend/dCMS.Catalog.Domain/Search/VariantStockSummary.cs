namespace dCMS.Core.Search;

/// <summary>Aggregated sellable quantity for a variant across warehouses (Inventory DB).</summary>
public readonly record struct VariantStockSummary(int AvailableQty, bool InStock);
