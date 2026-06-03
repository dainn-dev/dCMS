namespace dCMS.Core.Persistence;

/// <summary>Per-store Best Seller widget configuration (JSON document).</summary>
public interface IStoreBestSellerSettingsPersistence
{
    Task<string?> GetSettingsJsonAsync(string tenantId, string storeId, CancellationToken cancellationToken = default);

    Task<(string? SettingsJson, DateTimeOffset? UpdatedAt)> GetSettingsWithUpdatedAtAsync(string tenantId, string storeId,
        CancellationToken cancellationToken = default);

    Task UpsertSettingsJsonAsync(string tenantId, string storeId, string settingsJson, DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default);

    Task InsertHistoryAsync(string tenantId, string storeId, string settingsJson, string userId, string userRole,
        DateTimeOffset createdAt, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BestSellerSettingsHistoryRow>> ListHistoryAsync(string tenantId, string storeId, int limit,
        CancellationToken cancellationToken = default);
}

public sealed record BestSellerSettingsHistoryRow(
    long Id,
    string UserId,
    string UserRole,
    string SettingsJson,
    DateTimeOffset CreatedAt);
