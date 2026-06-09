using Microsoft.Data.SqlClient;

namespace dCMS.Tools.SpawnTenant.Steps;

public sealed class CreateUmbracoDbStep : IProvisioningStep
{
    public string Name => ProvisioningStepNames.CreateUmbracoDb;
    public int Order => 3;
    public int MaxRetries => 2;

    public async Task ExecuteAsync(ProvisioningContext ctx)
    {
        if (ctx.IsStepCompleted(Name))
            return;

        var dbName = ctx.UmbracoDbName;
        await using var conn = new SqlConnection(ctx.SqlServerSaConnectionString);
        await conn.OpenAsync(ctx.CancellationToken).ConfigureAwait(false);

        await using var check = conn.CreateCommand();
        check.CommandText = "SELECT DB_ID(@DbName)";
        check.Parameters.AddWithValue("@DbName", dbName);
        var existing = await check.ExecuteScalarAsync(ctx.CancellationToken).ConfigureAwait(false);
        var created = existing is null or DBNull;

        if (created)
        {
            await using var create = conn.CreateCommand();
            create.CommandText = $"IF DB_ID('{dbName}') IS NULL CREATE DATABASE [{dbName}];";
            await create.ExecuteNonQueryAsync(ctx.CancellationToken).ConfigureAwait(false);
            ctx.StepArtifacts["umbraco_db_created"] = true;
        }

        ctx.StepArtifacts["umbraco_db_name"] = dbName;
        ctx.MarkStepCompleted(Name);
    }

    public async Task RollbackAsync(ProvisioningContext ctx)
    {
        if (ctx.StepArtifacts.TryGetValue("umbraco_db_created", out var created) && created is not true)
            return;

        var dbName = ctx.UmbracoDbName;
        await using var conn = new SqlConnection(ctx.SqlServerSaConnectionString);
        await conn.OpenAsync(ctx.CancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            IF DB_ID('{dbName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{dbName}];
            END
            """;
        await cmd.ExecuteNonQueryAsync(ctx.CancellationToken).ConfigureAwait(false);
    }
}
