using dCMS.Core.Models;

namespace dCMS.Promotions.Api.Evaluator;

/// <summary>
/// Strategy contract for one campaign EditorKind. Implementations mutate <paramref name="ctx"/>
/// in place to record line/order adjustments, suggestions, or issued-code promises.
/// </summary>
public interface IMechanicEvaluator
{
    /// <summary>Lowercase EditorKind handled by this strategy (e.g. "product-discount").</summary>
    string EditorKind { get; }

    void Evaluate(EvaluationContext ctx, CampaignRow campaign);
}
