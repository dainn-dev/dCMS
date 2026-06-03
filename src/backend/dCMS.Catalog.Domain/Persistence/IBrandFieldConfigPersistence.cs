namespace dCMS.Core.Persistence;

/// <summary>
/// Persistence for the tenant-scoped Brand Configuration — the dynamic
/// "additional fields" definitions shown on the Add/Edit Brand form.
/// Stored as a single JSON array document per tenant (multi-tenant isolation via TenantId).
/// </summary>
public interface IBrandFieldConfigPersistence
{
    /// <summary>
    /// Returns the stored field-definitions JSON array for the tenant, or
    /// <c>null</c> when the tenant has never saved a configuration (so callers
    /// can fall back to seed defaults).
    /// </summary>
    Task<string?> GetFieldsJsonAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Upsert the whole field-definitions JSON array for the tenant.</summary>
    Task UpsertFieldsJsonAsync(
        string tenantId,
        string fieldsJson,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default);
}
