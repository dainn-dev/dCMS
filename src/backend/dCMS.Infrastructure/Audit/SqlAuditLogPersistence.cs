using Dapper;
using dCMS.Core.Audit;
using Npgsql;

namespace dCMS.Infrastructure.Audit;

public sealed class SqlAuditLogPersistence(string connectionString)
{
    private readonly string _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task InsertAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO "AuditLogs" ("TenantId", "StoreId", "UserId", "UserRole", "Action", "EntityType", "EntityId", "Diff", "IpAddress", "CreatedAt")
            VALUES (@TenantId, @StoreId, @UserId, @UserRole, @Action, @EntityType, @EntityId, @Diff, @IpAddress, @CreatedAt)
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql,
            new
            {
                entry.TenantId,
                entry.StoreId,
                entry.UserId,
                entry.UserRole,
                entry.Action,
                entry.EntityType,
                entry.EntityId,
                entry.Diff,
                entry.IpAddress,
                entry.CreatedAt
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
