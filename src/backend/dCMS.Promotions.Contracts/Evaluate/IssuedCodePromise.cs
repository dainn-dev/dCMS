namespace dCMS.Promotions.Contracts.Evaluate;

public sealed record IssuedCodePromise(
    string CampaignId,
    int Count,
    string CodeType);
