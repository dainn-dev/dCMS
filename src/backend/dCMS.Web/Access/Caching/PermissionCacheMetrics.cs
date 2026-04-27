using Prometheus;

namespace dCMS.Web.Access.Caching;

internal static class PermissionCacheMetrics
{
    public static readonly Counter Hits = Metrics.CreateCounter(
        "dcms_permissions_cache_hit_total",
        "Permission cache hits.",
        new CounterConfiguration { LabelNames = new[] { "level" } });

    public static readonly Counter Misses = Metrics.CreateCounter(
        "dcms_permissions_cache_miss_total",
        "Permission cache misses.");

    public static readonly Counter Errors = Metrics.CreateCounter(
        "dcms_permissions_cache_error_total",
        "Permission cache errors.",
        new CounterConfiguration { LabelNames = new[] { "op" } });

    public static readonly Histogram CheckDuration = Metrics.CreateHistogram(
        "dcms_permissions_check_duration_seconds",
        "PermissionService.HasPermissionAsync end-to-end duration.",
        new HistogramConfiguration
        {
            Buckets = new[] { 0.0001, 0.0005, 0.001, 0.002, 0.005, 0.01, 0.05, 0.1, 0.5 }
        });
}
