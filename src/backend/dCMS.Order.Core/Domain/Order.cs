namespace dCMS.Order.Core.Domain;

/// <summary>Order aggregate root (DAI-312 / US-18).</summary>
public sealed class Order
{
    private readonly List<OrderItem> _items;
    private readonly List<IDomainEvent> _domainEvents = [];

    private Order(
        string id,
        string tenantId,
        string storeId,
        string customerId,
        OrderStatus status,
        Money total,
        ShippingAddress shippingAddress,
        IEnumerable<OrderItem> items,
        string? paymentIntentId,
        string? failureReason,
        string? failureErrorCode,
        DateTimeOffset? failedAt,
        int retryCount)
    {
        Id = id;
        TenantId = tenantId;
        StoreId = storeId;
        CustomerId = customerId;
        Status = status;
        Total = total;
        ShippingAddress = shippingAddress;
        _items = items.ToList();
        PaymentIntentId = paymentIntentId;
        FailureReason = failureReason;
        FailureErrorCode = failureErrorCode;
        FailedAt = failedAt;
        RetryCount = retryCount;
    }

    public string Id { get; }
    public string TenantId { get; }
    public string StoreId { get; }
    public string CustomerId { get; }
    public OrderStatus Status { get; private set; }
    public Money Total { get; }
    public ShippingAddress ShippingAddress { get; }
    public IReadOnlyList<OrderItem> Items => _items;

    /// <summary>Payment provider intent id for saga correlation (DAI-315). Set before first persistence.</summary>
    public string? PaymentIntentId { get; private set; }

    public string? FailureReason { get; private set; }
    public string? FailureErrorCode { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public int RetryCount { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>Creates a new order in <see cref="OrderStatus.PaymentPending"/> and records <see cref="OrderPlaced"/>.</summary>
    public static Order Create(
        string orderId,
        string tenantId,
        string storeId,
        string customerId,
        IReadOnlyList<OrderItem> items,
        IReadOnlyList<OrderPlacedLine> placementLines,
        ShippingAddress shippingAddress,
        DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new ArgumentException("Order id is required.", nameof(orderId));
        if (items is null || items.Count == 0)
            throw new ArgumentException("At least one line item is required.", nameof(items));
        if (placementLines is null || placementLines.Count == 0)
            throw new ArgumentException("At least one placement line is required for saga messaging.", nameof(placementLines));

        var total = Money.Sum(items.Select(i => i.LineTotal()));
        var order = new Order(
            orderId,
            tenantId,
            storeId,
            customerId,
            OrderStatus.PaymentPending,
            total,
            shippingAddress,
            items,
            paymentIntentId: null,
            failureReason: null,
            failureErrorCode: null,
            failedAt: null,
            retryCount: 0);

        order._domainEvents.Add(new OrderPlaced(
            order.Id,
            order.TenantId,
            order.StoreId,
            order.CustomerId,
            order.Total.Amount,
            order.Total.Currency,
            placementLines,
            occurredAt));

        return order;
    }

    /// <summary>Rehydrates an order from persistence (no domain events raised).</summary>
    public static Order FromPersistence(
        string id,
        string tenantId,
        string storeId,
        string customerId,
        OrderStatus status,
        Money total,
        ShippingAddress shippingAddress,
        IEnumerable<OrderItem> items,
        string? paymentIntentId = null,
        string? failureReason = null,
        string? failureErrorCode = null,
        DateTimeOffset? failedAt = null,
        int retryCount = 0) =>
        new(id, tenantId, storeId, customerId, status, total, shippingAddress, items, paymentIntentId, failureReason,
            failureErrorCode, failedAt, retryCount);

    /// <summary>Assigns the payment intent id after Payment Service returns success (DAI-315).</summary>
    public void AssignPaymentIntent(string paymentIntentId)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            throw new ArgumentException("Payment intent id is required.", nameof(paymentIntentId));
        if (Status != OrderStatus.PaymentPending)
            throw new InvalidOperationException($"Cannot assign payment intent when order status is {Status}.");
        if (PaymentIntentId is not null)
            throw new InvalidOperationException("Payment intent is already assigned.");
        PaymentIntentId = paymentIntentId;
    }

    /// <summary>Payment succeeded — moves from <see cref="OrderStatus.PaymentPending"/> to <see cref="OrderStatus.Confirmed"/>.</summary>
    public void Confirm(DateTimeOffset occurredAt)
    {
        if (Status != OrderStatus.PaymentPending)
            throw new InvalidOperationException($"Cannot confirm order in status {Status}.");

        Status = OrderStatus.Confirmed;
        _domainEvents.Add(new OrderConfirmed(Id, occurredAt));
    }

    /// <summary>Warehouse starts fulfillment — <see cref="OrderStatus.Confirmed"/> to <see cref="OrderStatus.Processing"/>.</summary>
    public void StartProcessing(DateTimeOffset occurredAt)
    {
        if (Status != OrderStatus.Confirmed)
            throw new InvalidOperationException($"Cannot start processing in status {Status}.");

        Status = OrderStatus.Processing;
    }

    /// <summary>Fulfillment completed — moves from <see cref="OrderStatus.Processing"/> to <see cref="OrderStatus.Shipped"/>.</summary>
    public void MarkShipped(DateTimeOffset occurredAt)
    {
        if (Status != OrderStatus.Processing)
            throw new InvalidOperationException($"Cannot ship order in status {Status}.");

        Status = OrderStatus.Shipped;
        _domainEvents.Add(new OrderShipped(Id, occurredAt));
    }

    /// <summary>Delivery completed — <see cref="OrderStatus.Shipped"/> to <see cref="OrderStatus.Delivered"/>.</summary>
    public void MarkDelivered(DateTimeOffset occurredAt)
    {
        if (Status != OrderStatus.Shipped)
            throw new InvalidOperationException($"Cannot mark delivered in status {Status}.");

        Status = OrderStatus.Delivered;
        _domainEvents.Add(new OrderDelivered(Id, occurredAt));
    }

    /// <summary>Cancels the order unless it is already shipped, delivered, or cancelled.</summary>
    public void Cancel(string reason, DateTimeOffset occurredAt)
    {
        if (Status is OrderStatus.Shipped or OrderStatus.Delivered)
            throw new InvalidOperationException("Cannot cancel a shipped or delivered order.");

        if (Status == OrderStatus.Cancelled)
            throw new InvalidOperationException("Order is already cancelled.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Cancellation reason is required.", nameof(reason));

        Status = OrderStatus.Cancelled;
        _domainEvents.Add(new OrderCancelled(Id, reason, occurredAt));
    }

    // ── DAI-637 Failure transitions ─────────────────────────────────────────

    /// <summary>Returns true if the order is in any failure state recoverable via retry.</summary>
    public bool IsFailed => Status is OrderStatus.PaymentFailed
        or OrderStatus.AuthFailed
        or OrderStatus.AddressError
        or OrderStatus.StockError
        or OrderStatus.SystemError;

    /// <summary>Retry a failed order — moves back to <see cref="OrderStatus.PaymentPending"/> for re-attempt.</summary>
    public void RetryFailure(DateTimeOffset occurredAt)
    {
        if (!IsFailed)
            throw new InvalidOperationException($"Cannot retry order in status {Status}; not a failure state.");

        Status = OrderStatus.PaymentPending;
        RetryCount++;
        FailureReason = null;
        FailureErrorCode = null;
        FailedAt = null;
        _domainEvents.Add(new OrderFailureRetried(Id, occurredAt));
    }

    public void MarkFailed(OrderStatus failureStatus, string reason, string? errorCode, DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Failure reason is required.", nameof(reason));

        if (failureStatus is not (OrderStatus.PaymentFailed or OrderStatus.AuthFailed or OrderStatus.AddressError
            or OrderStatus.StockError or OrderStatus.SystemError))
            throw new ArgumentException("Invalid failure status.", nameof(failureStatus));

        Status = failureStatus;
        FailureReason = reason.Trim();
        FailureErrorCode = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim();
        FailedAt = occurredAt;
        _domainEvents.Add(new OrderFailed(Id, TenantId, StoreId, failureStatus.ToString(), FailureReason, FailureErrorCode, occurredAt));
    }

    /// <summary>Manually resolve a failed order — moves to <see cref="OrderStatus.Cancelled"/> with note.</summary>
    public void ResolveFailure(string note, DateTimeOffset occurredAt)
    {
        if (!IsFailed)
            throw new InvalidOperationException($"Cannot resolve order in status {Status}; not a failure state.");

        Status = OrderStatus.Cancelled;
        _domainEvents.Add(new OrderFailureResolved(Id, note ?? "", occurredAt));
    }
}

/// <summary>Emitted when a failed order is queued for retry.</summary>
public sealed record OrderFailureRetried(string OrderId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderFailed(
    string OrderId,
    string TenantId,
    string StoreId,
    string FailureStatus,
    string FailureReason,
    string? FailureErrorCode,
    DateTimeOffset FailedAt) : IDomainEvent;

/// <summary>Emitted when a failed order is manually resolved (treated as cancelled with note).</summary>
public sealed record OrderFailureResolved(string OrderId, string Note, DateTimeOffset OccurredAt) : IDomainEvent;
