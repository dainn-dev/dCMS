using System.Security.Claims;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Notification.Api.Routes;

public sealed class TemplateRepository
{
    private readonly string _cs;

    public TemplateRepository(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("Notification")
            ?? throw new InvalidOperationException("ConnectionStrings:Notification is required.");
    }

    public async Task<IReadOnlyList<TemplateRow>> ListAsync(string? tenantId, CancellationToken ct)
    {
        const string sql = """
            SELECT "Id","TenantId","Key","Locale","Channel","Subject","Body","ModelVersion","UpdatedAt","UpdatedBy"
            FROM "Templates"
            WHERE (@TenantId IS NULL OR "TenantId" = @TenantId OR "TenantId" IS NULL)
            ORDER BY COALESCE("TenantId",'') DESC, "Key" ASC, "Locale" ASC, "Channel" ASC
            LIMIT 2000
            """;
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<TemplateRow>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<TemplateRow?> GetResolvedAsync(
        string tenantId,
        string key,
        string locale,
        string channel,
        string defaultLocale,
        CancellationToken ct)
    {
        // Resolution order:
        // 1) tenant + locale
        // 2) tenant + defaultLocale
        // 3) global + locale
        // 4) global + defaultLocale
        const string sql = """
            SELECT "Id","TenantId","Key","Locale","Channel","Subject","Body","ModelVersion","UpdatedAt","UpdatedBy"
            FROM "Templates"
            WHERE "Key" = @Key AND "Channel" = @Channel
              AND (
                    ("TenantId" = @TenantId AND "Locale" = @Locale)
                 OR ("TenantId" = @TenantId AND "Locale" = @DefaultLocale)
                 OR ("TenantId" IS NULL AND "Locale" = @Locale)
                 OR ("TenantId" IS NULL AND "Locale" = @DefaultLocale)
              )
            ORDER BY
              CASE
                WHEN "TenantId" = @TenantId AND "Locale" = @Locale THEN 1
                WHEN "TenantId" = @TenantId AND "Locale" = @DefaultLocale THEN 2
                WHEN "TenantId" IS NULL AND "Locale" = @Locale THEN 3
                ELSE 4
              END
            LIMIT 1
            """;

        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        return await conn.QuerySingleOrDefaultAsync<TemplateRow>(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                Key = key,
                Locale = locale,
                DefaultLocale = defaultLocale,
                Channel = channel,
            }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task UpsertAsync(
        TemplateUpsertRequest req,
        string? actorUserId,
        string tenantIdForAudit,
        string storeIdForAudit,
        string roleForAudit,
        string ipForAudit,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var id = req.Id == Guid.Empty ? Guid.NewGuid() : req.Id;

        const string upsert = """
            INSERT INTO "Templates"
                ("Id","TenantId","Key","Locale","Channel","Subject","Body","ModelVersion","UpdatedAt","UpdatedBy")
            VALUES
                (@Id,@TenantId,@Key,@Locale,@Channel,@Subject,@Body,@ModelVersion,@UpdatedAt,@UpdatedBy)
            ON CONFLICT (COALESCE("TenantId",''),"Key","Locale","Channel")
            DO UPDATE SET
                "Subject" = EXCLUDED."Subject",
                "Body" = EXCLUDED."Body",
                "ModelVersion" = EXCLUDED."ModelVersion",
                "UpdatedAt" = EXCLUDED."UpdatedAt",
                "UpdatedBy" = EXCLUDED."UpdatedBy"
            """;

        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(upsert, new
        {
            Id = id,
            TenantId = string.IsNullOrWhiteSpace(req.TenantId) ? null : req.TenantId.Trim(),
            Key = req.Key.Trim(),
            Locale = string.IsNullOrWhiteSpace(req.Locale) ? "en-US" : req.Locale.Trim(),
            Channel = req.Channel.Trim(),
            Subject = req.Subject,
            Body = req.Body,
            ModelVersion = req.ModelVersion <= 0 ? 1 : req.ModelVersion,
            UpdatedAt = now,
            UpdatedBy = actorUserId,
        }, cancellationToken: ct)).ConfigureAwait(false);

        // Minimal audit row (reuse Catalog AuditLogs).
        const string audit = """
            INSERT INTO "AuditLogs"
                ("TenantId","StoreId","UserId","UserRole","Action","EntityType","EntityId","Diff","IpAddress","CreatedAt")
            VALUES
                (@TenantId,@StoreId,@UserId,@UserRole,@Action,@EntityType,@EntityId,@Diff,@IpAddress,@CreatedAt)
            """;
        await conn.ExecuteAsync(new CommandDefinition(audit, new
        {
            TenantId = tenantIdForAudit,
            StoreId = storeIdForAudit,
            UserId = actorUserId ?? "unknown",
            UserRole = roleForAudit,
            Action = "template_upsert",
            EntityType = "template",
            EntityId = $"{req.Key}:{req.Locale}:{req.Channel}:{(req.TenantId ?? "global")}",
            Diff = (string?)null,
            IpAddress = ipForAudit,
            CreatedAt = now,
        }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<int> DeleteAsync(string? tenantId, string key, string locale, string channel, CancellationToken ct)
    {
        const string sql = """
            DELETE FROM "Templates"
            WHERE COALESCE("TenantId",'') = COALESCE(@TenantId,'')
              AND "Key" = @Key
              AND "Locale" = @Locale
              AND "Channel" = @Channel
            """;
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        return await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            Key = key,
            Locale = locale,
            Channel = channel,
        }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public static string? ActorUserId(HttpContext http) =>
        http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? http.User.FindFirst("sub")?.Value;
}

public sealed class TemplateRow
{
    public Guid Id { get; init; }
    public string? TenantId { get; init; }
    public string Key { get; init; } = "";
    public string Locale { get; init; } = "en-US";
    public string Channel { get; init; } = "email";
    public string? Subject { get; init; }
    public string Body { get; init; } = "";
    public int ModelVersion { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}

public sealed class TemplateUpsertRequest
{
    public Guid Id { get; set; }
    public string? TenantId { get; set; }
    public string Key { get; set; } = null!;
    public string Locale { get; set; } = "en-US";
    public string Channel { get; set; } = "email";
    public string? Subject { get; set; }
    public string Body { get; set; } = null!;
    public int ModelVersion { get; set; } = 1;
}

