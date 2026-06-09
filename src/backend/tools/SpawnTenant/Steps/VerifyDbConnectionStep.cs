using Microsoft.Data.SqlClient;

namespace dCMS.Tools.SpawnTenant.Steps;

public sealed class VerifyDbConnectionStep : IProvisioningStep
{
    public string Name => ProvisioningStepNames.VerifyDbConnection;
    public int Order => 5;
    public int MaxRetries => 3;

    public async Task ExecuteAsync(ProvisioningContext ctx)
    {
        if (ctx.IsStepCompleted(Name))
            return;

        var connStr = WriteEnvFileStep.BuildTenantConnectionString(ctx.SqlServerSaConnectionString, ctx.UmbracoDbName);
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ctx.CancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1;";
        _ = await cmd.ExecuteScalarAsync(ctx.CancellationToken).ConfigureAwait(false);
        ctx.MarkStepCompleted(Name);
    }

    public Task RollbackAsync(ProvisioningContext ctx) => Task.CompletedTask;
}
