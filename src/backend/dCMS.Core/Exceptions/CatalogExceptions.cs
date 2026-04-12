namespace dCMS.Core.Exceptions;

public sealed class InvalidProductStateException : Exception
{
    public InvalidProductStateException(string message) : base(message) { }
}

public sealed class DuplicateProductSlugException : Exception
{
    public DuplicateProductSlugException(string storeId, string slug)
        : base($"Slug '{slug}' already exists for store '{storeId}'.") { }
}

public sealed class DuplicateVariantSkuException : Exception
{
    public DuplicateVariantSkuException(string storeId, string sku)
        : base($"Variant SKU '{sku}' already exists for store '{storeId}'.") { }
}

/// <summary>Another variant on the same product already uses this combination hash.</summary>
public sealed class DuplicateVariantCombinationHashException : Exception
{
    public string ConflictingVariantId { get; }

    public DuplicateVariantCombinationHashException(string conflictingVariantId, string combinationHash)
        : base($"Combination hash '{combinationHash}' is already used by variant '{conflictingVariantId}' on this product.")
    {
        ConflictingVariantId = conflictingVariantId;
    }
}

public sealed class ProductNotFoundException : Exception
{
    public ProductNotFoundException() : base("Product not found for tenant.") { }
}
