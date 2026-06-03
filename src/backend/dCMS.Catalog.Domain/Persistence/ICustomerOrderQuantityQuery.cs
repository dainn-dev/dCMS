namespace dCMS.Core.Persistence;

/// <summary>Counts purchased quantity for per-user quantity limit enforcement (Order DB).</summary>
public interface ICustomerOrderQuantityQuery
{
    Task<int> GetPurchasedQuantityAsync(
        string tenantId,
        string storeId,
        string customerId,
        string productId,
        DateOnly fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default);
}
