using Microsoft.Extensions.Logging;

namespace dCMS.Tools.SpawnTenant.Steps;

public sealed class HealthPollStep(ILogger<HealthPollStep> log) : IProvisioningStep
{
    public string Name => ProvisioningStepNames.HealthPoll;
    public int Order => 7;
    public int MaxRetries => 2;

    public async Task ExecuteAsync(ProvisioningContext ctx)
    {
        if (ctx.IsStepCompleted(Name))
            return;

        if (!ctx.ComposeUp || string.IsNullOrWhiteSpace(ctx.HealthUrl))
        {
            ctx.MarkStepCompleted(Name);
            return;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(5);
        while (DateTime.UtcNow < deadline)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            try
            {
                var resp = await http.GetAsync(ctx.HealthUrl, ctx.CancellationToken).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    ctx.MarkStepCompleted(Name);
                    return;
                }
                log.LogDebug("Health poll pending status {StatusCode}", (int)resp.StatusCode);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogDebug("Health poll attempt failed: {Reason}", ex.Message);
            }
            await Task.Delay(TimeSpan.FromSeconds(5), ctx.CancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Health poll timed out after 5 minutes.");
    }

    public Task RollbackAsync(ProvisioningContext ctx) => Task.CompletedTask;
}
