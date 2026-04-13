namespace dCMS.Order.Core.Ordering;

/// <summary>Read-model list for store orders (US-21 / DAI-325).</summary>
public sealed record OrderListQuery(
    string TenantId,
    string StoreId,
    string? CustomerId,
    string? Status,
    string? Cursor,
    int Limit);

/// <summary>Order aggregate plus <c>CreatedAt</c> for cursor pagination.</summary>
public sealed record TimedOrder(global::dCMS.Order.Core.Domain.Order Order, DateTimeOffset CreatedAt);

public sealed record OrderListPage(IReadOnlyList<TimedOrder> Items, string? NextCursor);
