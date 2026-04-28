using System.Text.Json;
using Dapper;
using dCMS.Core.Persistence;
using Npgsql;

namespace dCMS.Infrastructure.Approvals;

public sealed class SqlApprovalRequestPersistence(string connectionString) : IApprovalRequestPersistence
{
    public async Task<Guid> CreatePendingAsync(
        string tenantId,
        string entityType,
        string entityId,
        string requestedByUserId,
        string? currentApproverUserId,
        string payloadSnapshotJson,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var history = JsonSerializer.Serialize(new[]
        {
            new { state = "PendingApproval", by = requestedByUserId, at = now, notes = (string?)null }
        });

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO "ApprovalRequests"
              ("Id","TenantId","EntityType","EntityId","State","RequestedBy","RequestedAt","CurrentApprover","History","PayloadSnapshot")
            VALUES
              (@Id,@TenantId,@EntityType,@EntityId,'PendingApproval',@RequestedBy,@RequestedAt,@CurrentApprover,CAST(@History AS jsonb),CAST(@PayloadSnapshot AS jsonb));
            """,
            new
            {
                Id = id,
                TenantId = tenantId,
                EntityType = entityType,
                EntityId = entityId,
                RequestedBy = requestedByUserId,
                RequestedAt = now,
                CurrentApprover = currentApproverUserId,
                History = history,
                PayloadSnapshot = payloadSnapshotJson
            },
            cancellationToken: ct));

        return id;
    }

    public async Task<ApprovalRequestRow?> GetByIdAsync(string tenantId, Guid id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        return await conn.QuerySingleOrDefaultAsync<ApprovalRequestRow>(new CommandDefinition(
            """
            SELECT
              "Id" AS Id,
              "TenantId" AS TenantId,
              "EntityType" AS EntityType,
              "EntityId" AS EntityId,
              "State" AS State,
              "RequestedBy" AS RequestedBy,
              "RequestedAt" AS RequestedAt,
              "CurrentApprover" AS CurrentApprover,
              "History"::text AS HistoryJson,
              "PayloadSnapshot"::text AS PayloadSnapshotJson,
              "FinalizedAt" AS FinalizedAt
            FROM "ApprovalRequests"
            WHERE "TenantId"=@TenantId AND "Id"=@Id
            LIMIT 1;
            """,
            new { TenantId = tenantId, Id = id },
            cancellationToken: ct));
    }

    public async Task<ApprovalRequestRow?> GetByEntityAsync(string tenantId, string entityType, string entityId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        return await conn.QuerySingleOrDefaultAsync<ApprovalRequestRow>(new CommandDefinition(
            """
            SELECT
              "Id" AS Id,
              "TenantId" AS TenantId,
              "EntityType" AS EntityType,
              "EntityId" AS EntityId,
              "State" AS State,
              "RequestedBy" AS RequestedBy,
              "RequestedAt" AS RequestedAt,
              "CurrentApprover" AS CurrentApprover,
              "History"::text AS HistoryJson,
              "PayloadSnapshot"::text AS PayloadSnapshotJson,
              "FinalizedAt" AS FinalizedAt
            FROM "ApprovalRequests"
            WHERE "TenantId"=@TenantId AND "EntityType"=@EntityType AND "EntityId"=@EntityId
            ORDER BY "RequestedAt" DESC
            LIMIT 1;
            """,
            new { TenantId = tenantId, EntityType = entityType, EntityId = entityId },
            cancellationToken: ct));
    }

    public async Task<(IReadOnlyList<ApprovalRequestRow> Items, int Total)> ListAsync(
        string tenantId,
        string? entityType,
        string? state,
        string? assignedTo,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var offset = (page - 1) * pageSize;

        await using var conn = new NpgsqlConnection(connectionString);
        var where = """
            WHERE "TenantId"=@TenantId
              AND (@EntityType IS NULL OR "EntityType"=@EntityType)
              AND (@State IS NULL OR "State"=@State)
              AND (@AssignedTo IS NULL OR "CurrentApprover"=@AssignedTo)
            """;

        var total = await conn.QuerySingleAsync<int>(new CommandDefinition(
            $"""SELECT COUNT(1) FROM "ApprovalRequests" {where};""",
            new { TenantId = tenantId, EntityType = entityType, State = state, AssignedTo = assignedTo },
            cancellationToken: ct));

        var items = (await conn.QueryAsync<ApprovalRequestRow>(new CommandDefinition(
            $"""
            SELECT
              "Id" AS Id,
              "TenantId" AS TenantId,
              "EntityType" AS EntityType,
              "EntityId" AS EntityId,
              "State" AS State,
              "RequestedBy" AS RequestedBy,
              "RequestedAt" AS RequestedAt,
              "CurrentApprover" AS CurrentApprover,
              "History"::text AS HistoryJson,
              "PayloadSnapshot"::text AS PayloadSnapshotJson,
              "FinalizedAt" AS FinalizedAt
            FROM "ApprovalRequests"
            {where}
            ORDER BY "RequestedAt" DESC
            OFFSET @Offset LIMIT @Limit;
            """,
            new
            {
                TenantId = tenantId,
                EntityType = entityType,
                State = state,
                AssignedTo = assignedTo,
                Offset = offset,
                Limit = pageSize
            },
            cancellationToken: ct))).ToList();

        return (items, total);
    }

    public async Task<bool> TryTransitionAsync(
        string tenantId,
        Guid id,
        string expectedState,
        string nextState,
        string actedByUserId,
        string? notes,
        DateTimeOffset now,
        bool finalize,
        CancellationToken ct)
    {
        var historyEntry = JsonSerializer.Serialize(new
        {
            state = nextState,
            by = actedByUserId,
            at = now,
            notes
        });

        await using var conn = new NpgsqlConnection(connectionString);
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE "ApprovalRequests"
            SET
              "State"=@NextState,
              "FinalizedAt"=CASE WHEN @Finalize THEN @Now ELSE "FinalizedAt" END,
              "History"=("History" || CAST(@HistoryEntry AS jsonb))
            WHERE "TenantId"=@TenantId AND "Id"=@Id AND "State"=@ExpectedState;
            """,
            new
            {
                TenantId = tenantId,
                Id = id,
                ExpectedState = expectedState,
                NextState = nextState,
                Now = now,
                Finalize = finalize,
                HistoryEntry = historyEntry
            },
            cancellationToken: ct));

        return rows == 1;
    }

    public async Task<int> BulkApproveAsync(string tenantId, IReadOnlyList<Guid> ids, string actedByUserId, DateTimeOffset now, CancellationToken ct)
    {
        if (ids.Count == 0)
            return 0;

        var historyEntry = JsonSerializer.Serialize(new
        {
            state = "Approved",
            by = actedByUserId,
            at = now,
            notes = (string?)null
        });

        await using var conn = new NpgsqlConnection(connectionString);
        return await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE "ApprovalRequests"
            SET
              "State"='Approved',
              "FinalizedAt"=@Now,
              "History"=("History" || CAST(@HistoryEntry AS jsonb))
            WHERE "TenantId"=@TenantId
              AND "Id" = ANY(@Ids)
              AND "State"='PendingApproval';
            """,
            new { TenantId = tenantId, Ids = ids.ToArray(), Now = now, HistoryEntry = historyEntry },
            cancellationToken: ct));
    }
}

