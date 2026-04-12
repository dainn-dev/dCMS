namespace dCMS.Core.Commands;

public sealed record UpdateProductCommand(
    string ProductId,
    string TenantId,
    string StoreId,
    int CategoryId,
    string NameJson,
    string DescriptionJson,
    string Slug);
