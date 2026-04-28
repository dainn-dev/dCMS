using System.Text.Json;
using dCMS.Core.Persistence;
using dCMS.Promotions.Contracts.Evaluate;

namespace dCMS.Promotions.Api.Evaluator;

/// <summary>
/// DAI-692: validates a promo code against status, validity window, customer binding,
/// per-customer + total caps, group exclusivity, and exclusion list.
/// Uses Redis hot cache for the binding row; caps queried fresh from DB (authoritative).
/// </summary>
public sealed class DefaultPromoCodeResolver : PromoCodeResolver
{
    private readonly IPromoCodePersistence _codes;
    private readonly IPromoCodeRedemptionPersistence _redemptions;
    private readonly PromoCodeCache _cache;

    public DefaultPromoCodeResolver(
        IPromoCodePersistence codes,
        IPromoCodeRedemptionPersistence redemptions,
        PromoCodeCache cache)
    {
        _codes = codes;
        _redemptions = redemptions;
        _cache = cache;
    }

    public override async Task<PromoCodeResolveResult> ResolveAsync(
        EvaluateRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var code = request.PromoCode!.Trim();
        if (code.Length == 0)
            return Reject(code, RejectedCodeReasons.NotFound);

        var binding = await _cache.TryGetAsync(request.TenantId, code, cancellationToken)
            .ConfigureAwait(false);
        if (binding is null)
        {
            binding = await _codes.GetForResolutionAsync(request.TenantId, code, cancellationToken)
                .ConfigureAwait(false);
            if (binding is null) return Reject(code, RejectedCodeReasons.NotFound);
            await _cache.SetAsync(request.TenantId, code, binding, cancellationToken).ConfigureAwait(false);
        }

        // Workflow state — code must be in 'approved' (active) state.
        if (!string.Equals(binding.WorkflowState, "approved", StringComparison.OrdinalIgnoreCase))
            return Reject(code, RejectedCodeReasons.NotActive);

        // Validity window
        if (binding.StartDate is { } start && now < start) return Reject(code, RejectedCodeReasons.NotActive);
        if (binding.EndDate   is { } end   && now > end)   return Reject(code, RejectedCodeReasons.Expired);

        // Customer binding
        if (!string.IsNullOrEmpty(binding.CustomerId))
        {
            if (string.IsNullOrEmpty(request.CustomerId) ||
                !string.Equals(binding.CustomerId, request.CustomerId, StringComparison.Ordinal))
                return Reject(code, RejectedCodeReasons.NotForCustomer);
        }

        // Per-customer cap
        if (binding.MaxUsesPerCustomer is > 0 && !string.IsNullOrEmpty(request.CustomerId))
        {
            var used = await _redemptions
                .GetUsageCountByCustomerAsync(request.TenantId, binding.Id, request.CustomerId!, cancellationToken)
                .ConfigureAwait(false);
            if (used >= binding.MaxUsesPerCustomer.Value)
                return Reject(code, RejectedCodeReasons.CapExceeded);
        }

        // Total cap
        if (binding.MaxTotalUses is > 0)
        {
            var total = await _redemptions
                .GetTotalUsageAsync(request.TenantId, binding.Id, cancellationToken)
                .ConfigureAwait(false);
            if (total >= binding.MaxTotalUses.Value)
                return Reject(code, RejectedCodeReasons.CapExceeded);
        }

        // Group exclusivity
        if (!string.IsNullOrEmpty(binding.GroupId) && !string.IsNullOrEmpty(request.CustomerId))
        {
            var conflict = await _redemptions
                .HasGroupConflictAsync(request.TenantId, request.CustomerId!, binding.GroupId!, binding.Id, cancellationToken)
                .ConfigureAwait(false);
            if (conflict) return Reject(code, RejectedCodeReasons.GroupExclusionViolated);
        }

        var excluded = ParseExcluded(binding.ExcludedProductsJson);

        var ctx = new PromoCodeContext(
            PromoCodeId: binding.Id,
            Code: binding.Code,
            CampaignId: binding.CampaignId,
            ExcludedProductIds: excluded);

        return new PromoCodeResolveResult(ctx, null);
    }

    private static PromoCodeResolveResult Reject(string code, string reason) =>
        new(null, new RejectedCode(code, reason));

    private static IReadOnlySet<string>? ParseExcluded(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var el in doc.RootElement.EnumerateArray())
                if (el.ValueKind == JsonValueKind.String && el.GetString() is { Length: > 0 } s)
                    set.Add(s);
            return set.Count == 0 ? null : set;
        }
        catch (JsonException) { return null; }
    }
}
