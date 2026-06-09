namespace dCMS.Order.Core.Ordering;

public sealed record CreateOrderLine(
    string LineId,
    string ProductId,
    string VariantId,
    /// <summary>Fulfilment warehouse for inventory sync check (DAI-314 / US-18).</summary>
    string WarehouseId,
    int Quantity,
    Domain.Money UnitPrice,
    string ProductNameSnapshot,
    string VariantSnapshotJson,
    /// <summary>DAI-725 — promotion-applied discount for this line (sum of all line adjustments).</summary>
    decimal LineDiscount = 0m);

public sealed record CreateOrderCommand(
    string OrderId,
    string TenantId,
    string StoreId,
    string CustomerId,
    string IdempotencyKey,
    IReadOnlyList<CreateOrderLine> Lines,
    Domain.ShippingAddress ShippingAddress,
    DateTimeOffset OccurredAt,
    /// <summary>DAI-649: customer profile snapshot — captured at create time so reads don't need a Customer API.</summary>
    string? CustomerName = null,
    string? CustomerEmail = null,
    string? CustomerPhone = null,
    /// <summary>DAI-725 — order-level discount summed from `OrderAdjustments[]`.</summary>
    decimal OrderDiscount = 0m,
    /// <summary>DAI-725 — promo code text supplied by customer (null if none).</summary>
    string? PromoCode = null,
    /// <summary>DAI-725 — Promotions PromoCodeId resolved during evaluate.</summary>
    string? PromoCodeId = null,
    /// <summary>DAI-725 — applied campaign snapshots persisted in OrderPromotions for audit + saga side-effects.</summary>
    IReadOnlyList<Domain.AppliedPromotionSnapshot>? AppliedPromotions = null,
    string PaymentMethod = "card");
