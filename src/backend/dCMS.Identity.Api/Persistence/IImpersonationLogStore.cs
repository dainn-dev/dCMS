namespace dCMS.Identity.Api.Persistence;

public sealed record ImpersonationLogRow(
    Guid Id,
    string ClientId,
    Guid ActorUserId,
    string TargetTenantId,
    string Reason,
    Guid TokenJti,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? EndedAt);

public interface IImpersonationLogStore
{
    Task RecordAsync(ImpersonationLogRow row, CancellationToken ct);
    Task<bool> MarkEndedAsync(Guid tokenJti, DateTimeOffset endedAt, CancellationToken ct);
    Task<ImpersonationLogRow?> FindByJtiAsync(Guid tokenJti, CancellationToken ct);
}
