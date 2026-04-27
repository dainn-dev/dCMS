using dCMS.Core.Services;

namespace dCMS.Core.Commands;

public sealed record GenerateVariantsCommand(
    string ProductId,
    string TenantId,
    string StoreId,
    IReadOnlyList<ProductVariantGeneratorService.VariantAxisDefinition> Axes,
    string SkuPrefix);
