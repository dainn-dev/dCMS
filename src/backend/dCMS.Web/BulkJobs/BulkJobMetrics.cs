using Prometheus;

namespace dCMS.Web.BulkJobs;

internal static class BulkJobMetrics
{
    public static readonly Counter Started = Metrics.CreateCounter(
        "dcms_bulk_jobs_started_total",
        "Bulk background jobs enqueued (Hangfire).",
        new CounterConfiguration { LabelNames = ["tenant", "kind"] });

    public static readonly Counter Completed = Metrics.CreateCounter(
        "dcms_bulk_jobs_completed_total",
        "Bulk background jobs completed.",
        new CounterConfiguration { LabelNames = ["tenant", "kind", "status"] });

    public static readonly Counter ProgressUpdates = Metrics.CreateCounter(
        "dcms_bulk_jobs_progress_updates_total",
        "Bulk job progress rows written to SQL metadata.",
        new CounterConfiguration { LabelNames = ["tenant", "kind"] });
}
