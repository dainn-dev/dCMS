namespace dCMS.Promotions.Contracts.Evaluate;

public sealed record LineAdjustment(
    string LineId,
    string CampaignId,
    decimal Amount,
    string Reason);
