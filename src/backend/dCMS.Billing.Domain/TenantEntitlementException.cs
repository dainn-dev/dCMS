namespace dCMS.Billing.Domain;

public sealed class TenantEntitlementException : Exception
{
    public TenantEntitlementException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
