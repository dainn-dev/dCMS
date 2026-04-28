namespace dCMS.Core.Messaging;

// DAI-684 / DAI-706: published when a bulk import file is uploaded; consumed by Catalog.Worker.
public sealed record ImportJobQueuedV1(
    string JobId,
    string TenantId,
    string Type,
    string FileKey,
    DateTimeOffset OccurredAt);
