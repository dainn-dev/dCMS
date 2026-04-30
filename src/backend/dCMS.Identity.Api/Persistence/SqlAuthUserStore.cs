using Dapper;
using Npgsql;

namespace dCMS.Identity.Api.Persistence;

public sealed class SqlAuthUserStore(string connectionString) : IAuthUserStore
{
    public async Task<AuthUserRow?> FindByEmailAsync(string clientId, string email, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        return await conn.QuerySingleOrDefaultAsync<AuthUserRow>(new CommandDefinition(
            """
            SELECT "Id" AS Id, "ClientId" AS ClientId, "TenantId" AS TenantId,
                   "Email" AS Email, "DisplayName" AS DisplayName, "PasswordHash" AS PasswordHash,
                   "Role" AS Role, "IsActive" AS IsActive, "CreatedAt" AS CreatedAt
              FROM "AuthUsers"
             WHERE "ClientId" = @ClientId AND LOWER("Email") = LOWER(@Email)
             LIMIT 1;
            """,
            new { ClientId = clientId, Email = email }, cancellationToken: ct));
    }

    public async Task<AuthUserRow?> FindByIdAsync(Guid id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        return await conn.QuerySingleOrDefaultAsync<AuthUserRow>(new CommandDefinition(
            """
            SELECT "Id" AS Id, "ClientId" AS ClientId, "TenantId" AS TenantId,
                   "Email" AS Email, "DisplayName" AS DisplayName, "PasswordHash" AS PasswordHash,
                   "Role" AS Role, "IsActive" AS IsActive, "CreatedAt" AS CreatedAt
              FROM "AuthUsers"
             WHERE "Id" = @Id
             LIMIT 1;
            """,
            new { Id = id }, cancellationToken: ct));
    }
}
