using dCMS.Provisioning.Domain;

namespace dCMS.Tools.SpawnTenant.Steps;

public sealed class MarkOnboardingPendingStep(ITenantProvisioningRepository repository) : IProvisioningStep
{
    public string Name => ProvisioningStepNames.MarkOnboardingPending;
    public int Order => 10;
    public int MaxRetries => 0;

    public async Task ExecuteAsync(ProvisioningContext ctx)
    {
        if (ctx.IsStepCompleted(Name))
            return;

        await repository.SeedOnboardingChecklistAsync(ctx.TenantId, ctx.CancellationToken).ConfigureAwait(false);
        ctx.MarkStepCompleted(Name);
    }

    public Task RollbackAsync(ProvisioningContext ctx) => Task.CompletedTask;
}
