using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace dCMS.Tools.SpawnTenant.Steps;

public sealed class ComposeUpStep(ILogger<ComposeUpStep> log) : IProvisioningStep
{
    public string Name => ProvisioningStepNames.ComposeUp;
    public int Order => 6;
    public int MaxRetries => 1;

    public Task ExecuteAsync(ProvisioningContext ctx)
    {
        if (ctx.IsStepCompleted(Name))
            return Task.CompletedTask;

        if (!ctx.ComposeUp)
        {
            ctx.MarkStepCompleted(Name);
            return Task.CompletedTask;
        }

        var rc = RunComposeUp(ctx.EnvFilePath, ctx.TenantId);
        if (rc != 0)
            throw new InvalidOperationException($"docker compose exited with code {rc}.");

        ctx.StepArtifacts["compose_started"] = true;
        ctx.MarkStepCompleted(Name);
        return Task.CompletedTask;
    }

    public Task RollbackAsync(ProvisioningContext ctx)
    {
        if (!ctx.ComposeUp || ctx.StepArtifacts.TryGetValue("compose_started", out var started) && started is not true)
            return Task.CompletedTask;

        var psi = new ProcessStartInfo("docker", $"compose --env-file \"{ctx.EnvFilePath}\" down")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        var p = Process.Start(psi);
        p?.WaitForExit();
        return Task.CompletedTask;
    }

    private int RunComposeUp(string envFile, string tenant)
    {
        var psi = new ProcessStartInfo("docker", $"compose --env-file \"{envFile}\" up -d dcms-web")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        var p = Process.Start(psi)!;
        p.WaitForExit();
        if (p.ExitCode != 0)
            log.LogWarning("compose up failed tenant {TenantId} exit {ExitCode}", tenant, p.ExitCode);
        return p.ExitCode;
    }
}
