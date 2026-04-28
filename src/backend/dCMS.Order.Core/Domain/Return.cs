namespace dCMS.Order.Core.Domain;

public enum ReturnStatus
{
    Pending = 0,
    Approved,
    Rejected,
    Completed,
}

public enum ReturnReason
{
    WrongItem = 0,
    Defective,
    NotAsDescribed,
    ChangedMind,
    DamagedInTransit,
    Other,
}

/// <summary>DAI-697 — RMA aggregate. Approving a Pending return triggers
/// item ReturnedQuantity updates + ProductRestocked emission + RefundCase opening.</summary>
public sealed class Return
{
    private readonly List<ReturnItem> _items;

    public Return(
        string id,
        string orderId,
        string tenantId,
        string storeId,
        ReturnReason reason,
        string? notes,
        IReadOnlyList<ReturnItem> items,
        ReturnStatus status = ReturnStatus.Pending,
        string? refundCaseId = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? approvedAt = null,
        string? approvedBy = null,
        DateTimeOffset? completedAt = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Return id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(orderId))
            throw new ArgumentException("Order id is required.", nameof(orderId));
        if (items is null || items.Count == 0)
            throw new ArgumentException("At least one return item is required.", nameof(items));

        Id = id;
        OrderId = orderId;
        TenantId = tenantId;
        StoreId = storeId;
        Status = status;
        Reason = reason;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        _items = items.ToList();
        RefundCaseId = refundCaseId;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        ApprovedAt = approvedAt;
        ApprovedBy = approvedBy;
        CompletedAt = completedAt;
    }

    public string Id { get; }
    public string OrderId { get; }
    public string TenantId { get; }
    public string StoreId { get; }
    public ReturnStatus Status { get; private set; }
    public ReturnReason Reason { get; }
    public string? Notes { get; }
    public IReadOnlyList<ReturnItem> Items => _items;
    public string? RefundCaseId { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public void Approve(string approvedBy, DateTimeOffset occurredAt)
    {
        if (Status != ReturnStatus.Pending)
            throw new InvalidOperationException($"Cannot approve return in status {Status}.");
        Status = ReturnStatus.Approved;
        ApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? null : approvedBy.Trim();
        ApprovedAt = occurredAt;
    }

    public void Reject(string approvedBy, DateTimeOffset occurredAt)
    {
        if (Status != ReturnStatus.Pending)
            throw new InvalidOperationException($"Cannot reject return in status {Status}.");
        Status = ReturnStatus.Rejected;
        ApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? null : approvedBy.Trim();
        ApprovedAt = occurredAt;
    }

    public void Complete(string? refundCaseId, DateTimeOffset occurredAt)
    {
        if (Status != ReturnStatus.Approved)
            throw new InvalidOperationException($"Cannot complete return in status {Status}.");
        Status = ReturnStatus.Completed;
        RefundCaseId = string.IsNullOrWhiteSpace(refundCaseId) ? null : refundCaseId.Trim();
        CompletedAt = occurredAt;
    }
}

public sealed class ReturnItem
{
    public ReturnItem(string id, string orderItemId, int quantity, ReturnReason? reason = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("ReturnItem id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(orderItemId))
            throw new ArgumentException("OrderItem id is required.", nameof(orderItemId));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

        Id = id;
        OrderItemId = orderItemId;
        Quantity = quantity;
        Reason = reason;
    }

    public string Id { get; }
    public string OrderItemId { get; }
    public int Quantity { get; }
    public ReturnReason? Reason { get; }
}

/// <summary>DAI-697 — emitted when a Return.Approved item must restock inventory in Catalog.</summary>
public sealed record ProductRestocked(
    string OrderId,
    string TenantId,
    string StoreId,
    string VariantId,
    int Quantity,
    string ReturnId,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>DAI-697 — emitted when a Return state transitions; backoffice subscribers can refresh views.</summary>
public sealed record ReturnStatusChanged(
    string ReturnId,
    string OrderId,
    string FromStatus,
    string ToStatus,
    DateTimeOffset OccurredAt) : IDomainEvent;
