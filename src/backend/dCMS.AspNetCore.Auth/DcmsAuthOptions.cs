namespace dCMS.AspNetCore.Auth;

public sealed class DcmsAuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>When false, JWT and tenant filters are not applied (local dev / tests).</summary>
    public bool Enabled { get; set; } = true;

    public string JwtSigningKey { get; set; } = "";
    public string Issuer { get; set; } = "dcms";
    public string Audience { get; set; } = "dcms-api";
}
