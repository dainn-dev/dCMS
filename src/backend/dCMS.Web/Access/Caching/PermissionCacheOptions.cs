namespace dCMS.Web.Access.Caching;

public sealed class PermissionCacheOptions
{
    public const string SectionName = "Dcms:Access:PermissionCache";

    /// <summary>Redis TTL for permission entries (default 5 minutes).</summary>
    public TimeSpan RedisTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Fallback in-memory TTL when Redis is unavailable (default 30 seconds).</summary>
    public TimeSpan MemoryTtl { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Prefix for all permission cache keys.</summary>
    public string KeyPrefix { get; set; } = "dcms:perm";

    /// <summary>
    /// In-memory TTL for the per-tenant <c>roles_version</c> read used to build cache keys.
    /// Keeps the hot path off Redis on every call. After a <c>BumpTenantVersionAsync</c>, callers may
    /// observe a stale version for at most this window — acceptable given the entry TTLs above.
    /// </summary>
    public TimeSpan VersionMemoryTtl { get; set; } = TimeSpan.FromSeconds(2);
}

