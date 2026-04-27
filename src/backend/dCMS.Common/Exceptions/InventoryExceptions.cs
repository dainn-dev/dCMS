namespace dCMS.Core.Exceptions;

/// <summary>Reserved quantity exceeds available stock (Inventory aggregate).</summary>
public sealed class OutOfStockException : Exception
{
    public OutOfStockException(string message) : base(message) { }

    public OutOfStockException(string variantId, int requested, int available)
        : base($"Insufficient stock for variant {variantId}: requested {requested}, available {available}")
    {
        VariantId = variantId;
        Requested = requested;
        Available = available;
    }

    public string? VariantId { get; }
    public int? Requested { get; }
    public int? Available { get; }
}

/// <summary>Stock invariants violated (e.g. reserved &gt; quantity after adjust).</summary>
public sealed class StockInvariantException : Exception
{
    public StockInvariantException(string message) : base(message) { }
}

/// <summary>Optimistic concurrency failure on stock row.</summary>
public sealed class StockConcurrencyException : Exception
{
    public StockConcurrencyException(string message) : base(message) { }

    public StockConcurrencyException(string variantId, string warehouseId)
        : base($"Concurrent stock modification detected for variant {variantId} in warehouse {warehouseId}. Please retry.")
    {
        VariantId = variantId;
        WarehouseId = warehouseId;
    }

    public string? VariantId { get; }
    public string? WarehouseId { get; }
}
