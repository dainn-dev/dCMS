using Microsoft.Data.SqlClient;
using dCMS.Provisioning.Domain;

namespace dCMS.Tools.SpawnTenant;

public static class PlatformTenantSync
{
    public static async Task SyncProvisioningStatusAsync(
        string connectionString,
        string tenantId,
        ProvisioningStatus status,
        Guid? runId,
        string planTier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE dcms_tenants
            SET provisioning_status = @Status,
                provisioning_run_id = @RunId,
                plan_tier = @PlanTier,
                updated_at = GETUTCDATE()
            WHERE id = @TenantId
            """;
        cmd.Parameters.AddWithValue("@Status", status.ToDbString());
        cmd.Parameters.AddWithValue("@RunId", (object?)runId?.ToString("N") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PlanTier", planTier);
        cmd.Parameters.AddWithValue("@TenantId", tenantId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
