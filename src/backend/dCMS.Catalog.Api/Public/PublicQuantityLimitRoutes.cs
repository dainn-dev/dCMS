using dCMS.Catalog.Api.Http;
using dCMS.Core.Persistence;
using dCMS.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Catalog.Api.Public;

/// <summary>Storefront quantity-limit validation and product limit lookup.</summary>
public static class PublicQuantityLimitRoutes
{
    public static void MapPublicQuantityLimitRoutes(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/quantity-limits")
            .WithTags("catalog-public-quantity-limits")
            .AllowAnonymous();

        g.MapPost("validate", ValidateCart);
        g.MapGet("products/{productId}", GetProductLimit);
    }

    private sealed record ValidateRequest(
        string? TenantId,
        string? StoreId,
        string? CustomerId,
        string? MembershipType,
        string? MembershipTier,
        List<ValidateLine>? Lines);

    private sealed record ValidateLine(string? ProductId, int Quantity);

    private static async Task<IResult> ValidateCart(
        [FromBody] ValidateRequest body,
        QuantityLimitValidationService validation,
        CancellationToken cancellationToken)
    {
        if (!TryScope(body.TenantId, body.StoreId, out var tenant, out var store, out var err))
            return err!;

        if (body.Lines is null || body.Lines.Count == 0)
            return ApiEnvelope.Error("validation_error", "At least one line is required.", StatusCodes.Status400BadRequest);

        var lines = new List<QuantityLimitCartLine>();
        foreach (var line in body.Lines)
        {
            var pid = (line.ProductId ?? "").Trim();
            if (pid.Length == 0 || line.Quantity <= 0)
                return ApiEnvelope.Error("validation_error", "Each line requires productId and positive quantity.",
                    StatusCodes.Status400BadRequest);
            lines.Add(new QuantityLimitCartLine(pid, line.Quantity));
        }

        var violations = await validation.ValidateCartAsync(
            tenant,
            store,
            lines,
            new QuantityLimitValidationContext(body.CustomerId?.Trim(), body.MembershipType?.Trim(), body.MembershipTier?.Trim()),
            cancellationToken).ConfigureAwait(false);

        return ApiEnvelope.Ok(new
        {
            valid = violations.Count == 0,
            violations = violations.Select(v => new
            {
                productId = v.ProductId,
                requested = v.Requested,
                limit = v.Limit,
                limitScope = v.LimitScope,
                ruleId = v.RuleId,
                ruleName = v.RuleName
            }).ToList()
        });
    }

    private static async Task<IResult> GetProductLimit(
        string productId,
        QuantityLimitValidationService validation,
        ICatalogPersistence catalog,
        string? tenantId,
        string? storeId,
        string? membershipType,
        string? membershipTier,
        CancellationToken cancellationToken)
    {
        if (!TryScope(tenantId, storeId, out var tenant, out var store, out var err))
            return err!;

        var product = await catalog.GetByIdAsync(productId, tenant, cancellationToken).ConfigureAwait(false);
        if (product is null || !string.Equals(product.StoreId, store, StringComparison.Ordinal))
            return ApiEnvelope.Error("not_found", "Product not found.", StatusCodes.Status404NotFound);

        var limit = await validation.ResolvePerCartLimitAsync(
            tenant, store, product, membershipType?.Trim(), membershipTier?.Trim(), cancellationToken)
            .ConfigureAwait(false);

        return ApiEnvelope.Ok(new
        {
            productId = product.Id,
            perCartLimit = limit,
            tenantId = tenant,
            storeId = store
        });
    }

    private static bool TryScope(string? tenantId, string? storeId, out string tenant, out string store, out IResult? error)
    {
        tenant = "";
        store = "";
        error = null;
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(storeId))
        {
            error = ApiEnvelope.Error("validation_error", "tenantId and storeId are required.",
                StatusCodes.Status400BadRequest);
            return false;
        }

        tenant = tenantId.Trim();
        store = storeId.Trim();
        return true;
    }
}
