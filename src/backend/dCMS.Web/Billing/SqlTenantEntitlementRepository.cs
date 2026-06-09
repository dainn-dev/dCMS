using System.Text.Json;
using dCMS.Billing.Domain;
using Umbraco.Cms.Infrastructure.Persistence;

namespace dCMS.Web.Billing;

public sealed class SqlTenantEntitlementRepository(IUmbracoDatabaseFactory dbFactory) : ITenantEntitlementRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<TenantSubscriptionRecord?> GetByTenantIdAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        using var db = dbFactory.CreateDatabase();
        var row = await db.SingleOrDefaultAsync<SubscriptionJoinRow>("""
            SELECT s.tenant_id AS TenantId,
                   s.plan_id AS PlanId,
                   p.code AS PlanCode,
                   p.name AS PlanName,
                   p.max_brands AS MaxBrands,
                   p.max_active_products AS MaxActiveProducts,
                   p.features_json AS FeaturesJson,
                   s.subscription_state AS SubscriptionState,
                   s.manual_invoice_status AS ManualInvoiceStatus,
                   t.active AS TenantActive,
                   s.trial_ends_at AS TrialEndsAt,
                   s.current_period_start AS CurrentPeriodStart,
                   s.current_period_end AS CurrentPeriodEnd,
                   s.pending_plan_id AS PendingPlanId,
                   s.suspended_at AS SuspendedAt,
                   s.cancelled_at AS CancelledAt,
                   s.cancellation_reason AS CancellationReason,
                   s.invoice_reference AS InvoiceReference,
                   s.invoice_notes AS InvoiceNotes,
                   s.updated_at AS UpdatedAt
            FROM dcms_tenant_subscriptions s
            INNER JOIN dcms_plans p ON p.id = s.plan_id
            INNER JOIN dcms_tenants t ON t.id = s.tenant_id
            WHERE s.tenant_id = @0
            """, tenantId).ConfigureAwait(false);

        return row is null ? null : MapRow(row);
    }

    public async Task<IReadOnlyList<PlanDefinition>> ListPlansAsync(CancellationToken cancellationToken = default)
    {
        using var db = dbFactory.CreateDatabase();
        var rows = await db.FetchAsync<PlanRow>("""
            SELECT id AS Id, code AS Code, name AS Name, max_brands AS MaxBrands,
                   max_active_products AS MaxActiveProducts, features_json AS FeaturesJson, is_active AS IsActive
            FROM dcms_plans
            WHERE is_active = 1
            ORDER BY max_brands
            """).ConfigureAwait(false);

        return rows.Select(MapPlan).ToList();
    }

    public async Task<PlanDefinition?> GetPlanByIdAsync(string planId, CancellationToken cancellationToken = default)
    {
        using var db = dbFactory.CreateDatabase();
        var row = await db.SingleOrDefaultAsync<PlanRow>("""
            SELECT id AS Id, code AS Code, name AS Name, max_brands AS MaxBrands,
                   max_active_products AS MaxActiveProducts, features_json AS FeaturesJson, is_active AS IsActive
            FROM dcms_plans
            WHERE id = @0
            """, planId).ConfigureAwait(false);

        return row is null ? null : MapPlan(row);
    }

    public async Task<PlanDefinition?> GetPlanByCodeAsync(PlanCode code, CancellationToken cancellationToken = default)
    {
        using var db = dbFactory.CreateDatabase();
        var row = await db.SingleOrDefaultAsync<PlanRow>("""
            SELECT id AS Id, code AS Code, name AS Name, max_brands AS MaxBrands,
                   max_active_products AS MaxActiveProducts, features_json AS FeaturesJson, is_active AS IsActive
            FROM dcms_plans
            WHERE code = @0
            """, code.ToPersistedValue()).ConfigureAwait(false);

        return row is null ? null : MapPlan(row);
    }

    public async Task CreateDefaultTrialSubscriptionAsync(
        string tenantId,
        PlanCode planCode,
        int trialDays,
        CancellationToken cancellationToken = default)
    {
        var plan = await GetPlanByCodeAsync(planCode, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Plan '{planCode.ToPersistedValue()}' was not found.");

        var trialEnds = DateTime.UtcNow.AddDays(Math.Max(1, trialDays));
        using var db = dbFactory.CreateDatabase();
        await db.ExecuteAsync("""
            INSERT INTO dcms_tenant_subscriptions (
                tenant_id, plan_id, subscription_state, manual_invoice_status,
                trial_ends_at, created_at, updated_at)
            VALUES (@0, @1, @2, @3, @4, GETUTCDATE(), GETUTCDATE())
            """,
            tenantId,
            plan.Id,
            TenantSubscriptionState.Trial.ToPersistedValue(),
            ManualInvoiceStatus.None.ToPersistedValue(),
            trialEnds).ConfigureAwait(false);
    }

    public async Task UpdateManualInvoiceAsync(
        string tenantId,
        ManualInvoiceStatus status,
        string? invoiceReference,
        string? invoiceNotes,
        CancellationToken cancellationToken = default)
    {
        using var db = dbFactory.CreateDatabase();
        var n = await db.ExecuteAsync("""
            UPDATE dcms_tenant_subscriptions
            SET manual_invoice_status = @0,
                invoice_reference = @1,
                invoice_notes = @2,
                updated_at = GETUTCDATE()
            WHERE tenant_id = @3
            """,
            status.ToPersistedValue(),
            (invoiceReference ?? "").Trim(),
            (invoiceNotes ?? "").Trim(),
            tenantId).ConfigureAwait(false);

        if (n == 0)
            throw new InvalidOperationException("Tenant subscription not found.");
    }

    public async Task ActivateAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        using var db = dbFactory.CreateDatabase();
        var n = await db.ExecuteAsync("""
            UPDATE dcms_tenant_subscriptions
            SET subscription_state = @0,
                suspended_at = NULL,
                cancelled_at = NULL,
                cancellation_reason = '',
                current_period_start = COALESCE(current_period_start, GETUTCDATE()),
                updated_at = GETUTCDATE()
            WHERE tenant_id = @1
            """,
            TenantSubscriptionState.Active.ToPersistedValue(),
            tenantId).ConfigureAwait(false);

        if (n == 0)
            throw new InvalidOperationException("Tenant subscription not found.");
    }

    public async Task SuspendAsync(string tenantId, string? reason, CancellationToken cancellationToken = default)
    {
        using var db = dbFactory.CreateDatabase();
        var n = await db.ExecuteAsync("""
            UPDATE dcms_tenant_subscriptions
            SET subscription_state = @0,
                suspended_at = GETUTCDATE(),
                cancellation_reason = @1,
                updated_at = GETUTCDATE()
            WHERE tenant_id = @2
            """,
            TenantSubscriptionState.Suspended.ToPersistedValue(),
            (reason ?? "").Trim(),
            tenantId).ConfigureAwait(false);

        if (n == 0)
            throw new InvalidOperationException("Tenant subscription not found.");
    }

    public async Task CancelAsync(string tenantId, string? reason, CancellationToken cancellationToken = default)
    {
        using var db = dbFactory.CreateDatabase();
        var n = await db.ExecuteAsync("""
            UPDATE dcms_tenant_subscriptions
            SET subscription_state = @0,
                cancelled_at = GETUTCDATE(),
                cancellation_reason = @1,
                updated_at = GETUTCDATE()
            WHERE tenant_id = @2
            """,
            TenantSubscriptionState.Cancelled.ToPersistedValue(),
            (reason ?? "").Trim(),
            tenantId).ConfigureAwait(false);

        if (n == 0)
            throw new InvalidOperationException("Tenant subscription not found.");
    }

    public async Task ChangePlanAsync(
        string tenantId,
        string planId,
        string? pendingPlanId,
        CancellationToken cancellationToken = default)
    {
        using var db = dbFactory.CreateDatabase();
        var n = await db.ExecuteAsync("""
            UPDATE dcms_tenant_subscriptions
            SET plan_id = @0,
                pending_plan_id = @1,
                updated_at = GETUTCDATE()
            WHERE tenant_id = @2
            """,
            planId,
            string.IsNullOrWhiteSpace(pendingPlanId) ? null : pendingPlanId.Trim(),
            tenantId).ConfigureAwait(false);

        if (n == 0)
            throw new InvalidOperationException("Tenant subscription not found.");
    }

    private static TenantSubscriptionRecord MapRow(SubscriptionJoinRow row) =>
        new()
        {
            TenantId = row.TenantId,
            PlanId = row.PlanId,
            PlanCode = PlanCodeExtensions.ParsePersisted(row.PlanCode),
            PlanName = row.PlanName,
            MaxBrands = row.MaxBrands,
            MaxActiveProducts = row.MaxActiveProducts,
            Features = ParseFeatures(row.FeaturesJson),
            SubscriptionState = TenantSubscriptionStateExtensions.ParsePersisted(row.SubscriptionState),
            ManualInvoiceStatus = ManualInvoiceStatusExtensions.ParsePersisted(row.ManualInvoiceStatus),
            TenantActive = row.TenantActive,
            TrialEndsAt = ToOffset(row.TrialEndsAt),
            CurrentPeriodStart = ToOffset(row.CurrentPeriodStart),
            CurrentPeriodEnd = ToOffset(row.CurrentPeriodEnd),
            PendingPlanId = row.PendingPlanId,
            SuspendedAt = ToOffset(row.SuspendedAt),
            CancelledAt = ToOffset(row.CancelledAt),
            CancellationReason = row.CancellationReason,
            InvoiceReference = row.InvoiceReference,
            InvoiceNotes = row.InvoiceNotes,
            UpdatedAt = ToOffset(row.UpdatedAt) ?? DateTimeOffset.UtcNow,
        };

    private static PlanDefinition MapPlan(PlanRow row) =>
        new()
        {
            Id = row.Id,
            Code = PlanCodeExtensions.ParsePersisted(row.Code),
            Name = row.Name,
            MaxBrands = row.MaxBrands,
            MaxActiveProducts = row.MaxActiveProducts,
            Features = ParseFeatures(row.FeaturesJson),
            IsActive = row.IsActive,
        };

    private static IReadOnlyList<string> ParseFeatures(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<string[]>(json, Json) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private sealed class PlanRow
    {
        public string Id { get; set; } = null!;
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int MaxBrands { get; set; }
        public int MaxActiveProducts { get; set; }
        public string FeaturesJson { get; set; } = "[]";
        public bool IsActive { get; set; }
    }

    private sealed class SubscriptionJoinRow
    {
        public string TenantId { get; set; } = null!;
        public string PlanId { get; set; } = null!;
        public string PlanCode { get; set; } = null!;
        public string PlanName { get; set; } = null!;
        public int MaxBrands { get; set; }
        public int MaxActiveProducts { get; set; }
        public string FeaturesJson { get; set; } = "[]";
        public string SubscriptionState { get; set; } = null!;
        public string ManualInvoiceStatus { get; set; } = null!;
        public bool TenantActive { get; set; }
        public DateTime? TrialEndsAt { get; set; }
        public DateTime? CurrentPeriodStart { get; set; }
        public DateTime? CurrentPeriodEnd { get; set; }
        public string? PendingPlanId { get; set; }
        public DateTime? SuspendedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string CancellationReason { get; set; } = "";
        public string InvoiceReference { get; set; } = "";
        public string InvoiceNotes { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
    }
}
