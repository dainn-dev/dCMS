namespace dCMS.Promotions.Contracts.Evaluate;

public sealed record Suggestion(
    string ProductId,
    string CampaignId,
    string Reason);
