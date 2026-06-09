using System.Text.Json;
using dCMS.Billing.Domain;
using Microsoft.Data.SqlClient;
using StackExchange.Redis;

namespace dCMS.Tools.SpawnTenant.Steps;

public sealed class CreatePlatformTenantStep : IProvisioningStep
{
    public string Name => ProvisioningStepNames.CreatePlatformTenant;
    public int Order => 2;
    public int MaxRetries => 2;

    public async Task ExecuteAsync(ProvisioningContext ctx)
    {
        if (ctx.IsStepCompleted(Name))
            return;

        if (string.IsNullOrWhiteSpace(ctx.UmbracoPlatformConnectionString))
            throw new InvalidOperationException("Umbraco platform connection string is required.");

        var tenantId = ctx.TenantId;
        var code = ctx.TenantCode;
        var name = string.IsNullOrWhiteSpace(ctx.PlatformTenantName) ? ctx.TenantCode : ctx.PlatformTenantName;

        await using var conn = new SqlConnection(ctx.UmbracoPlatformConnectionString);
        await conn.OpenAsync(ctx.CancellationToken).ConfigureAwait(false);

        await using (var check = conn.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(1) FROM dcms_tenants WHERE id = @Id OR code = @Code";
            check.Parameters.AddWithValue("@Id", tenantId);
            check.Parameters.AddWithValue("@Code", code);
            var exists = (int)(await check.ExecuteScalarAsync(ctx.CancellationToken).ConfigureAwait(false) ?? 0);
            if (exists == 0)
            {
                await using var insert = conn.CreateCommand();
                insert.CommandText = """
                    INSERT INTO dcms_tenants (
                        id, code, name, contact_name, contact_email, brand_count, active,
                        provisioning_status, provisioning_run_id, plan_tier, created_at, updated_at)
                    VALUES (
                        @Id, @Code, @Name, '', '', 0, 1,
                        'provisioning', @RunId, @PlanTier, GETUTCDATE(), GETUTCDATE())
                    """;
                insert.Parameters.AddWithValue("@Id", tenantId);
                insert.Parameters.AddWithValue("@Code", code);
                insert.Parameters.AddWithValue("@Name", name);
                insert.Parameters.AddWithValue("@RunId", ctx.RunId.ToString("N"));
                insert.Parameters.AddWithValue("@PlanTier", ctx.PlanTier);
                await insert.ExecuteNonQueryAsync(ctx.CancellationToken).ConfigureAwait(false);
                ctx.StepArtifacts["platform_tenant_created"] = true;
            }
        }

        await using (var subCheck = conn.CreateCommand())
        {
            subCheck.CommandText = "SELECT COUNT(1) FROM dcms_tenant_subscriptions WHERE tenant_id = @Id";
            subCheck.Parameters.AddWithValue("@Id", tenantId);
            var hasSub = (int)(await subCheck.ExecuteScalarAsync(ctx.CancellationToken).ConfigureAwait(false) ?? 0) > 0;
            if (!hasSub)
            {
                var planCode = Enum.TryParse<PlanCode>(ctx.PlanTier, true, out var parsed)
                    ? parsed : PlanCode.Starter;

                await using var planCmd = conn.CreateCommand();
                planCmd.CommandText = "SELECT id FROM dcms_plans WHERE code = @Code AND is_active = 1";
                planCmd.Parameters.AddWithValue("@Code", planCode.ToPersistedValue());
                var planId = (string?)await planCmd.ExecuteScalarAsync(ctx.CancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Plan '{planCode}' not found in dcms_plans.");

                var trialEnds = DateTime.UtcNow.AddDays(Math.Max(1, ctx.DefaultTrialDays));
                await using var subInsert = conn.CreateCommand();
                subInsert.CommandText = """
                    INSERT INTO dcms_tenant_subscriptions (
                        tenant_id, plan_id, subscription_state, manual_invoice_status,
                        trial_ends_at, created_at, updated_at)
                    VALUES (@TenantId, @PlanId, @State, @Invoice, @TrialEnds, GETUTCDATE(), GETUTCDATE())
                    """;
                subInsert.Parameters.AddWithValue("@TenantId", tenantId);
                subInsert.Parameters.AddWithValue("@PlanId", planId);
                subInsert.Parameters.AddWithValue("@State", TenantSubscriptionState.Trial.ToPersistedValue());
                subInsert.Parameters.AddWithValue("@Invoice", ManualInvoiceStatus.None.ToPersistedValue());
                subInsert.Parameters.AddWithValue("@TrialEnds", trialEnds);
                await subInsert.ExecuteNonQueryAsync(ctx.CancellationToken).ConfigureAwait(false);
            }
        }

        await TryWarmEntitlementCacheAsync(ctx).ConfigureAwait(false);
        ctx.MarkStepCompleted(Name);
    }

    public async Task RollbackAsync(ProvisioningContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.UmbracoPlatformConnectionString))
            return;

        await using var conn = new SqlConnection(ctx.UmbracoPlatformConnectionString);
        await conn.OpenAsync(ctx.CancellationToken).ConfigureAwait(false);

        await using var deactivate = conn.CreateCommand();
        deactivate.CommandText = """
            UPDATE dcms_tenants SET active = 0, updated_at = GETUTCDATE() WHERE id = @Id;
            UPDATE dcms_tenant_subscriptions
            SET subscription_state = @Suspended, suspended_at = GETUTCDATE(), updated_at = GETUTCDATE()
            WHERE tenant_id = @Id;
            """;
        deactivate.Parameters.AddWithValue("@Id", ctx.TenantId);
        deactivate.Parameters.AddWithValue("@Suspended", TenantSubscriptionState.Suspended.ToPersistedValue());
        await deactivate.ExecuteNonQueryAsync(ctx.CancellationToken).ConfigureAwait(false);
    }

    private static async Task TryWarmEntitlementCacheAsync(ProvisioningContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.RedisConnectionString))
            return;

        try
        {
            var redis = await ConnectionMultiplexer.ConnectAsync(ctx.RedisConnectionString).ConfigureAwait(false);
            var db = redis.GetDatabase();
            var version = (long)await db.StringIncrementAsync($"dcms:tenant:entitlements:ver:{ctx.TenantId}").ConfigureAwait(false);
            var snapshot = new
            {
                tenantId = ctx.TenantId,
                planCode = ctx.PlanTier,
                subscriptionState = "trial",
                manualInvoiceStatus = "none",
                tenantActive = true,
                trialEndsAt = DateTimeOffset.UtcNow.AddDays(ctx.DefaultTrialDays),
                maxBrands = 2,
                maxActiveProducts = 500,
                features = new[] { "catalog.read", "orders.read" },
                version
            };
            var payload = JsonSerializer.Serialize(snapshot);
            await db.StringSetAsync($"dcms:tenant:entitlements:{ctx.TenantId}:v{version}", payload, TimeSpan.FromHours(24))
                .ConfigureAwait(false);
            await db.StringSetAsync($"dcms:tenant:plan:{ctx.TenantId}", ctx.PlanTier, TimeSpan.FromHours(24))
                .ConfigureAwait(false);
        }
        catch
        {
            /* entitlement warm is best-effort during CLI provision */
        }
    }
}
