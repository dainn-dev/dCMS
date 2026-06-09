namespace dCMS.Gateway;

public sealed class GatewayHostRoutingOptions
{
    public const string SectionName = "HostRouting";

    public bool Enabled { get; set; } = true;

    /// <summary>When true, unknown host on /storefront/v1 returns 404.</summary>
    public bool FailClosedOnStorefront { get; set; } = true;

    /// <summary>Hosts that bypass fail-closed (platform default ingress).</summary>
    public string[] PlatformHosts { get; set; } = ["localhost", "127.0.0.1"];
}
