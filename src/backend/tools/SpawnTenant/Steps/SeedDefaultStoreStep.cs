using Npgsql;

namespace dCMS.Tools.SpawnTenant.Steps;

public sealed class SeedDefaultStoreStep : IProvisioningStep
{
    public string Name => ProvisioningStepNames.SeedDefaultStore;
    public int Order => 9;
    public int MaxRetries => 2;

    public async Task ExecuteAsync(ProvisioningContext ctx)
    {
        if (ctx.IsStepCompleted(Name))
            return;

        if (string.IsNullOrWhiteSpace(ctx.CatalogConnectionString))
            throw new InvalidOperationException("Catalog connection string is required.");

        await using var conn = new NpgsqlConnection(ctx.CatalogConnectionString);
        await conn.OpenAsync(ctx.CancellationToken).ConfigureAwait(false);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO "Branches" ("ClientId", "TenantId", "Name", "Address", "Lat", "Lng", "IsDefault", "IsActive")
            VALUES (@ClientId, @TenantId, @Name, '', 0, 0, TRUE, TRUE)
            ON CONFLICT ON CONSTRAINT "UQ_Branches_Client_Tenant" DO NOTHING
            """;
        cmd.Parameters.AddWithValue("ClientId", ctx.ClientId);
        cmd.Parameters.AddWithValue("TenantId", ctx.TenantId);
        cmd.Parameters.AddWithValue("Name", $"{ctx.TenantCode} default branch");
        var n = await cmd.ExecuteNonQueryAsync(ctx.CancellationToken).ConfigureAwait(false);
        ctx.StepArtifacts["branch_seeded"] = n >= 0;
        ctx.MarkStepCompleted(Name);
    }

    public async Task RollbackAsync(ProvisioningContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.CatalogConnectionString))
            return;

        await using var conn = new NpgsqlConnection(ctx.CatalogConnectionString);
        await conn.OpenAsync(ctx.CancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM "Branches" WHERE "ClientId" = @ClientId AND "TenantId" = @TenantId
            """;
        cmd.Parameters.AddWithValue("ClientId", ctx.ClientId);
        cmd.Parameters.AddWithValue("TenantId", ctx.TenantId);
        await cmd.ExecuteNonQueryAsync(ctx.CancellationToken).ConfigureAwait(false);
    }
}
