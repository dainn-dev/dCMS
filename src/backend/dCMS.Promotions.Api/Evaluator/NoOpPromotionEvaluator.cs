using dCMS.Promotions.Contracts.Evaluate;

namespace dCMS.Promotions.Api.Evaluator;

/// <summary>
/// Wire-up evaluator that returns no adjustments. Replaced by <see cref="PromotionEvaluator"/>
/// once mechanic strategies are registered (DAI-690+).
/// </summary>
public sealed class NoOpPromotionEvaluator : IPromotionEvaluator
{
    public Task<EvaluateResponse> EvaluateAsync(EvaluateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(EvaluateResponse.Empty);
}
