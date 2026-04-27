namespace dCMS.Messaging.Contracts.Messaging;

/// <summary>DAI-717: queue an email render+send using a template.</summary>
public sealed record EmailQueuedV1(
    string IdempotencyKey,
    string TenantId,
    string? StoreId,
    string TemplateKey,
    string Locale,
    string ToAddress,
    string Channel, // email
    string ModelJson,
    DateTimeOffset OccurredAt);

