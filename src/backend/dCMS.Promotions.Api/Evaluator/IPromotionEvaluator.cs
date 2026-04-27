using dCMS.Promotions.Contracts.Evaluate;

namespace dCMS.Promotions.Api.Evaluator;

public interface IPromotionEvaluator
{
    Task<EvaluateResponse> EvaluateAsync(EvaluateRequest request, CancellationToken cancellationToken = default);
}
