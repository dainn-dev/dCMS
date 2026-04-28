namespace dCMS.Promotions.Api.Internal;

public sealed class InternalPromotionsOptions
{
    public const string SectionName = "InternalPromotions";

    /// <summary>When non-empty, <c>/internal/promotions/*</c> is enabled and requires header <c>X-Internal-Api-Key</c> matching this value.</summary>
    public string ApiKey { get; set; } = "";
}
