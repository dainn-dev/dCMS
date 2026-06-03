using dCMS.Core.Exceptions;
using dCMS.Core.Models;
using dCMS.Core.Persistence;

namespace dCMS.Core.Services;

public sealed record QuantityLimitCartLine(string ProductId, int Quantity);

public sealed record QuantityLimitValidationContext(
    string? CustomerId,
    string? MembershipType,
    string? MembershipTier);

public sealed record QuantityLimitViolation(
    string ProductId,
    int Requested,
    int Limit,
    string LimitScope,
    string? RuleId,
    string? RuleName);

/// <summary>Resolves store quantity limits and validates cart lines (general + advance rules).</summary>
public sealed class QuantityLimitValidationService(
    IStoreQuantityLimitPersistence limits,
    ICatalogPersistence catalog,
    ICustomerOrderQuantityQuery? customerOrders = null)
{
    public async Task<IReadOnlyList<QuantityLimitViolation>> ValidateCartAsync(
        string tenantId,
        string storeId,
        IReadOnlyList<QuantityLimitCartLine> lines,
        QuantityLimitValidationContext context,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
            return Array.Empty<QuantityLimitViolation>();

        var general = await limits.GetGeneralAsync(tenantId, storeId, cancellationToken).ConfigureAwait(false);
        var rules = await limits.ListRulesAsync(tenantId, storeId, cancellationToken).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeRules = rules.Where(r => IsRuleActive(r, today)).ToList();

        var byProduct = lines
            .GroupBy(l => l.ProductId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity), StringComparer.Ordinal);

        var products = new Dictionary<string, Product>(StringComparer.Ordinal);
        foreach (var productId in byProduct.Keys)
        {
            var product = await catalog.GetByIdAsync(productId, tenantId, cancellationToken).ConfigureAwait(false);
            if (product is not null && string.Equals(product.StoreId, storeId, StringComparison.Ordinal))
                products[productId] = product;
        }

        var violations = new List<QuantityLimitViolation>();

        foreach (var (productId, qty) in byProduct)
        {
            if (qty > general.CartLimitPerProduct)
            {
                violations.Add(new QuantityLimitViolation(
                    productId, qty, general.CartLimitPerProduct, "per_cart", null, "Default cart limit"));
            }
        }

        foreach (var rule in activeRules.Where(r => r.LimitType == "per_cart"))
        {
            if (rule.PerProduct)
            {
                foreach (var (productId, qty) in byProduct)
                {
                    if (!products.TryGetValue(productId, out var product)) continue;
                    if (!RuleMatches(rule, product, context.MembershipType, context.MembershipTier)) continue;
                    if (qty > rule.QuantityLimit)
                        violations.Add(new QuantityLimitViolation(productId, qty, rule.QuantityLimit, "per_cart", rule.Id, rule.Name));
                }
            }
            else
            {
                var total = 0;
                foreach (var (productId, qty) in byProduct)
                {
                    if (!products.TryGetValue(productId, out var product)) continue;
                    if (RuleMatches(rule, product, context.MembershipType, context.MembershipTier))
                        total += qty;
                }

                if (total > rule.QuantityLimit)
                {
                    violations.Add(new QuantityLimitViolation(
                        "*", total, rule.QuantityLimit, "per_cart", rule.Id, rule.Name));
                }
            }
        }

        if (string.IsNullOrWhiteSpace(context.CustomerId) || customerOrders is null)
            return violations;

        foreach (var rule in activeRules.Where(r => r.LimitType == "per_user"))
        {
            if (rule.PerProduct)
            {
                foreach (var (productId, qty) in byProduct)
                {
                    if (!products.TryGetValue(productId, out var product)) continue;
                    if (!RuleMatches(rule, product, context.MembershipType, context.MembershipTier)) continue;

                    var purchased = await customerOrders
                        .GetPurchasedQuantityAsync(tenantId, storeId, context.CustomerId!, productId,
                            rule.StartDate, rule.EndDate, cancellationToken)
                        .ConfigureAwait(false);
                    var total = purchased + qty;
                    if (total > rule.QuantityLimit)
                    {
                        violations.Add(new QuantityLimitViolation(
                            productId, total, rule.QuantityLimit, "per_user", rule.Id, rule.Name));
                    }
                }
            }
            else
            {
                var cartQty = 0;
                foreach (var (productId, qty) in byProduct)
                {
                    if (!products.TryGetValue(productId, out var product)) continue;
                    if (!RuleMatches(rule, product, context.MembershipType, context.MembershipTier)) continue;
                    cartQty += qty;
                }

                if (cartQty == 0) continue;

                var purchasedTotal = 0;
                foreach (var (productId, _) in byProduct)
                {
                    if (!products.TryGetValue(productId, out var product)) continue;
                    if (!RuleMatches(rule, product, context.MembershipType, context.MembershipTier)) continue;
                    purchasedTotal += await customerOrders
                        .GetPurchasedQuantityAsync(tenantId, storeId, context.CustomerId!, productId,
                            rule.StartDate, rule.EndDate, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (purchasedTotal + cartQty > rule.QuantityLimit)
                {
                    violations.Add(new QuantityLimitViolation(
                        "*", purchasedTotal + cartQty, rule.QuantityLimit, "per_user", rule.Id, rule.Name));
                }
            }
        }

        return violations;
    }

    public async Task EnsureCartValidAsync(
        string tenantId,
        string storeId,
        IReadOnlyList<QuantityLimitCartLine> lines,
        QuantityLimitValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var violations = await ValidateCartAsync(tenantId, storeId, lines, context, cancellationToken)
            .ConfigureAwait(false);
        var first = violations.FirstOrDefault();
        if (first is null) return;

        throw new QuantityLimitExceededException(
            first.ProductId,
            first.Requested,
            first.Limit,
            first.LimitScope,
            first.RuleId,
            first.RuleName);
    }

    public async Task<int> ResolvePerCartLimitAsync(
        string tenantId,
        string storeId,
        Product product,
        string? membershipType,
        string? membershipTier,
        CancellationToken cancellationToken = default)
    {
        var general = await limits.GetGeneralAsync(tenantId, storeId, cancellationToken).ConfigureAwait(false);
        var rules = await limits.ListRulesAsync(tenantId, storeId, cancellationToken).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var limit = general.CartLimitPerProduct;
        foreach (var rule in rules.Where(r => r.LimitType == "per_cart" && IsRuleActive(r, today)))
        {
            if (RuleMatches(rule, product, membershipType, membershipTier))
                limit = Math.Min(limit, rule.QuantityLimit);
        }

        return limit;
    }

    private static bool IsRuleActive(StoreQuantityLimitRuleRow rule, DateOnly today)
    {
        if (today < rule.StartDate) return false;
        if (rule.EndDate.HasValue && today > rule.EndDate.Value) return false;
        return true;
    }

    private static bool RuleMatches(
        StoreQuantityLimitRuleRow rule,
        Product product,
        string? membershipType,
        string? membershipTier)
    {
        if (!string.IsNullOrWhiteSpace(rule.ProductId) &&
            !string.Equals(rule.ProductId, product.Id, StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrWhiteSpace(rule.BrandId) &&
            !string.Equals(rule.BrandId, product.BrandId, StringComparison.Ordinal))
            return false;

        if (rule.CategoryIds.Length > 0 && !rule.CategoryIds.Contains(product.CategoryId))
            return false;

        if (!string.IsNullOrWhiteSpace(rule.MembershipType))
        {
            if (string.IsNullOrWhiteSpace(membershipType) ||
                !string.Equals(rule.MembershipType, membershipType, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.MembershipTier))
        {
            if (string.IsNullOrWhiteSpace(membershipTier) ||
                !string.Equals(rule.MembershipTier, membershipTier, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
