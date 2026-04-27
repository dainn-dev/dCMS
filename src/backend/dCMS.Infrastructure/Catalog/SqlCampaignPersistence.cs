using System.Text;
using Dapper;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using Npgsql;

namespace dCMS.Infrastructure.Catalog;

/// <summary>
/// Dapper + PostgreSQL implementation of <see cref="ICampaignPersistence"/> (DAI-598).
/// All queries are scoped by TenantId.
/// </summary>
public sealed class SqlCampaignPersistence(string connectionString) : ICampaignPersistence
{
    private readonly string _cs = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    // ── Internal DTO ──────────────────────────────────────────────────────────

    private sealed class CampaignDapperRow
    {
        public string    Id                   { get; init; } = null!;
        public string    TenantId             { get; init; } = null!;
        public string    Code                 { get; init; } = null!;
        public string    NameJson             { get; init; } = "{}";
        public string    EditorKind           { get; init; } = null!;
        public string    WorkflowState        { get; init; } = "draft";
        public string    Channel              { get; init; } = "Email";
        public DateTime? StartDate            { get; init; }
        public DateTime? EndDate              { get; init; }
        public string    ActiveDaysJson       { get; init; } = "[]";
        public string    ActiveMonthsJson     { get; init; } = "[]";
        public string    QualifiersJson       { get; init; } = "{}";
        public string    MechanicsJson        { get; init; } = "{}";
        public string    PromotionDetailsJson { get; init; } = "{}";
        public string    Budget               { get; init; } = "";
        public string    Audience             { get; init; } = "";
        public int       Conversions          { get; init; }
        public DateTime  CreatedAt            { get; init; }
        public DateTime  UpdatedAt            { get; init; }
        public string?   SubmittedByUserId    { get; init; }
        public DateTime? SubmittedAt          { get; init; }

        public CampaignRow ToModel() => new(
            Id, TenantId, Code, NameJson, EditorKind, WorkflowState, Channel,
            StartDate.HasValue  ? new DateTimeOffset(StartDate.Value,  TimeSpan.Zero) : null,
            EndDate.HasValue    ? new DateTimeOffset(EndDate.Value,    TimeSpan.Zero) : null,
            ActiveDaysJson, ActiveMonthsJson,
            QualifiersJson, MechanicsJson, PromotionDetailsJson,
            Budget, Audience, Conversions,
            new DateTimeOffset(CreatedAt, TimeSpan.Zero),
            new DateTimeOffset(UpdatedAt, TimeSpan.Zero),
            SubmittedByUserId,
            SubmittedAt.HasValue ? new DateTimeOffset(SubmittedAt.Value, TimeSpan.Zero) : null);
    }

    private const string CampaignCols = """
        "Id","TenantId","Code","NameJson","EditorKind","WorkflowState","Channel",
        "StartDate","EndDate","ActiveDaysJson","ActiveMonthsJson",
        "QualifiersJson","MechanicsJson","PromotionDetailsJson",
        "Budget","Audience","Conversions","CreatedAt","UpdatedAt"
        """;

    private const string CampaignColsAliased = """
        c."Id",c."TenantId",c."Code",c."NameJson",c."EditorKind",c."WorkflowState",c."Channel",
        c."StartDate",c."EndDate",c."ActiveDaysJson",c."ActiveMonthsJson",
        c."QualifiersJson",c."MechanicsJson",c."PromotionDetailsJson",
        c."Budget",c."Audience",c."Conversions",c."CreatedAt",c."UpdatedAt"
        """;

    // ── List ──────────────────────────────────────────────────────────────────

    public async Task<(IReadOnlyList<CampaignRow> Items, int Total)> ListCampaignsAsync(
        string tenantId, string? status, string? channel, string? search,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var clauses = new List<string> { "c.\"TenantId\" = @TenantId" };

        if (!string.IsNullOrWhiteSpace(status))  { clauses.Add("c.\"WorkflowState\" = @Status");  p.Add("Status",  status); }
        if (!string.IsNullOrWhiteSpace(channel)) { clauses.Add("c.\"Channel\" = @Channel");        p.Add("Channel", channel); }
        if (!string.IsNullOrWhiteSpace(search))
        {
            clauses.Add("(c.\"Code\" ILIKE @Search OR c.\"NameJson\" ILIKE @Search OR c.\"Audience\" ILIKE @Search)");
            p.Add("Search", $"%{search.Trim()}%");
        }

        var where = "WHERE " + string.Join(" AND ", clauses);
        var pendingApproval = string.Equals(status, "pending_approval", StringComparison.OrdinalIgnoreCase);
        await using var conn = new NpgsqlConnection(_cs);

        var total = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition($"""SELECT COUNT(*)::INT FROM "Campaigns" c {where}""", p,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        p.Add("PageSize", pageSize);
        p.Add("Offset", Math.Max(0, page - 1) * pageSize);

        var listSql = pendingApproval
            ? $"""
              SELECT {CampaignColsAliased},
                     h."ActorUserId" AS "SubmittedByUserId",
                     h."CreatedAt"   AS "SubmittedAt"
              FROM "Campaigns" c
              LEFT JOIN LATERAL (
                  SELECT "ActorUserId", "CreatedAt"
                  FROM "CampaignWorkflowHistory"
                  WHERE "CampaignId" = c."Id"
                    AND "TenantId"   = c."TenantId"
                    AND "ToState"    = 'pending_approval'
                  ORDER BY "CreatedAt" DESC
                  LIMIT 1
              ) h ON TRUE
              {where}
              ORDER BY c."UpdatedAt" DESC
              LIMIT @PageSize OFFSET @Offset
              """
            : $"""
              SELECT {CampaignColsAliased},
                     NULL::TEXT AS "SubmittedByUserId",
                     NULL::TIMESTAMPTZ AS "SubmittedAt"
              FROM "Campaigns" c
              {where}
              ORDER BY c."UpdatedAt" DESC
              LIMIT @PageSize OFFSET @Offset
              """;

        var rows = await conn.QueryAsync<CampaignDapperRow>(
            new CommandDefinition(listSql, p, cancellationToken: cancellationToken)).ConfigureAwait(false);

        return (rows.Select(r => r.ToModel()).ToList(), total);
    }

    // ── Get single ────────────────────────────────────────────────────────────

    public async Task<CampaignRow?> GetCampaignAsync(string id, string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var row = await conn.QuerySingleOrDefaultAsync<CampaignDapperRow>(
            new CommandDefinition($"""SELECT {CampaignCols} FROM "Campaigns" WHERE "Id"=@Id AND "TenantId"=@TenantId""",
                new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row?.ToModel();
    }

    // ── Code exists ───────────────────────────────────────────────────────────

    public async Task<bool> CampaignCodeExistsAsync(string tenantId, string code, string? excludeId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var sql = excludeId is null
            ? """SELECT CASE WHEN EXISTS(SELECT 1 FROM "Campaigns" WHERE "TenantId"=@TenantId AND "Code"=@Code) THEN 1 ELSE 0 END"""
            : """SELECT CASE WHEN EXISTS(SELECT 1 FROM "Campaigns" WHERE "TenantId"=@TenantId AND "Code"=@Code AND "Id"<>@Excl) THEN 1 ELSE 0 END""";
        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { TenantId = tenantId, Code = code, Excl = excludeId },
                cancellationToken: cancellationToken)).ConfigureAwait(false) == 1;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<string> CreateCampaignAsync(CampaignRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            INSERT INTO "Campaigns"
            ("Id","TenantId","Code","NameJson","EditorKind","WorkflowState","Channel",
             "StartDate","EndDate","ActiveDaysJson","ActiveMonthsJson",
             "QualifiersJson","MechanicsJson","PromotionDetailsJson",
             "Budget","Audience","Conversions","CreatedAt","UpdatedAt")
            VALUES
            (@Id,@TenantId,@Code,@NameJson,@EditorKind,@WorkflowState,@Channel,
             @StartDate,@EndDate,@ActiveDaysJson,@ActiveMonthsJson,
             @QualifiersJson::jsonb,@MechanicsJson::jsonb,@PromotionDetailsJson::jsonb,
             @Budget,@Audience,@Conversions,@CreatedAt,@UpdatedAt)
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            row.Id, row.TenantId, row.Code, row.NameJson, row.EditorKind, row.WorkflowState, row.Channel,
            StartDate  = row.StartDate?.UtcDateTime, EndDate = row.EndDate?.UtcDateTime,
            row.ActiveDaysJson, row.ActiveMonthsJson,
            row.QualifiersJson, row.MechanicsJson, row.PromotionDetailsJson,
            row.Budget, row.Audience, row.Conversions,
            CreatedAt = row.CreatedAt.UtcDateTime, UpdatedAt = row.UpdatedAt.UtcDateTime,
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row.Id;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task<bool> UpdateCampaignAsync(CampaignRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            UPDATE "Campaigns" SET
                "Code"                 = @Code,
                "NameJson"             = @NameJson,
                "EditorKind"           = @EditorKind,
                "Channel"              = @Channel,
                "StartDate"            = @StartDate,
                "EndDate"              = @EndDate,
                "ActiveDaysJson"       = @ActiveDaysJson,
                "ActiveMonthsJson"     = @ActiveMonthsJson,
                "QualifiersJson"       = @QualifiersJson::jsonb,
                "MechanicsJson"        = @MechanicsJson::jsonb,
                "PromotionDetailsJson" = @PromotionDetailsJson::jsonb,
                "Budget"               = @Budget,
                "Audience"             = @Audience,
                "UpdatedAt"            = @UpdatedAt
            WHERE "Id" = @Id AND "TenantId" = @TenantId
            """;
        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            row.Id, row.TenantId, row.Code, row.NameJson, row.EditorKind, row.Channel,
            StartDate  = row.StartDate?.UtcDateTime, EndDate = row.EndDate?.UtcDateTime,
            row.ActiveDaysJson, row.ActiveMonthsJson,
            row.QualifiersJson, row.MechanicsJson, row.PromotionDetailsJson,
            row.Budget, row.Audience, UpdatedAt = DateTimeOffset.UtcNow.UtcDateTime,
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public async Task<bool> DeleteCampaignAsync(string id, string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var affected = await conn.ExecuteAsync(
            new CommandDefinition("""DELETE FROM "Campaigns" WHERE "Id"=@Id AND "TenantId"=@TenantId""",
                new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    // ── Workflow transition ───────────────────────────────────────────────────

    public async Task<bool> TransitionWorkflowAsync(
        string id, string tenantId, string toState,
        string actorUserId, string comment,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var current = await conn.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("""SELECT "WorkflowState" FROM "Campaigns" WHERE "Id"=@Id AND "TenantId"=@TenantId""",
                new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (current is null) return false;
        if (!CampaignRow.CanTransitionTo(current, toState))
            throw new InvalidOperationException(
                $"Cannot transition campaign from '{current}' to '{toState}'.");

        await conn.ExecuteAsync(new CommandDefinition(
            """UPDATE "Campaigns" SET "WorkflowState"=@ToState,"UpdatedAt"=@Now WHERE "Id"=@Id AND "TenantId"=@TenantId""",
            new { ToState = toState, Now = DateTime.UtcNow, Id = id, TenantId = tenantId },
            tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

        await conn.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO ""CampaignWorkflowHistory""(""CampaignId"",""TenantId"",""ActorUserId"",""FromState"",""ToState"",""Comment"",""CreatedAt"")
               VALUES(@CampaignId,@TenantId,@ActorUserId,@FromState,@ToState,@Comment,@CreatedAt)",
            new { CampaignId = id, TenantId = tenantId, ActorUserId = actorUserId,
                  FromState = current, ToState = toState, Comment = comment,
                  CreatedAt = DateTime.UtcNow },
            tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    // ── Active by tenant (DAI-679) ────────────────────────────────────────────

    public async Task<IReadOnlyList<CampaignRow>> GetActiveByTenantAsync(
        string tenantId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = $"""
            SELECT {CampaignCols}
            FROM "Campaigns"
            WHERE "TenantId" = @TenantId
              AND "WorkflowState" = 'active'
              AND ("StartDate" IS NULL OR "StartDate" <= @Now)
              AND ("EndDate"   IS NULL OR "EndDate"   >= @Now)
            ORDER BY "CreatedAt" ASC
            """;
        var rows = await conn.QueryAsync<CampaignDapperRow>(
            new CommandDefinition(sql,
                new { TenantId = tenantId, Now = now.UtcDateTime },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => r.ToModel()).ToList();
    }

    // ── History ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<CampaignWorkflowHistoryRow>> GetWorkflowHistoryAsync(
        string id, string tenantId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var rows = await conn.QueryAsync<CampaignWorkflowHistoryDapperRow>(
            new CommandDefinition(
                @"SELECT ""Id"",""CampaignId"",""TenantId"",""ActorUserId"",""FromState"",""ToState"",""Comment"",""CreatedAt""
                   FROM ""CampaignWorkflowHistory""
                   WHERE ""CampaignId""=@Id AND ""TenantId""=@TenantId
                   ORDER BY ""CreatedAt"" DESC",
                new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.Select(r => r.ToModel()).ToList();
    }

    private sealed class CampaignWorkflowHistoryDapperRow
    {
        public int      Id          { get; init; }
        public string   CampaignId  { get; init; } = null!;
        public string   TenantId    { get; init; } = null!;
        public string   ActorUserId { get; init; } = "";
        public string   FromState   { get; init; } = null!;
        public string   ToState     { get; init; } = null!;
        public string   Comment     { get; init; } = "";
        public DateTime CreatedAt   { get; init; }

        public CampaignWorkflowHistoryRow ToModel() =>
            new(Id, CampaignId, TenantId, ActorUserId, FromState, ToState, Comment,
                new DateTimeOffset(CreatedAt, TimeSpan.Zero));
    }
}
