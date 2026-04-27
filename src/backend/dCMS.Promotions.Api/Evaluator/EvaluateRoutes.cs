using dCMS.AspNetCore.Auth;
using dCMS.Promotions.Api.Http;
using dCMS.Promotions.Contracts.Evaluate;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Promotions.Api.Evaluator;

/// <summary>
/// DAI-679 / DAI-680: stateless rule-engine evaluator endpoint.
/// </summary>
public static class EvaluateRoutes
{
    public static void MapEvaluateRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();

        var g = app.MapGroup("/api/v1/tenants/{tenantId}/promotions")
            .WithTags("evaluate")
            .WithTenantAccess(configuration);

        var evaluate = g.MapPost("/evaluate", Evaluate);
        if (auth) evaluate.RequireAuthorization(DcmsPolicies.CatalogRead);
    }

    private static async Task<IResult> Evaluate(
        string tenantId,
        [FromBody] EvaluateRequest request,
        IPromotionEvaluator evaluator,
        EvaluateIdempotencyCache cache,
        CancellationToken ct)
    {
        if (request is null)
            return ApiEnvelope.Error("invalid_request", "Body is required.", StatusCodes.Status400BadRequest);
        if (!string.Equals(tenantId, request.TenantId, StringComparison.Ordinal))
            return ApiEnvelope.Error("tenant_mismatch", "Route tenantId does not match body.", StatusCodes.Status400BadRequest);
        if (string.IsNullOrWhiteSpace(request.Currency))
            return ApiEnvelope.Error("invalid_request", "Currency is required.", StatusCodes.Status400BadRequest);
        if (request.Lines is null)
            return ApiEnvelope.Error("invalid_request", "Lines is required.", StatusCodes.Status400BadRequest);

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var cached = await cache.TryGetAsync(tenantId, request.IdempotencyKey, ct);
            if (cached is not null) return ApiEnvelope.Ok(cached);
        }

        var response = await evaluator.EvaluateAsync(request, ct);

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            await cache.SetAsync(tenantId, request.IdempotencyKey, response, ct);

        return ApiEnvelope.Ok(response);
    }
}
