namespace dCMS.Promotions.Contracts.Evaluate;

public sealed record CartLine(
    string LineId,
    string ProductId,
    string? VariantId,
    string Sku,
    int Quantity,
    decimal UnitPrice,
    IReadOnlyList<string> CategoryIds,
    string? BrandId);
