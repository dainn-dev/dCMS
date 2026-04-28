namespace dCMS.Order.Core.Domain.Payments;

/// <summary>DAI-722: A single payment slice that contributes toward the order total.</summary>
public sealed class PaymentComponent
{
    public Guid Id { get; }
    public PaymentComponentType Type { get; }
    public decimal Amount { get; }
    /// <summary>Input identifier set at plan time (voucher code, customer id, etc.). Immutable.</summary>
    public string? Reference { get; }
    /// <summary>Output identifier set after Authorize (holdId, chargeRef, etc.).</summary>
    public string? ExternalRef { get; private set; }
    public PaymentComponentState State { get; private set; }
    public string? LastError { get; private set; }
    public int Ordering { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public PaymentComponent(
        Guid id,
        PaymentComponentType type,
        decimal amount,
        int ordering,
        PaymentComponentState state = PaymentComponentState.Pending,
        string? reference = null,
        string? externalRef = null,
        string? lastError = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null)
    {
        if (amount < 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be non-negative.");
        Id = id;
        Type = type;
        Amount = amount;
        Ordering = ordering;
        State = state;
        Reference = reference;
        ExternalRef = externalRef;
        LastError = lastError;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = updatedAt;
    }

    public void Authorize(string externalRef, DateTimeOffset? at = null)
    {
        ExternalRef = externalRef;
        State = PaymentComponentState.Authorized;
        UpdatedAt = at ?? DateTimeOffset.UtcNow;
    }

    public void Capture(DateTimeOffset? at = null)
    {
        State = PaymentComponentState.Captured;
        UpdatedAt = at ?? DateTimeOffset.UtcNow;
    }

    public void Fail(string error, DateTimeOffset? at = null)
    {
        State = PaymentComponentState.Failed;
        LastError = error;
        UpdatedAt = at ?? DateTimeOffset.UtcNow;
    }

    public void Refund(DateTimeOffset? at = null)
    {
        State = PaymentComponentState.Refunded;
        UpdatedAt = at ?? DateTimeOffset.UtcNow;
    }

    public void Cancel(DateTimeOffset? at = null)
    {
        State = PaymentComponentState.Cancelled;
        UpdatedAt = at ?? DateTimeOffset.UtcNow;
    }
}
