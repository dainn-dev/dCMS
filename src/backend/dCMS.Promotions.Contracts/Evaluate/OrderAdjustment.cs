namespace dCMS.Promotions.Contracts.Evaluate;

public sealed record OrderAdjustment(
    string CampaignId,
    decimal Amount,
    string Reason);
