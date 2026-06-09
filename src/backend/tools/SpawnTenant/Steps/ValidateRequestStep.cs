using System.Text.RegularExpressions;
using dCMS.Provisioning.Domain;

namespace dCMS.Tools.SpawnTenant.Steps;

public sealed class ValidateRequestStep(ITenantProvisioningRepository repository) : IProvisioningStep
{
    private static readonly Regex CodeRegex = new(@"^[a-z0-9][a-z0-9_-]{0,18}$", RegexOptions.Compiled);

    public string Name => ProvisioningStepNames.ValidateRequest;
    public int Order => 1;
    public int MaxRetries => 0;

    public async Task ExecuteAsync(ProvisioningContext ctx)
    {
        if (ctx.IsStepCompleted(Name))
            return;

        if (string.IsNullOrWhiteSpace(ctx.TenantId) || string.IsNullOrWhiteSpace(ctx.TenantCode))
            throw new InvalidOperationException("TenantId and TenantCode are required.");

        if (!CodeRegex.IsMatch(ctx.TenantCode))
            throw new InvalidOperationException("TenantCode must be lowercase alphanumeric with optional _ or -.");

        var byCode = await repository.GetByTenantCodeAsync(ctx.TenantCode, ctx.CancellationToken).ConfigureAwait(false);
        if (byCode is not null && !string.Equals(byCode.TenantId, ctx.TenantId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"TenantCode '{ctx.TenantCode}' is already in use.");

        if (!string.IsNullOrWhiteSpace(ctx.PrimaryDomain) && !ctx.PrimaryDomain.Contains('.'))
            throw new InvalidOperationException("PrimaryDomain must be a valid hostname.");

        ctx.MarkStepCompleted(Name);
    }

    public Task RollbackAsync(ProvisioningContext ctx) => Task.CompletedTask;
}
