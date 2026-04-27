namespace dCMS.Promotions.Contracts.Evaluate;

public sealed record RejectedCode(string Code, string Reason);

public static class RejectedCodeReasons
{
    public const string NotFound = "NotFound";
    public const string NotActive = "NotActive";
    public const string Expired = "Expired";
    public const string CapExceeded = "CapExceeded";
    public const string GroupExclusionViolated = "GroupExclusionViolated";
    public const string NotForCustomer = "NotForCustomer";
    public const string ExclusionList = "ExclusionList";
}
