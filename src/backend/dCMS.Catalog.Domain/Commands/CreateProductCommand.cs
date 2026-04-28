namespace dCMS.Core.Commands;

public sealed record CreateProductCommand(
    string TenantId,
    string StoreId,
    int CategoryId,
    string NameJson,
    string DescriptionJson,
    string Slug);
