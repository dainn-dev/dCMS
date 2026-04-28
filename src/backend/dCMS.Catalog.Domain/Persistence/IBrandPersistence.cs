using dCMS.Core.Models;

namespace dCMS.Core.Persistence;

/// <summary>
/// Brand persistence — tenant-scoped master data.
/// All methods scope queries by <paramref name="tenantId"/> for multi-tenant isolation.
/// </summary>
public interface IBrandPersistence
{
    /// <summary>Paginated brand list for a tenant.</summary>
    Task<IReadOnlyList<Brand>> ListBrandsAsync(
        string tenantId,
        bool?  activeOnly,
        string? search,
        int    page,
        int    pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Total count (for pagination meta).</summary>
    Task<int> CountBrandsAsync(
        string tenantId,
        bool?  activeOnly,
        string? search,
        CancellationToken cancellationToken = default);

    Task<Brand?> GetBrandAsync(
        string tenantId,
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(
        string tenantId,
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>Upsert — INSERT … ON CONFLICT DO UPDATE.</summary>
    Task SaveBrandAsync(
        Brand brand,
        CancellationToken cancellationToken = default);

    /// <summary>Hard delete. Returns <c>false</c> if brand was not found.</summary>
    Task<bool> DeleteBrandAsync(
        string tenantId,
        string code,
        CancellationToken cancellationToken = default);
}
