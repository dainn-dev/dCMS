using Prometheus;

namespace dCMS.Order.Api.Routes;

/// <summary>
/// DAI-693 / DAI-679 — Prometheus counters for the Order ↔ Promotions integration.
/// Exposed via <c>/metrics</c> on the Order API (see <c>MapDcmsPrometheusMetrics</c>).
/// </summary>
internal static class OrderPromotionMetrics
{
    /// <summary>
    /// Number of orders that successfully had at least one promotion applied during creation.
    /// Labels: tenant (the dCMS supermarket / TenantId).
    /// </summary>
    public static readonly Counter OrdersPromotionsApplied = Metrics.CreateCounter(
        "dcms_orders_promotions_applied_total",
        "Number of orders that had at least one promotion applied at creation time.",
        new CounterConfiguration { LabelNames = ["tenant"] });

    /// <summary>
    /// Number of times the call to <c>POST /promotions/evaluate</c> from the Order API failed.
    /// Labels: tenant, mode = fail-open | fail-closed (driven by <c>Promotions:Required</c>).
    /// </summary>
    public static readonly Counter PromotionsEvaluateFailures = Metrics.CreateCounter(
        "dcms_promotions_evaluate_failures_total",
        "Number of times Order→Promotions /evaluate failed; mode reflects the Promotions:Required setting.",
        new CounterConfiguration { LabelNames = ["tenant", "mode"] });
}
