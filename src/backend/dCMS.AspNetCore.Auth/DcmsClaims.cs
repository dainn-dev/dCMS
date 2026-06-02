namespace dCMS.AspNetCore.Auth;

public static class DcmsClaims
{
    /// <summary>Top-level chain identifier (single static client per deployment). DAI-748.</summary>
    public const string ClientId = "client_id";

    public const string TenantId = "tenant_id";
    public const string StoreId = "store_id";

    /// <summary>DAI-752 (US-5) — set on impersonation tokens; carries the HQ user ID who started the session.</summary>
    public const string ImpersonatedBy = "impersonated_by";

    /// <summary>DAI-752 (US-5) — original role of the HQ user before impersonation (always ClientAdmin today).</summary>
    public const string OriginalRole = "original_role";

    /// <summary>JWT ID — used by the impersonation blacklist (Redis) to invalidate a token before its TTL.</summary>
    public const string Jti = "jti";

    /// <summary>Optional: comma-separated list of store IDs the user may operate on (tenant-scoped).</summary>
    [System.Obsolete("Will be removed once US-5 (Users module) lands. Prefer client_id + tenant_id scoping.")]
    public const string StoreIds = "store_ids";

    /// <summary>Optional: comma-separated list of brand IDs the user may operate on (tenant-scoped).</summary>
    [System.Obsolete("Will be removed once US-5 (Users module) lands. Prefer client_id + tenant_id scoping.")]
    public const string BrandIds = "brand_ids";
}
