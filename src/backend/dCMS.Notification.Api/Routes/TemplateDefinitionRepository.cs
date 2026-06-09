using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Notification.Api.Routes;

public sealed class TemplateDefinitionRow
{
    public Guid Id { get; init; }
    public string TenantId { get; init; } = "";
    public string Key { get; init; } = "";
    public string Channel { get; init; } = "email";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    /// <summary>Raw JSON array of { path, label, sample }.</summary>
    public string Variables { get; init; } = "[]";
    public string? DefaultSubject { get; init; }
    public string? DefaultBody { get; init; }
    public int SortOrder { get; init; }
}

public sealed class TemplateDefinitionRepository
{
    private readonly string _cs;

    public TemplateDefinitionRepository(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("Notification")
            ?? throw new InvalidOperationException("ConnectionStrings:Notification is required.");
    }

    public async Task<IReadOnlyList<TemplateDefinitionRow>> ListAsync(string tenantId, CancellationToken ct)
    {
        const string sql = """
            SELECT "Id","TenantId","Key","Channel","Name","Description",
                   "Variables"::text AS "Variables","DefaultSubject","DefaultBody","SortOrder"
            FROM "TemplateDefinitions"
            WHERE "TenantId" = @TenantId
            ORDER BY "SortOrder" ASC, "Name" ASC
            """;
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<TemplateDefinitionRow>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<int> CountAsync(string tenantId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM \"TemplateDefinitions\" WHERE \"TenantId\" = @TenantId",
            new { TenantId = tenantId }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task UpsertAsync(
        string tenantId,
        string key,
        string channel,
        string name,
        string description,
        string variablesJson,
        string? defaultSubject,
        string? defaultBody,
        string? actorUserId,
        CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        // Preserve order on edit; append new definitions to the end.
        var existingSort = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT \"SortOrder\" FROM \"TemplateDefinitions\" WHERE \"TenantId\" = @TenantId AND \"Key\" = @Key AND \"Channel\" = @Channel",
            new { TenantId = tenantId, Key = key, Channel = channel }, cancellationToken: ct)).ConfigureAwait(false);
        int sortOrder;
        if (existingSort.HasValue)
        {
            sortOrder = existingSort.Value;
        }
        else
        {
            var maxSort = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT MAX(\"SortOrder\") FROM \"TemplateDefinitions\" WHERE \"TenantId\" = @TenantId",
                new { TenantId = tenantId }, cancellationToken: ct)).ConfigureAwait(false);
            sortOrder = (maxSort ?? 0) + 10;
        }

        const string sql = """
            INSERT INTO "TemplateDefinitions"
                ("Id","TenantId","Key","Channel","Name","Description","Variables","DefaultSubject","DefaultBody","SortOrder","UpdatedAt","UpdatedBy")
            VALUES
                (@Id,@TenantId,@Key,@Channel,@Name,@Description,@Variables::jsonb,@DefaultSubject,@DefaultBody,@SortOrder,@UpdatedAt,@UpdatedBy)
            ON CONFLICT ("TenantId","Key","Channel")
            DO UPDATE SET
                "Name" = EXCLUDED."Name",
                "Description" = EXCLUDED."Description",
                "Variables" = EXCLUDED."Variables",
                "DefaultSubject" = EXCLUDED."DefaultSubject",
                "DefaultBody" = EXCLUDED."DefaultBody",
                "UpdatedAt" = EXCLUDED."UpdatedAt",
                "UpdatedBy" = EXCLUDED."UpdatedBy"
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = key,
            Channel = channel,
            Name = name,
            Description = description,
            Variables = variablesJson,
            DefaultSubject = defaultSubject,
            DefaultBody = defaultBody,
            SortOrder = sortOrder,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = actorUserId,
        }, cancellationToken: ct)).ConfigureAwait(false);
    }

    /// <summary>Deletes the definition and any saved template content for that key/channel (all locales).</summary>
    public async Task<int> DeleteAsync(string tenantId, string key, string channel, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

        var deleted = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM \"TemplateDefinitions\" WHERE \"TenantId\" = @TenantId AND \"Key\" = @Key AND \"Channel\" = @Channel",
            new { TenantId = tenantId, Key = key, Channel = channel }, tx, cancellationToken: ct)).ConfigureAwait(false);

        // Remove the operator's saved content for this message too (tenant scope).
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM \"Templates\" WHERE \"TenantId\" = @TenantId AND \"Key\" = @Key AND \"Channel\" = @Channel",
            new { TenantId = tenantId, Key = key, Channel = channel }, tx, cancellationToken: ct)).ConfigureAwait(false);

        await tx.CommitAsync(ct).ConfigureAwait(false);
        return deleted;
    }

    /// <summary>Idempotently seeds a tenant's catalog from the supplied defaults (used on first access).</summary>
    public async Task SeedAsync(string tenantId, IReadOnlyList<(string Key, string Channel, string Name, string Description, string VariablesJson, string? DefaultSubject, string? DefaultBody)> defaults, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO "TemplateDefinitions"
                ("Id","TenantId","Key","Channel","Name","Description","Variables","DefaultSubject","DefaultBody","SortOrder","UpdatedAt")
            VALUES
                (@Id,@TenantId,@Key,@Channel,@Name,@Description,@Variables::jsonb,@DefaultSubject,@DefaultBody,@SortOrder,@UpdatedAt)
            ON CONFLICT ("TenantId","Key","Channel") DO NOTHING
            """;
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var i = 0;
        foreach (var d in defaults)
        {
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                d.Key,
                d.Channel,
                d.Name,
                d.Description,
                Variables = d.VariablesJson,
                d.DefaultSubject,
                d.DefaultBody,
                SortOrder = i++ * 10,
                UpdatedAt = now,
            }, cancellationToken: ct)).ConfigureAwait(false);
        }
    }
}
