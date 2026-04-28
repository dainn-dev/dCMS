using dCMS.Order.Core.Domain;

namespace dCMS.Order.Infrastructure.Persistence;

/// <summary>DAI-697 — read/write surface for Return aggregates.</summary>
public interface IReturnsRepository
{
    Task<Return?> GetByIdAsync(string tenantId, string storeId, Guid returnId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Return>> ListByOrderAsync(string tenantId, string storeId, Guid orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Return>> ListByStatusAsync(
        string tenantId,
        string storeId,
        ReturnStatus? status,
        int limit,
        CancellationToken cancellationToken = default);
}
