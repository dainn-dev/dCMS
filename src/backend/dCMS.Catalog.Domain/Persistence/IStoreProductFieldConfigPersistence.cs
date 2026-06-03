namespace dCMS.Core.Persistence;

/// <summary>Per-store Product Configuration custom field definitions (JSON document).</summary>
public interface IStoreProductFieldConfigPersistence
{
    Task<string?> GetFieldsJsonAsync(string tenantId, string storeId, CancellationToken cancellationToken = default);

    Task<(string? FieldsJson, DateTimeOffset? UpdatedAt)> GetFieldsWithUpdatedAtAsync(string tenantId, string storeId,
        CancellationToken cancellationToken = default);

    Task UpsertFieldsJsonAsync(string tenantId, string storeId, string fieldsJson, DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default);
}
