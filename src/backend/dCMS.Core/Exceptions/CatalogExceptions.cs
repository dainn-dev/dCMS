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

public sealed class ProductNotFoundException : Exception
{
    public ProductNotFoundException() : base("Product not found for tenant.") { }
}
