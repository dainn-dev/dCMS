using System.Text.Json;
using dCMS.Provisioning.Domain;
using dCMS.Tools.SpawnTenant.Steps;
using Microsoft.Extensions.Logging;

namespace dCMS.Tools.SpawnTenant;

public sealed class ProvisioningOrchestrator : IProvisioningOrchestrator
{
    private readonly ITenantProvisioningRepository _repository;
    private readonly IReadOnlyList<IProvisioningStep> _steps;
    private readonly ILogger<ProvisioningOrchestrator> _log;

    public ProvisioningOrchestrator(
        ITenantProvisioningRepository repository,
        IEnumerable<IProvisioningStep> steps,
        ILogger<ProvisioningOrchestrator> log)
    {
        _repository = repository;
        _steps = steps.OrderBy(s => s.Order).ToList();
        _log = log;
    }

    public async Task<ProvisioningResult> ProvisionAsync(
        ProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid();
        var record = await _repository.GetByTenantIdAsync(request.TenantId, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            await _repository.CreateRequestedAsync(
                request.TenantId, request.TenantCode, request.PlanTier, request.Actor, cancellationToken)
                .ConfigureAwait(false);
            await _repository.SeedOnboardingChecklistAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
            record = await _repository.GetByTenantIdAsync(request.TenantId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Failed to create provisioning record.");
        }
        else if (record.Status is ProvisioningStatus.Active)
        {
            return new ProvisioningResult(false, request.TenantId, record.Status, record.CurrentRunId,
                "Tenant is already active.");
        }
        else if (record.Status is ProvisioningStatus.Failing or ProvisioningStatus.Rollback)
        {
            return new ProvisioningResult(false, request.TenantId, record.Status, record.CurrentRunId,
                "Tenant is in a failed state — use 'retry' or 'rollback'.");
        }

        if (!await TransitionAsync(record, ProvisioningStatus.Provisioning, runId, null, request, cancellationToken)
                .ConfigureAwait(false))
        {
            return new ProvisioningResult(false, request.TenantId, record.Status, runId,
                $"Cannot transition to provisioning from {record.Status}.");
        }

        var ctx = BuildContext(request, runId, cancellationToken);
        return await RunPipelineAsync(ctx, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProvisioningResult> RetryAsync(
        ProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = request.TenantId;
        var record = await _repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Provisioning record not found.");

        if (record.Status is not (ProvisioningStatus.Failing or ProvisioningStatus.Retrying))
        {
            return new ProvisioningResult(false, tenantId, record.Status, record.CurrentRunId,
                $"Retry is only allowed from failing/retrying (current: {record.Status}).");
        }

        var runId = record.CurrentRunId ?? Guid.NewGuid();
        await TransitionAsync(record, ProvisioningStatus.Retrying, runId, null, request, cancellationToken)
            .ConfigureAwait(false);

        record = (await _repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false))!;
        await TransitionAsync(record, ProvisioningStatus.Provisioning, runId, null, request, cancellationToken)
            .ConfigureAwait(false);

        var ctx = BuildContext(request with { }, runId, cancellationToken);
        return await RunPipelineAsync(ctx, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProvisioningResult> RollbackAsync(
        ProvisioningRequest request,
        bool force,
        CancellationToken cancellationToken = default)
    {
        var tenantId = request.TenantId;
        var record = await _repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Provisioning record not found.");

        if (!force && record.Status is ProvisioningStatus.Active)
        {
            return new ProvisioningResult(false, tenantId, record.Status, record.CurrentRunId,
                "Active tenants require --force to rollback.");
        }

        if (!ProvisioningStatusTransitions.CanTransition(record.Status, ProvisioningStatus.Rollback))
        {
            return new ProvisioningResult(false, tenantId, record.Status, record.CurrentRunId,
                $"Cannot rollback from {record.Status}.");
        }

        var runId = record.CurrentRunId ?? Guid.NewGuid();
        await TransitionAsync(record, ProvisioningStatus.Rollback, runId, null, request, cancellationToken)
            .ConfigureAwait(false);

        var ctx = BuildContext(request, runId, cancellationToken);
        var succeeded = runId != Guid.Empty
            ? await _repository.GetSucceededStepsForRunAsync(tenantId, runId, cancellationToken).ConfigureAwait(false)
            : [];

        foreach (var stepRecord in succeeded)
        {
            var step = _steps.FirstOrDefault(s => string.Equals(s.Name, stepRecord.StepName, StringComparison.OrdinalIgnoreCase));
            if (step is null)
                continue;

            try
            {
                await step.RollbackAsync(ctx).ConfigureAwait(false);
                await _repository.UpdateStepRollbackAsync(
                    stepRecord.Id, RollbackStatus.Succeeded, null, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _repository.UpdateStepRollbackAsync(
                    stepRecord.Id, RollbackStatus.Failed, ex.Message, cancellationToken).ConfigureAwait(false);
                record = (await _repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false))!;
                await TransitionAsync(record, ProvisioningStatus.Failing, runId, ex.Message, request, cancellationToken)
                    .ConfigureAwait(false);
                return new ProvisioningResult(false, tenantId, ProvisioningStatus.Failing, runId, ex.Message);
            }
        }

        record = (await _repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false))!;
        await TransitionAsync(record, ProvisioningStatus.Deprovisioned, runId, null, request, cancellationToken)
            .ConfigureAwait(false);

        return new ProvisioningResult(true, tenantId, ProvisioningStatus.Deprovisioned, runId, "Rollback completed.");
    }

    public async Task<ProvisioningResult> SuspendAsync(
        ProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = request.TenantId;
        var record = await _repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Provisioning record not found.");

        if (!await TransitionAsync(record, ProvisioningStatus.Suspended, record.CurrentRunId, null,
                request, cancellationToken).ConfigureAwait(false))
        {
            return new ProvisioningResult(false, tenantId, record.Status, record.CurrentRunId,
                $"Cannot suspend from {record.Status}.");
        }

        return new ProvisioningResult(true, tenantId, ProvisioningStatus.Suspended, record.CurrentRunId, "Suspended.");
    }

    public async Task<ProvisioningResult> ReactivateAsync(
        ProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = request.TenantId;
        var record = await _repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Provisioning record not found.");

        if (!await TransitionAsync(record, ProvisioningStatus.Active, record.CurrentRunId, null,
                request, cancellationToken).ConfigureAwait(false))
        {
            return new ProvisioningResult(false, tenantId, record.Status, record.CurrentRunId,
                $"Cannot reactivate from {record.Status}.");
        }

        return new ProvisioningResult(true, tenantId, ProvisioningStatus.Active, record.CurrentRunId, "Reactivated.");
    }

    private async Task<ProvisioningResult> RunPipelineAsync(
        ProvisioningContext ctx,
        CancellationToken cancellationToken)
    {
        var checkpoints = await _repository.LoadStepCheckpointsAsync(ctx.TenantId, ctx.RunId, cancellationToken)
            .ConfigureAwait(false);
        foreach (var (k, v) in checkpoints)
            ctx.Checkpoints[k] = v;

        foreach (var step in _steps)
        {
            if (ctx.IsStepCompleted(step.Name))
                continue;

            var attempt = 0;
            while (true)
            {
                attempt++;
                try
                {
                    _log.LogInformation(
                        "Provisioning step {Step} tenant {TenantId} run {RunId} attempt {Attempt}",
                        step.Name, ctx.TenantId, ctx.RunId, attempt);

                    await _repository.UpsertStepAsync(
                        ctx.TenantId, ctx.RunId, step.Order, step.Name,
                        ProvisioningStepStatus.Running, attempt, step.MaxRetries, null,
                        ProvisioningCheckpoint.CompletedJson(), cancellationToken).ConfigureAwait(false);

                    await step.ExecuteAsync(ctx).ConfigureAwait(false);

                    await _repository.UpsertStepAsync(
                        ctx.TenantId, ctx.RunId, step.Order, step.Name,
                        ProvisioningStepStatus.Succeeded, attempt, step.MaxRetries, null,
                        ProvisioningCheckpoint.CompletedJson(), cancellationToken).ConfigureAwait(false);

                    await _repository.AppendAuditAsync(
                        ctx.TenantId, ctx.RunId, "step_succeeded", null, null, ctx.Actor,
                        JsonSerializer.Serialize(new { step = step.Name }), cancellationToken).ConfigureAwait(false);

                    break;
                }
                catch (Exception ex) when (attempt <= step.MaxRetries)
                {
                    _log.LogWarning(ex, "Step {Step} failed attempt {Attempt}/{Max}", step.Name, attempt, step.MaxRetries);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt * 2, 10)), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await _repository.UpsertStepAsync(
                        ctx.TenantId, ctx.RunId, step.Order, step.Name,
                        ProvisioningStepStatus.Failed, attempt, step.MaxRetries, ex.Message,
                        ProvisioningCheckpoint.CompletedJson(), cancellationToken).ConfigureAwait(false);

                    var record = await _repository.GetByTenantIdAsync(ctx.TenantId, cancellationToken).ConfigureAwait(false)!;
                    await TransitionAsync(record!, ProvisioningStatus.Failing, ctx.RunId, ex.Message,
                        RequestFromContext(ctx), cancellationToken).ConfigureAwait(false);

                    return new ProvisioningResult(false, ctx.TenantId, ProvisioningStatus.Failing, ctx.RunId, ex.Message);
                }
            }
        }

        await _repository.UpdateInfrastructureAsync(
            ctx.TenantId, ctx.UmbracoDbName, ctx.EnvFilePath,
            string.IsNullOrWhiteSpace(ctx.PrimaryDomain) ? null : ctx.PrimaryDomain,
            ctx.RunId, ctx.RunId, cancellationToken).ConfigureAwait(false);

        var finalRecord = await _repository.GetByTenantIdAsync(ctx.TenantId, cancellationToken).ConfigureAwait(false)!;
        await TransitionAsync(finalRecord!, ProvisioningStatus.Active, ctx.RunId, null,
            RequestFromContext(ctx), cancellationToken).ConfigureAwait(false);

        _log.LogInformation("Provisioning complete tenant {TenantId} run {RunId}", ctx.TenantId, ctx.RunId);
        return new ProvisioningResult(true, ctx.TenantId, ProvisioningStatus.Active, ctx.RunId, "Provisioning succeeded.");
    }

    private async Task<bool> TransitionAsync(
        TenantProvisioningRecord record,
        ProvisioningStatus to,
        Guid? runId,
        string? failureMessage,
        ProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        var ok = await _repository.TransitionStatusAsync(
            record.TenantId, record.Status, to, runId, failureMessage, request.Actor, cancellationToken)
            .ConfigureAwait(false);

        if (ok)
        {
            await PlatformTenantSync.SyncProvisioningStatusAsync(
                request.UmbracoPlatformConnectionString,
                record.TenantId, to, runId, record.PlanTier, cancellationToken).ConfigureAwait(false);
        }

        return ok;
    }

    private static ProvisioningRequest RequestFromContext(ProvisioningContext ctx) => new(
        ctx.TenantId,
        ctx.TenantCode,
        ctx.PlanTier,
        ctx.SqlServerSaConnectionString,
        ctx.CatalogConnectionString,
        ctx.UmbracoPlatformConnectionString,
        ctx.RedisConnectionString,
        ctx.AdminEmail,
        ctx.AdminPassword,
        ctx.PrimaryDomain,
        ctx.DefaultStoreId,
        ctx.EnvFilePath,
        ctx.ComposeProject,
        ctx.Actor,
        ctx.ComposeUp,
        ctx.HealthUrl,
        ctx.DefaultTrialDays);

    private static ProvisioningContext BuildContext(ProvisioningRequest request, Guid runId, CancellationToken cancellationToken = default) => new()
    {
        TenantId = request.TenantId,
        TenantCode = request.TenantCode,
        PlanTier = request.PlanTier,
        RunId = runId,
        SqlServerSaConnectionString = request.SqlServerSaConnectionString,
        UmbracoPlatformConnectionString = request.UmbracoPlatformConnectionString,
        CatalogConnectionString = request.CatalogConnectionString,
        RedisConnectionString = request.RedisConnectionString,
        AdminEmail = request.AdminEmail,
        AdminPassword = request.AdminPassword,
        PrimaryDomain = request.PrimaryDomain ?? "",
        DefaultStoreId = request.DefaultStoreId,
        EnvFilePath = request.EnvFilePath,
        ComposeProject = request.ComposeProject,
        ComposeUp = request.ComposeUp,
        HealthUrl = request.HealthUrl,
        DefaultTrialDays = request.DefaultTrialDays,
        PlatformTenantName = request.TenantCode,
        Actor = request.Actor,
        CancellationToken = cancellationToken
    };
}
