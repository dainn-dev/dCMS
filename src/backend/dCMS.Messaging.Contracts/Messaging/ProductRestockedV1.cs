namespace dCMS.Core.Messaging;

// DAI-726: cross-service integration contract — Order publishes when an approved Return restocks units;
// Inventory consumes to bump on-hand stock.

public sealed record ProductRestockedV1(
    string OrderId,
    string TenantId,
    string StoreId,
    string VariantId,
    int Quantity,
    string ReturnId,
    DateTimeOffset OccurredAt);
