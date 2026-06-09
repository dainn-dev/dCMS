using System.Text.Json;
using Dapper;
using dCMS.Provisioning.Domain;
using Npgsql;

namespace dCMS.Infrastructure.Provisioning;

public sealed class SqlTenantProvisioningRepository(string connectionString) : ITenantProvisioningRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<TenantProvisioningRecord?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var row = await conn.QuerySingleOrDefaultAsync<TenantRow>(
            new CommandDefinition(TenantSelectSql + " WHERE \"TenantId\" = @TenantId", new { TenantId = tenantId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : MapTenant(row);
    }

    public async Task<TenantProvisioningRecord?> GetByTenantCodeAsync(string tenantCode, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var row = await conn.QuerySingleOrDefaultAsync<TenantRow>(
            new CommandDefinition(TenantSelectSql + " WHERE \"TenantCode\" = @TenantCode", new { TenantCode = tenantCode },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : MapTenant(row);
    }

    public async Task CreateRequestedAsync(
        string tenantId,
        string tenantCode,
        string planTier,
        string? requestedBy,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO "TenantProvisioning" (
                "TenantId", "TenantCode", "Status", "PlanTier", "RequestedBy", "UpdatedAt")
            VALUES (@TenantId, @TenantCode, 'requested', @PlanTier, @RequestedBy, NOW())
            """, new { TenantId = tenantId, TenantCode = tenantCode, PlanTier = planTier, RequestedBy = requestedBy },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> TransitionStatusAsync(
        string tenantId,
        ProvisioningStatus from,
        ProvisioningStatus to,
        Guid? runId,
        string? failureMessage,
        string? actor,
        CancellationToken cancellationToken = default)
    {
        if (!ProvisioningStatusTransitions.CanTransition(from, to))
            throw new InvalidOperationException($"Invalid transition {from} → {to}.");

        await using var conn = new NpgsqlConnection(_connectionString);
        var sql = """
            UPDATE "TenantProvisioning"
            SET "Status" = @ToStatus,
                "CurrentRunId" = COALESCE(@RunId, "CurrentRunId"),
                "LastFailureMessage" = @FailureMessage,
                "FailureCount" = CASE WHEN @ToStatus = 'failing' THEN "FailureCount" + 1 ELSE "FailureCount" END,
                "ProvisioningStartedAt" = CASE WHEN @ToStatus = 'provisioning' AND "ProvisioningStartedAt" IS NULL THEN NOW() ELSE "ProvisioningStartedAt" END,
                "ProvisionedAt" = CASE WHEN @ToStatus = 'active' THEN NOW() ELSE "ProvisionedAt" END,
                "SuspendedAt" = CASE WHEN @ToStatus = 'suspended' THEN NOW() WHEN @ToStatus = 'active' THEN NULL ELSE "SuspendedAt" END,
                "DeprovisionedAt" = CASE WHEN @ToStatus = 'deprovisioned' THEN NOW() ELSE "DeprovisionedAt" END,
                "LastSuccessfulRunId" = CASE WHEN @ToStatus = 'active' THEN @RunId ELSE "LastSuccessfulRunId" END,
                "UpdatedAt" = NOW()
            WHERE "TenantId" = @TenantId AND "Status" = @FromStatus
            """;

        var n = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            FromStatus = from.ToDbString(),
            ToStatus = to.ToDbString(),
            RunId = runId,
            FailureMessage = failureMessage
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (n == 1 && actor is not null)
        {
            await AppendAuditAsync(tenantId, runId, "status_transition",
                from.ToDbString(), to.ToDbString(), actor, "{}", cancellationToken).ConfigureAwait(false);
        }

        return n == 1;
    }

    public async Task UpdateInfrastructureAsync(
        string tenantId,
        string? umbracoDbName,
        string? envFilePath,
        string? primaryDomain,
        Guid? currentRunId,
        Guid? lastSuccessfulRunId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE "TenantProvisioning"
            SET "UmbracoDbName" = COALESCE(@UmbracoDbName, "UmbracoDbName"),
                "EnvFilePath" = COALESCE(@EnvFilePath, "EnvFilePath"),
                "PrimaryDomain" = COALESCE(@PrimaryDomain, "PrimaryDomain"),
                "CurrentRunId" = COALESCE(@CurrentRunId, "CurrentRunId"),
                "LastSuccessfulRunId" = COALESCE(@LastSuccessfulRunId, "LastSuccessfulRunId"),
                "UpdatedAt" = NOW()
            WHERE "TenantId" = @TenantId
            """, new
        {
            TenantId = tenantId,
            UmbracoDbName = umbracoDbName,
            EnvFilePath = envFilePath,
            PrimaryDomain = primaryDomain,
            CurrentRunId = currentRunId,
            LastSuccessfulRunId = lastSuccessfulRunId
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task SetOnboardingCompleteAsync(string tenantId, bool complete, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE "TenantProvisioning"
            SET "OnboardingComplete" = @Complete,
                "OnboardingCompletedAt" = CASE WHEN @Complete THEN NOW() ELSE NULL END,
                "UpdatedAt" = NOW()
            WHERE "TenantId" = @TenantId
            """, new { TenantId = tenantId, Complete = complete }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProvisioningStepRecord>> GetStepsAsync(
        string tenantId,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<StepRow>(new CommandDefinition("""
            SELECT "Id", "TenantId", "RunId", "StepOrder", "StepName", "Status", "AttemptCount", "MaxRetries",
                   "ErrorMessage", "LastAttemptAt", "Checkpoint"::text AS CheckpointJson,
                   "RollbackStatus", "RollbackAttemptedAt", "RollbackErrorMessage",
                   "CreatedAt", "UpdatedAt", "CompletedAt"
            FROM "ProvisioningSteps"
            WHERE "TenantId" = @TenantId AND "RunId" = @RunId
            ORDER BY "StepOrder"
            """, new { TenantId = tenantId, RunId = runId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.Select(MapStep).ToList();
    }

    public async Task<IReadOnlyList<ProvisioningStepRecord>> GetSucceededStepsForRunAsync(
        string tenantId,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<StepRow>(new CommandDefinition("""
            SELECT "Id", "TenantId", "RunId", "StepOrder", "StepName", "Status", "AttemptCount", "MaxRetries",
                   "ErrorMessage", "LastAttemptAt", "Checkpoint"::text AS CheckpointJson,
                   "RollbackStatus", "RollbackAttemptedAt", "RollbackErrorMessage",
                   "CreatedAt", "UpdatedAt", "CompletedAt"
            FROM "ProvisioningSteps"
            WHERE "TenantId" = @TenantId AND "RunId" = @RunId AND "Status" = 'succeeded'
            ORDER BY "StepOrder" DESC
            """, new { TenantId = tenantId, RunId = runId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.Select(MapStep).ToList();
    }

    public async Task UpsertStepAsync(
        string tenantId,
        Guid runId,
        int stepOrder,
        string stepName,
        ProvisioningStepStatus status,
        int attemptCount,
        int maxRetries,
        string? errorMessage,
        string checkpointJson,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO "ProvisioningSteps" (
                "TenantId", "RunId", "StepOrder", "StepName", "Status", "AttemptCount", "MaxRetries",
                "ErrorMessage", "LastAttemptAt", "Checkpoint", "UpdatedAt", "CompletedAt")
            VALUES (
                @TenantId, @RunId, @StepOrder, @StepName, @Status, @AttemptCount, @MaxRetries,
                @ErrorMessage, NOW(), @CheckpointJson::jsonb, NOW(),
                CASE WHEN @Status = 'succeeded' THEN NOW() ELSE NULL END)
            ON CONFLICT ("TenantId", "RunId", "StepOrder") DO UPDATE SET
                "StepName" = EXCLUDED."StepName",
                "Status" = EXCLUDED."Status",
                "AttemptCount" = EXCLUDED."AttemptCount",
                "MaxRetries" = EXCLUDED."MaxRetries",
                "ErrorMessage" = EXCLUDED."ErrorMessage",
                "LastAttemptAt" = NOW(),
                "Checkpoint" = EXCLUDED."Checkpoint",
                "UpdatedAt" = NOW(),
                "CompletedAt" = CASE WHEN EXCLUDED."Status" = 'succeeded' THEN NOW() ELSE "ProvisioningSteps"."CompletedAt" END
            """, new
        {
            TenantId = tenantId,
            RunId = runId,
            StepOrder = stepOrder,
            StepName = stepName,
            Status = status.ToDbString(),
            AttemptCount = attemptCount,
            MaxRetries = maxRetries,
            ErrorMessage = errorMessage,
            CheckpointJson = string.IsNullOrWhiteSpace(checkpointJson) ? "{}" : checkpointJson
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task UpdateStepRollbackAsync(
        long stepId,
        RollbackStatus rollbackStatus,
        string? rollbackErrorMessage,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE "ProvisioningSteps"
            SET "RollbackStatus" = @RollbackStatus,
                "RollbackAttemptedAt" = NOW(),
                "RollbackErrorMessage" = @RollbackErrorMessage,
                "Status" = CASE WHEN @RollbackStatus = 'succeeded' THEN 'rolled_back' ELSE "Status" END,
                "UpdatedAt" = NOW()
            WHERE "Id" = @Id
            """, new
        {
            Id = stepId,
            RollbackStatus = rollbackStatus.ToDbString(),
            RollbackErrorMessage = rollbackErrorMessage
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<Dictionary<string, string>> LoadStepCheckpointsAsync(
        string tenantId,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var steps = await GetStepsAsync(tenantId, runId, cancellationToken).ConfigureAwait(false);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in steps.Where(s => s.Status == ProvisioningStepStatus.Succeeded))
        {
            try
            {
                var doc = JsonDocument.Parse(step.CheckpointJson);
                if (doc.RootElement.TryGetProperty("marker", out var marker) &&
                    marker.GetString() == "completed")
                    map[step.StepName] = "completed";
            }
            catch
            {
                map[step.StepName] = "completed";
            }
        }
        return map;
    }

    public async Task AppendAuditAsync(
        string tenantId,
        Guid? runId,
        string operation,
        string? fromStatus,
        string? toStatus,
        string? actor,
        string detailsJson,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO "ProvisioningAuditLog" (
                "TenantId", "RunId", "Operation", "FromStatus", "ToStatus", "Actor", "Details")
            VALUES (@TenantId, @RunId, @Operation, @FromStatus, @ToStatus, @Actor, @DetailsJson::jsonb)
            """, new
        {
            TenantId = tenantId,
            RunId = runId,
            Operation = operation,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Actor = actor,
            DetailsJson = string.IsNullOrWhiteSpace(detailsJson) ? "{}" : detailsJson
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProvisioningAuditEntry>> ListAuditAsync(
        string tenantId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<AuditRow>(new CommandDefinition("""
            SELECT "Id", "TenantId", "RunId", "Operation", "FromStatus", "ToStatus", "Actor",
                   "Details"::text AS DetailsJson, "CreatedAt"
            FROM "ProvisioningAuditLog"
            WHERE "TenantId" = @TenantId
            ORDER BY "CreatedAt" DESC
            LIMIT @Limit
            """, new { TenantId = tenantId, Limit = Math.Clamp(limit, 1, 500) },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new ProvisioningAuditEntry(
            r.Id, r.TenantId, r.RunId, r.Operation, r.FromStatus, r.ToStatus, r.Actor,
            r.DetailsJson ?? "{}", new DateTimeOffset(r.CreatedAt, TimeSpan.Zero))).ToList();
    }

    public async Task SeedOnboardingChecklistAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        foreach (var (checkItem, isRequired) in OnboardingChecklist.DefaultItems)
        {
            await conn.ExecuteAsync(new CommandDefinition("""
                INSERT INTO "TenantOnboarding" ("TenantId", "CheckItem", "Status", "IsRequired", "UpdatedAt")
                VALUES (@TenantId, @CheckItem, 'pending', @IsRequired, NOW())
                ON CONFLICT ("TenantId", "CheckItem") DO NOTHING
                """, new { TenantId = tenantId, CheckItem = checkItem, IsRequired = isRequired },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<TenantOnboardingItem>> ListOnboardingAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<OnboardingRow>(new CommandDefinition("""
            SELECT "Id", "TenantId", "CheckItem", "Status", "IsRequired", "CompletedAt", "VerifiedAt",
                   "VerifiedBy", "Notes", "CreatedAt", "UpdatedAt"
            FROM "TenantOnboarding"
            WHERE "TenantId" = @TenantId
            ORDER BY "Id"
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new TenantOnboardingItem(
            r.Id, r.TenantId, r.CheckItem,
            OnboardingItemStatusExtensions.FromDbString(r.Status),
            r.IsRequired,
            r.CompletedAt is null ? null : new DateTimeOffset(r.CompletedAt.Value, TimeSpan.Zero),
            r.VerifiedAt is null ? null : new DateTimeOffset(r.VerifiedAt.Value, TimeSpan.Zero),
            r.VerifiedBy, r.Notes,
            new DateTimeOffset(r.CreatedAt, TimeSpan.Zero),
            new DateTimeOffset(r.UpdatedAt, TimeSpan.Zero))).ToList();
    }

    public async Task UpsertDomainBindingAsync(
        string domain,
        string tenantId,
        string storeId,
        bool isPrimary,
        DomainBindingStatus status,
        string? redisHostKey,
        string? redisKeysWrittenJson,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO "TenantDomainBindings" (
                "Domain", "TenantId", "StoreId", "IsPrimary", "Status", "RedisHostKey", "RedisKeysWritten", "UpdatedAt")
            VALUES (@Domain, @TenantId, @StoreId, @IsPrimary, @Status, @RedisHostKey, @RedisKeysWritten::jsonb, NOW())
            ON CONFLICT ("Domain") DO UPDATE SET
                "TenantId" = EXCLUDED."TenantId",
                "StoreId" = EXCLUDED."StoreId",
                "IsPrimary" = EXCLUDED."IsPrimary",
                "Status" = EXCLUDED."Status",
                "RedisHostKey" = EXCLUDED."RedisHostKey",
                "RedisKeysWritten" = EXCLUDED."RedisKeysWritten",
                "UpdatedAt" = NOW(),
                "ActivatedAt" = CASE WHEN EXCLUDED."Status" = 'active' THEN NOW() ELSE "TenantDomainBindings"."ActivatedAt" END,
                "RemovedAt" = CASE WHEN EXCLUDED."Status" = 'removed' THEN NOW() ELSE NULL END
            """, new
        {
            Domain = domain.ToLowerInvariant(),
            TenantId = tenantId,
            StoreId = storeId,
            IsPrimary = isPrimary,
            Status = status.ToDbString(),
            RedisHostKey = redisHostKey,
            RedisKeysWritten = redisKeysWrittenJson ?? "[]"
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TenantDomainBindingRecord>> ListDomainBindingsAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<DomainRow>(new CommandDefinition("""
            SELECT "Domain", "TenantId", "StoreId", "IsPrimary", "Status", "RedisHostKey",
                   "RedisKeysWritten"::text AS RedisKeysWrittenJson, "CreatedAt", "UpdatedAt", "ActivatedAt", "RemovedAt"
            FROM "TenantDomainBindings"
            WHERE "TenantId" = @TenantId
            ORDER BY "Domain"
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => new TenantDomainBindingRecord(
            r.Domain, r.TenantId, r.StoreId, r.IsPrimary,
            DomainBindingStatusExtensions.FromDbString(r.Status),
            r.RedisHostKey, r.RedisKeysWrittenJson,
            new DateTimeOffset(r.CreatedAt, TimeSpan.Zero),
            new DateTimeOffset(r.UpdatedAt, TimeSpan.Zero),
            r.ActivatedAt is null ? null : new DateTimeOffset(r.ActivatedAt.Value, TimeSpan.Zero),
            r.RemovedAt is null ? null : new DateTimeOffset(r.RemovedAt.Value, TimeSpan.Zero))).ToList();
    }

    private const string TenantSelectSql = """
        SELECT "TenantId", "TenantCode", "Status", "PlanTier", "UmbracoDbName", "EnvFilePath", "PrimaryDomain",
               "CurrentRunId", "LastSuccessfulRunId", "OnboardingComplete", "OnboardingCompletedAt",
               "RequestedAt", "RequestedBy", "ProvisioningStartedAt", "ProvisionedAt", "SuspendedAt",
               "DeprovisionedAt", "UpdatedAt", "LastFailureMessage", "FailureCount"
        FROM "TenantProvisioning"
        """;

    private static TenantProvisioningRecord MapTenant(TenantRow r) => new(
        r.TenantId,
        r.TenantCode,
        ProvisioningStatusTransitions.FromDbString(r.Status),
        r.PlanTier,
        r.UmbracoDbName,
        r.EnvFilePath,
        r.PrimaryDomain,
        r.CurrentRunId,
        r.LastSuccessfulRunId,
        r.OnboardingComplete,
        r.OnboardingCompletedAt is null ? null : new DateTimeOffset(r.OnboardingCompletedAt.Value, TimeSpan.Zero),
        new DateTimeOffset(r.RequestedAt, TimeSpan.Zero),
        r.RequestedBy,
        r.ProvisioningStartedAt is null ? null : new DateTimeOffset(r.ProvisioningStartedAt.Value, TimeSpan.Zero),
        r.ProvisionedAt is null ? null : new DateTimeOffset(r.ProvisionedAt.Value, TimeSpan.Zero),
        r.SuspendedAt is null ? null : new DateTimeOffset(r.SuspendedAt.Value, TimeSpan.Zero),
        r.DeprovisionedAt is null ? null : new DateTimeOffset(r.DeprovisionedAt.Value, TimeSpan.Zero),
        new DateTimeOffset(r.UpdatedAt, TimeSpan.Zero),
        r.LastFailureMessage,
        r.FailureCount);

    private static ProvisioningStepRecord MapStep(StepRow r) => new(
        r.Id, r.TenantId, r.RunId, r.StepOrder, r.StepName,
        ProvisioningStepStatusExtensions.FromDbString(r.Status),
        r.AttemptCount, r.MaxRetries, r.ErrorMessage,
        r.LastAttemptAt is null ? null : new DateTimeOffset(r.LastAttemptAt.Value, TimeSpan.Zero),
        r.CheckpointJson ?? "{}",
        r.RollbackStatus is null ? null : RollbackStatusExtensions.FromDbString(r.RollbackStatus),
        r.RollbackAttemptedAt is null ? null : new DateTimeOffset(r.RollbackAttemptedAt.Value, TimeSpan.Zero),
        r.RollbackErrorMessage,
        new DateTimeOffset(r.CreatedAt, TimeSpan.Zero),
        new DateTimeOffset(r.UpdatedAt, TimeSpan.Zero),
        r.CompletedAt is null ? null : new DateTimeOffset(r.CompletedAt.Value, TimeSpan.Zero));

    private sealed class TenantRow
    {
        public string TenantId { get; set; } = "";
        public string TenantCode { get; set; } = "";
        public string Status { get; set; } = "";
        public string PlanTier { get; set; } = "";
        public string? UmbracoDbName { get; set; }
        public string? EnvFilePath { get; set; }
        public string? PrimaryDomain { get; set; }
        public Guid? CurrentRunId { get; set; }
        public Guid? LastSuccessfulRunId { get; set; }
        public bool OnboardingComplete { get; set; }
        public DateTime? OnboardingCompletedAt { get; set; }
        public DateTime RequestedAt { get; set; }
        public string? RequestedBy { get; set; }
        public DateTime? ProvisioningStartedAt { get; set; }
        public DateTime? ProvisionedAt { get; set; }
        public DateTime? SuspendedAt { get; set; }
        public DateTime? DeprovisionedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? LastFailureMessage { get; set; }
        public int FailureCount { get; set; }
    }

    private sealed class StepRow
    {
        public long Id { get; set; }
        public string TenantId { get; set; } = "";
        public Guid RunId { get; set; }
        public int StepOrder { get; set; }
        public string StepName { get; set; } = "";
        public string Status { get; set; } = "";
        public int AttemptCount { get; set; }
        public int MaxRetries { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public string? CheckpointJson { get; set; }
        public string? RollbackStatus { get; set; }
        public DateTime? RollbackAttemptedAt { get; set; }
        public string? RollbackErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    private sealed class AuditRow
    {
        public long Id { get; set; }
        public string TenantId { get; set; } = "";
        public Guid? RunId { get; set; }
        public string Operation { get; set; } = "";
        public string? FromStatus { get; set; }
        public string? ToStatus { get; set; }
        public string? Actor { get; set; }
        public string? DetailsJson { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class OnboardingRow
    {
        public long Id { get; set; }
        public string TenantId { get; set; } = "";
        public string CheckItem { get; set; } = "";
        public string Status { get; set; } = "";
        public bool IsRequired { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string? VerifiedBy { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    private sealed class DomainRow
    {
        public string Domain { get; set; } = "";
        public string TenantId { get; set; } = "";
        public string StoreId { get; set; } = "";
        public bool IsPrimary { get; set; }
        public string Status { get; set; } = "";
        public string? RedisHostKey { get; set; }
        public string? RedisKeysWrittenJson { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? ActivatedAt { get; set; }
        public DateTime? RemovedAt { get; set; }
    }
}
