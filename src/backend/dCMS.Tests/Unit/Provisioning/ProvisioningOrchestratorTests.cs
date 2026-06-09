using dCMS.Provisioning.Domain;
using dCMS.Tools.SpawnTenant;
using dCMS.Tools.SpawnTenant.Steps;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace dCMS.Tests.Unit.Provisioning;

public sealed class ProvisioningOrchestratorTests
{
    [Fact]
    public async Task ProvisionAsync_skips_completed_steps_on_checkpoint()
    {
        var repo = new FakeProvisioningRepository();
        var executed = new List<string>();
        var steps = new IProvisioningStep[]
        {
            new FakeStep("validate_request", 1, executed),
            new FakeStep("create_platform_tenant", 2, executed),
        };

        var orchestrator = new ProvisioningOrchestrator(repo, steps, NullLogger<ProvisioningOrchestrator>.Instance);
        var request = MinimalRequest("t-test");

        var first = await orchestrator.ProvisionAsync(request);
        first.Success.Should().BeTrue();
        executed.Should().HaveCount(2);

        executed.Clear();
        repo.ForcedStatus = ProvisioningStatus.Requested;
        var second = await orchestrator.ProvisionAsync(request);
        second.Success.Should().BeFalse();
        executed.Should().BeEmpty("completed steps must be skipped when re-entering from active guard");
    }

    [Fact]
    public async Task RetryAsync_transitions_from_failing_to_active()
    {
        var repo = new FakeProvisioningRepository { ForcedStatus = ProvisioningStatus.Failing };
        repo.SeedRecord("t-retry", "retry", ProvisioningStatus.Failing);
        var executed = new List<string>();
        var steps = new IProvisioningStep[] { new FakeStep("validate_request", 1, executed) };
        var orchestrator = new ProvisioningOrchestrator(repo, steps, NullLogger<ProvisioningOrchestrator>.Instance);

        var result = await orchestrator.RetryAsync(MinimalRequest("t-retry"));
        result.Success.Should().BeTrue();
        result.Status.Should().Be(ProvisioningStatus.Active);
    }

    private static ProvisioningRequest MinimalRequest(string tenantId) => new(
        tenantId, "test", "starter", "", "fake-catalog", "", "", "", "",
        null, "default", "infra/tenants/test.env", "dcms-test", "test", false, null);

    private sealed class FakeStep(string name, int order, List<string> executed) : IProvisioningStep
    {
        public string Name => name;
        public int Order => order;
        public int MaxRetries => 0;

        public Task ExecuteAsync(ProvisioningContext ctx)
        {
            if (ctx.IsStepCompleted(Name))
                return Task.CompletedTask;
            executed.Add(Name);
            ctx.MarkStepCompleted(Name);
            return Task.CompletedTask;
        }

        public Task RollbackAsync(ProvisioningContext ctx) => Task.CompletedTask;
    }

    private sealed class FakeProvisioningRepository : ITenantProvisioningRepository
    {
        private readonly Dictionary<string, TenantProvisioningRecord> _records = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<ProvisioningStepRecord>> _steps = new(StringComparer.OrdinalIgnoreCase);
        public ProvisioningStatus ForcedStatus { get; set; } = ProvisioningStatus.Requested;

        public void SeedRecord(string tenantId, string code, ProvisioningStatus status)
        {
            _records[tenantId] = new TenantProvisioningRecord(
                tenantId, code, status, "starter", null, null, null, Guid.NewGuid(), null,
                false, null, DateTimeOffset.UtcNow, "test", null, null, null, null,
                DateTimeOffset.UtcNow, null, 0);
        }

        public Task<TenantProvisioningRecord?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (_records.TryGetValue(tenantId, out var r))
                return Task.FromResult<TenantProvisioningRecord?>(r with { Status = ForcedStatus == ProvisioningStatus.Requested ? r.Status : ForcedStatus });
            return Task.FromResult<TenantProvisioningRecord?>(null);
        }

        public Task<TenantProvisioningRecord?> GetByTenantCodeAsync(string tenantCode, CancellationToken cancellationToken = default)
            => Task.FromResult<TenantProvisioningRecord?>(null);

        public Task CreateRequestedAsync(string tenantId, string tenantCode, string planTier, string? requestedBy, CancellationToken cancellationToken = default)
        {
            SeedRecord(tenantId, tenantCode, ProvisioningStatus.Requested);
            return Task.CompletedTask;
        }

        public Task<bool> TransitionStatusAsync(string tenantId, ProvisioningStatus from, ProvisioningStatus to, Guid? runId, string? failureMessage, string? actor, CancellationToken cancellationToken = default)
        {
            if (!_records.TryGetValue(tenantId, out var r))
                return Task.FromResult(false);
            if (r.Status != from)
                return Task.FromResult(false);
            _records[tenantId] = r with { Status = to, CurrentRunId = runId ?? r.CurrentRunId };
            ForcedStatus = to;
            return Task.FromResult(true);
        }

        public Task UpdateInfrastructureAsync(string tenantId, string? umbracoDbName, string? envFilePath, string? primaryDomain, Guid? currentRunId, Guid? lastSuccessfulRunId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SetOnboardingCompleteAsync(string tenantId, bool complete, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ProvisioningStepRecord>> GetStepsAsync(string tenantId, Guid runId, CancellationToken cancellationToken = default)
        {
            var key = $"{tenantId}:{runId:N}";
            return Task.FromResult<IReadOnlyList<ProvisioningStepRecord>>(_steps.TryGetValue(key, out var list) ? list : []);
        }

        public Task<IReadOnlyList<ProvisioningStepRecord>> GetSucceededStepsForRunAsync(string tenantId, Guid runId, CancellationToken cancellationToken = default)
            => GetStepsAsync(tenantId, runId, cancellationToken);

        public Task UpsertStepAsync(string tenantId, Guid runId, int stepOrder, string stepName, ProvisioningStepStatus status, int attemptCount, int maxRetries, string? errorMessage, string checkpointJson, CancellationToken cancellationToken = default)
        {
            var key = $"{tenantId}:{runId:N}";
            if (!_steps.TryGetValue(key, out var list))
            {
                list = [];
                _steps[key] = list;
            }
            list.RemoveAll(s => s.StepOrder == stepOrder);
            list.Add(new ProvisioningStepRecord(
                list.Count + 1, tenantId, runId, stepOrder, stepName, status, attemptCount, maxRetries,
                errorMessage, null, ProvisioningCheckpoint.CompletedJson(), null, null, null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, status == ProvisioningStepStatus.Succeeded ? DateTimeOffset.UtcNow : null));
            return Task.CompletedTask;
        }

        public Task UpdateStepRollbackAsync(long stepId, RollbackStatus rollbackStatus, string? rollbackErrorMessage, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Dictionary<string, string>> LoadStepCheckpointsAsync(string tenantId, Guid runId, CancellationToken cancellationToken = default)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (_steps.TryGetValue($"{tenantId}:{runId:N}", out var list))
            {
                foreach (var s in list.Where(x => x.Status == ProvisioningStepStatus.Succeeded))
                    map[s.StepName] = "completed";
            }
            return Task.FromResult(map);
        }

        public Task AppendAuditAsync(string tenantId, Guid? runId, string operation, string? fromStatus, string? toStatus, string? actor, string detailsJson, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ProvisioningAuditEntry>> ListAuditAsync(string tenantId, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProvisioningAuditEntry>>([]);

        public Task SeedOnboardingChecklistAsync(string tenantId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<TenantOnboardingItem>> ListOnboardingAsync(string tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TenantOnboardingItem>>([]);

        public Task UpsertDomainBindingAsync(string domain, string tenantId, string storeId, bool isPrimary, DomainBindingStatus status, string? redisHostKey, string? redisKeysWrittenJson, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<TenantDomainBindingRecord>> ListDomainBindingsAsync(string tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TenantDomainBindingRecord>>([]);
    }
}
