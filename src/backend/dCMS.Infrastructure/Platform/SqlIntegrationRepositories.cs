using System.Text.Json;
using Dapper;
using dCMS.Provisioning.Domain;
using Npgsql;

namespace dCMS.Infrastructure.Platform;

public sealed class SqlIntegrationAppRepository(string connectionString) : IIntegrationAppRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString = connectionString;

    public async Task<IReadOnlyList<IntegrationAppRecord>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<AppRow>(new CommandDefinition(
            """
            SELECT "Id", "Name", "Description", "Scopes"::text AS ScopesJson, "EventTypes"::text AS EventTypesJson,
                   "IsActive", "CreatedAt", "UpdatedAt"
            FROM "IntegrationApps"
            WHERE "IsActive" = TRUE
            ORDER BY "Name"
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(MapApp).ToList();
    }

    public async Task<IntegrationAppRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var row = await conn.QuerySingleOrDefaultAsync<AppRow>(new CommandDefinition(
            """
            SELECT "Id", "Name", "Description", "Scopes"::text AS ScopesJson, "EventTypes"::text AS EventTypesJson,
                   "IsActive", "CreatedAt", "UpdatedAt"
            FROM "IntegrationApps"
            WHERE "Id" = @Id
            """,
            new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : MapApp(row);
    }

    public async Task UpsertAsync(IntegrationAppRecord record, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO "IntegrationApps" ("Id", "Name", "Description", "Scopes", "EventTypes", "IsActive", "CreatedAt", "UpdatedAt")
            VALUES (@Id, @Name, @Description, @Scopes::jsonb, @EventTypes::jsonb, @IsActive, @CreatedAt, @UpdatedAt)
            ON CONFLICT ("Id") DO UPDATE SET
                "Name" = EXCLUDED."Name",
                "Description" = EXCLUDED."Description",
                "Scopes" = EXCLUDED."Scopes",
                "EventTypes" = EXCLUDED."EventTypes",
                "IsActive" = EXCLUDED."IsActive",
                "UpdatedAt" = NOW()
            """, new
        {
            record.Id,
            record.Name,
            record.Description,
            Scopes = JsonSerializer.Serialize(record.Scopes, Json),
            EventTypes = JsonSerializer.Serialize(record.EventTypes, Json),
            record.IsActive,
            CreatedAt = record.CreatedAt.UtcDateTime,
            UpdatedAt = record.UpdatedAt.UtcDateTime,
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static IntegrationAppRecord MapApp(AppRow r) => new(
        r.Id,
        r.Name,
        r.Description,
        JsonSerializer.Deserialize<List<string>>(r.ScopesJson ?? "[]", Json) ?? [],
        JsonSerializer.Deserialize<List<string>>(r.EventTypesJson ?? "[]", Json) ?? [],
        r.IsActive,
        new DateTimeOffset(r.CreatedAt, TimeSpan.Zero),
        new DateTimeOffset(r.UpdatedAt, TimeSpan.Zero));

    private sealed class AppRow
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string? Description { get; init; }
        public string? ScopesJson { get; init; }
        public string? EventTypesJson { get; init; }
        public bool IsActive { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}

public sealed class SqlTenantIntegrationRepository(string connectionString) : ITenantIntegrationRepository
{
    private readonly string _connectionString = connectionString;

    public async Task<IReadOnlyList<TenantIntegrationRecord>> ListByTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<IntRow>(new CommandDefinition(
            """
            SELECT "Id", "TenantId", "AppId", "ClientId", "ClientSecretHash", "Status", "CreatedAt", "UpdatedAt"
            FROM "TenantIntegrations"
            WHERE "TenantId" = @TenantId
            ORDER BY "CreatedAt" DESC
            """,
            new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    public async Task<TenantIntegrationRecord?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var row = await conn.QuerySingleOrDefaultAsync<IntRow>(new CommandDefinition(
            """
            SELECT "Id", "TenantId", "AppId", "ClientId", "ClientSecretHash", "Status", "CreatedAt", "UpdatedAt"
            FROM "TenantIntegrations"
            WHERE "ClientId" = @ClientId
            """,
            new { ClientId = clientId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    public async Task CreateAsync(TenantIntegrationRecord record, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO "TenantIntegrations" (
                "Id", "TenantId", "AppId", "ClientId", "ClientSecretHash", "Status", "CreatedAt", "UpdatedAt")
            VALUES (@Id, @TenantId, @AppId, @ClientId, @ClientSecretHash, @Status, @CreatedAt, @UpdatedAt)
            """, new
        {
            record.Id,
            record.TenantId,
            record.AppId,
            record.ClientId,
            record.ClientSecretHash,
            Status = record.Status.ToDbString(),
            CreatedAt = record.CreatedAt.UtcDateTime,
            UpdatedAt = record.UpdatedAt.UtcDateTime,
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task RevokeAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE "TenantIntegrations"
            SET "Status" = 'revoked', "UpdatedAt" = NOW()
            WHERE "Id" = @Id
            """,
            new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static TenantIntegrationRecord Map(IntRow r) => new(
        r.Id,
        r.TenantId,
        r.AppId,
        r.ClientId,
        r.ClientSecretHash,
        TenantIntegrationStatusExtensions.FromDbString(r.Status),
        new DateTimeOffset(r.CreatedAt, TimeSpan.Zero),
        new DateTimeOffset(r.UpdatedAt, TimeSpan.Zero));

    private sealed class IntRow
    {
        public string Id { get; init; } = "";
        public string TenantId { get; init; } = "";
        public string AppId { get; init; } = "";
        public string ClientId { get; init; } = "";
        public string ClientSecretHash { get; init; } = "";
        public string Status { get; init; } = "";
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
