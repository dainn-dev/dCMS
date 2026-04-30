using Dapper;
using Npgsql;

namespace dCMS.Identity.Api.Persistence;

public sealed class SqlImpersonationLogStore(string connectionString) : IImpersonationLogStore
{
    public async Task RecordAsync(ImpersonationLogRow row, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO "ImpersonationLog"
              ("Id","ClientId","ActorUserId","TargetTenantId","Reason","TokenJti","IssuedAt","ExpiresAt","EndedAt")
            VALUES (@Id,@ClientId,@ActorUserId,@TargetTenantId,@Reason,@TokenJti,@IssuedAt,@ExpiresAt,@EndedAt);
            """,
            row, cancellationToken: ct));
    }

    public async Task<bool> MarkEndedAsync(Guid tokenJti, DateTimeOffset endedAt, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE "ImpersonationLog"
               SET "EndedAt" = @EndedAt
             WHERE "TokenJti" = @TokenJti AND "EndedAt" IS NULL;
            """,
            new { TokenJti = tokenJti, EndedAt = endedAt }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<ImpersonationLogRow?> FindByJtiAsync(Guid tokenJti, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        return await conn.QuerySingleOrDefaultAsync<ImpersonationLogRow>(new CommandDefinition(
            """
            SELECT "Id" AS Id, "ClientId" AS ClientId, "ActorUserId" AS ActorUserId,
                   "TargetTenantId" AS TargetTenantId, "Reason" AS Reason, "TokenJti" AS TokenJti,
                   "IssuedAt" AS IssuedAt, "ExpiresAt" AS ExpiresAt, "EndedAt" AS EndedAt
              FROM "ImpersonationLog"
             WHERE "TokenJti" = @TokenJti
             LIMIT 1;
            """,
            new { TokenJti = tokenJti }, cancellationToken: ct));
    }
}
