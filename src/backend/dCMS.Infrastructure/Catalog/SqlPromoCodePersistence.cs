using Dapper;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using Npgsql;

namespace dCMS.Infrastructure.Catalog;

/// <summary>
/// Dapper + PostgreSQL implementation of <see cref="IPromoCodePersistence"/> (DAI-659).
/// </summary>
public sealed class SqlPromoCodePersistence(string connectionString) : IPromoCodePersistence
{
    private readonly string _cs = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    private sealed class PromoCodeDapperRow
    {
        public string    Id                  { get; init; } = null!;
        public string    TenantId            { get; init; } = null!;
        public string    Code                { get; init; } = null!;
        public string    NameJson            { get; init; } = "{}";
        public string    DiscountType        { get; init; } = null!;
        public string    DiscountValue       { get; init; } = "";
        public string    WorkflowState       { get; init; } = "draft";
        public DateTime  CreatedAt           { get; init; }
        public DateTime  UpdatedAt           { get; init; }
        public string?   PromoTypeLabel      { get; init; }
        public string    MinSpend            { get; init; } = "";
        public DateTime? StartDate           { get; init; }
        public DateTime? EndDate             { get; init; }
        public string?   SubmittedByUserId   { get; init; }
        public DateTime? SubmittedAt         { get; init; }

        public PromoCodeRow ToModel() => new(
            Id, TenantId, Code, NameJson, DiscountType, DiscountValue, WorkflowState,
            new DateTimeOffset(CreatedAt, TimeSpan.Zero),
            new DateTimeOffset(UpdatedAt, TimeSpan.Zero),
            PromoTypeLabel ?? "",
            MinSpend ?? "",
            StartDate.HasValue ? new DateTimeOffset(StartDate.Value, TimeSpan.Zero) : null,
            EndDate.HasValue ? new DateTimeOffset(EndDate.Value, TimeSpan.Zero) : null,
            SubmittedByUserId,
            SubmittedAt.HasValue ? new DateTimeOffset(SubmittedAt.Value, TimeSpan.Zero) : null);
    }

    private const string PromoCols = """
        p."Id",p."TenantId",p."Code",p."NameJson",p."DiscountType",p."DiscountValue",
        p."WorkflowState",p."CreatedAt",p."UpdatedAt",
        p."PromoTypeLabel",p."MinSpend",p."StartDate",p."EndDate"
        """;

    public async Task<(IReadOnlyList<PromoCodeRow> Items, int Total)> ListPromoCodesAsync(
        string tenantId, string? status, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var clauses = new List<string> { @"p.""TenantId"" = @TenantId" };
        if (!string.IsNullOrWhiteSpace(status))
        {
            clauses.Add(@"p.""WorkflowState"" = @Status");
            p.Add("Status", status);
        }

        var where = "WHERE " + string.Join(" AND ", clauses);
        await using var conn = new NpgsqlConnection(_cs);

        var total = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition($"""
                SELECT COUNT(*)::INT FROM "PromoCodes" p {where}
                """, p, cancellationToken: cancellationToken)).ConfigureAwait(false);

        p.Add("PageSize", pageSize);
        p.Add("Offset", Math.Max(0, page - 1) * pageSize);

        var sql = $"""
            SELECT {PromoCols},
                   sub."ActorUserId" AS "SubmittedByUserId",
                   sub."CreatedAt"   AS "SubmittedAt"
            FROM "PromoCodes" p
            LEFT JOIN LATERAL (
              SELECT h."ActorUserId", h."CreatedAt"
              FROM "PromoCodeWorkflowHistory" h
              WHERE h."PromoCodeId" = p."Id" AND h."TenantId" = p."TenantId" AND h."ToState" = 'pending_approval'
              ORDER BY h."CreatedAt" DESC
              LIMIT 1
            ) sub ON true
            {where}
            ORDER BY p."UpdatedAt" DESC
            LIMIT @PageSize OFFSET @Offset
            """;

        var rows = await conn.QueryAsync<PromoCodeDapperRow>(
            new CommandDefinition(sql, p, cancellationToken: cancellationToken)).ConfigureAwait(false);

        return (rows.Select(r => r.ToModel()).ToList(), total);
    }

    public async Task<PromoCodeRow?> GetPromoCodeAsync(string id, string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var row = await conn.QuerySingleOrDefaultAsync<PromoCodeDapperRow>(
            new CommandDefinition(
                """SELECT "Id","TenantId","Code","NameJson","DiscountType","DiscountValue","WorkflowState","CreatedAt","UpdatedAt","PromoTypeLabel","MinSpend","StartDate","EndDate" FROM "PromoCodes" WHERE "Id"=@Id AND "TenantId"=@TenantId""",
                new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row?.ToModel();
    }

    public async Task<bool> PromoCodeExistsAsync(string tenantId, string code, string? excludeId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var sql = excludeId is null
            ? """SELECT CASE WHEN EXISTS(SELECT 1 FROM "PromoCodes" WHERE "TenantId"=@TenantId AND "Code"=@Code) THEN 1 ELSE 0 END"""
            : """SELECT CASE WHEN EXISTS(SELECT 1 FROM "PromoCodes" WHERE "TenantId"=@TenantId AND "Code"=@Code AND "Id"<>@Excl) THEN 1 ELSE 0 END""";
        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { TenantId = tenantId, Code = code, Excl = excludeId },
                cancellationToken: cancellationToken)).ConfigureAwait(false) == 1;
    }

    public async Task CreatePromoCodeAsync(PromoCodeRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            INSERT INTO "PromoCodes"
            ("Id","TenantId","Code","NameJson","DiscountType","DiscountValue","WorkflowState","CreatedAt","UpdatedAt",
             "PromoTypeLabel","MinSpend","StartDate","EndDate")
            VALUES (@Id,@TenantId,@Code,@NameJson,@DiscountType,@DiscountValue,@WorkflowState,@CreatedAt,@UpdatedAt,
                    @PromoTypeLabel,@MinSpend,@StartDate,@EndDate)
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            row.Id, row.TenantId, row.Code, row.NameJson, row.DiscountType, row.DiscountValue, row.WorkflowState,
            CreatedAt = row.CreatedAt.UtcDateTime, UpdatedAt = row.UpdatedAt.UtcDateTime,
            PromoTypeLabel = string.IsNullOrWhiteSpace(row.PromoTypeLabel) ? null : row.PromoTypeLabel,
            row.MinSpend,
            StartDate = row.StartDate?.UtcDateTime,
            EndDate = row.EndDate?.UtcDateTime,
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> UpdatePromoCodeAsync(PromoCodeRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        const string sql = """
            UPDATE "PromoCodes" SET
                "Code" = @Code,
                "NameJson" = @NameJson,
                "DiscountType" = @DiscountType,
                "DiscountValue" = @DiscountValue,
                "PromoTypeLabel" = @PromoTypeLabel,
                "MinSpend" = @MinSpend,
                "StartDate" = @StartDate,
                "EndDate" = @EndDate,
                "UpdatedAt" = @UpdatedAt
            WHERE "Id" = @Id AND "TenantId" = @TenantId
            """;
        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            row.Id, row.TenantId, row.Code, row.NameJson, row.DiscountType, row.DiscountValue,
            PromoTypeLabel = string.IsNullOrWhiteSpace(row.PromoTypeLabel) ? null : row.PromoTypeLabel,
            row.MinSpend,
            StartDate = row.StartDate?.UtcDateTime,
            EndDate = row.EndDate?.UtcDateTime,
            UpdatedAt = DateTime.UtcNow,
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    public async Task<bool> TransitionWorkflowAsync(
        string id, string tenantId, string toState,
        string actorUserId, string comment,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var current = await conn.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("""SELECT "WorkflowState" FROM "PromoCodes" WHERE "Id"=@Id AND "TenantId"=@TenantId""",
                new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (current is null) return false;
        if (!PromoCodeRow.CanTransitionTo(current, toState))
            throw new InvalidOperationException(
                $"Cannot transition promo code from '{current}' to '{toState}'.");

        await conn.ExecuteAsync(new CommandDefinition(
            """UPDATE "PromoCodes" SET "WorkflowState"=@ToState,"UpdatedAt"=@Now WHERE "Id"=@Id AND "TenantId"=@TenantId""",
            new { ToState = toState, Now = DateTime.UtcNow, Id = id, TenantId = tenantId },
            tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

        await conn.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO ""PromoCodeWorkflowHistory""(""PromoCodeId"",""TenantId"",""ActorUserId"",""FromState"",""ToState"",""Comment"",""CreatedAt"")
               VALUES(@PromoCodeId,@TenantId,@ActorUserId,@FromState,@ToState,@Comment,@CreatedAt)",
            new
            {
                PromoCodeId = id, TenantId = tenantId, ActorUserId = actorUserId,
                FromState = current, ToState = toState, Comment = comment,
                CreatedAt = DateTime.UtcNow,
            },
            tx, cancellationToken: cancellationToken)).ConfigureAwait(false);

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
