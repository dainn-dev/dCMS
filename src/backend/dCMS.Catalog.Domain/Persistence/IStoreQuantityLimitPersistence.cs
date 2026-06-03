namespace dCMS.Core.Persistence;

public sealed record StoreQuantityLimitGeneralRow(int CartLimitPerProduct, DateTimeOffset? UpdatedAt);

public sealed record StoreQuantityLimitRuleRow(
    string Id,
    string TenantId,
    string StoreId,
    string Name,
    string LimitType,
    bool PerProduct,
    int QuantityLimit,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? BrandId,
    int[] CategoryIds,
    string? ProductId,
    string? MembershipType,
    string? MembershipTier,
    string ModifiedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public interface IStoreQuantityLimitPersistence
{
    Task<StoreQuantityLimitGeneralRow> GetGeneralAsync(string tenantId, string storeId,
        CancellationToken cancellationToken = default);

    Task UpsertGeneralAsync(string tenantId, string storeId, int cartLimitPerProduct, DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoreQuantityLimitRuleRow>> ListRulesAsync(string tenantId, string storeId,
        CancellationToken cancellationToken = default);

    Task<StoreQuantityLimitRuleRow?> GetRuleAsync(string tenantId, string storeId, string ruleId,
        CancellationToken cancellationToken = default);

    Task InsertRuleAsync(StoreQuantityLimitRuleRow row, CancellationToken cancellationToken = default);

    Task<bool> UpdateRuleAsync(StoreQuantityLimitRuleRow row, CancellationToken cancellationToken = default);

    Task<bool> DeleteRuleAsync(string tenantId, string storeId, string ruleId,
        CancellationToken cancellationToken = default);

    Task InsertHistoryAsync(string tenantId, string storeId, string action, string snapshotJson,
        string userId, string userRole, DateTimeOffset createdAt, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuantityLimitHistoryRow>> ListHistoryAsync(string tenantId, string storeId, int limit,
        CancellationToken cancellationToken = default);
}

public sealed record QuantityLimitHistoryRow(
    long Id,
    string UserId,
    string UserRole,
    string Action,
    string SnapshotJson,
    DateTimeOffset CreatedAt);
