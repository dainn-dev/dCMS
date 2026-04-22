namespace dCMS.Gateway;

/// <summary>Gateway-specific auth options — extends base DcmsAuthOptions with token TTL.</summary>
public sealed class GatewayAuthOptions
{
    public const string SectionName = "Auth";

    public bool   Enabled                { get; set; } = true;
    public string JwtSigningKey          { get; set; } = "";
    public string Issuer                 { get; set; } = "dcms-gateway";
    public string Audience               { get; set; } = "dcms-services";
    /// <summary>TTL of the minted internal JWT forwarded to upstream services (seconds).</summary>
    public int    InternalTokenTtlSeconds { get; set; } = 300;
}
