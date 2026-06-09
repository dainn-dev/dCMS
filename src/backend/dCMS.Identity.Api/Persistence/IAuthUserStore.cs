namespace dCMS.Identity.Api.Persistence;

public sealed record AuthUserRow(
    Guid Id,
    string ClientId,
    string? TenantId,
    string? StoreId,
    string Email,
    string DisplayName,
    string PasswordHash,
    string Role,
    bool IsActive,
    DateTime CreatedAt);

public interface IAuthUserStore
{
    Task<AuthUserRow?> FindByEmailAsync(string clientId, string email, CancellationToken ct);
    Task<AuthUserRow?> FindByIdAsync(Guid id, CancellationToken ct);
}
