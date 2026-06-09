using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace dCMS.Reports.Api.Routes;

/// <summary>
/// DAI-711: delivery-slot utilization report (BRD §3). Reads the Fulfillment read-model directly
/// (cross-DB read, same precedent as Order.Api → Payment DB) because there is no slot-booking
/// analytics projection yet. Slot configuration lives in dcms_fulfillment; order↔slot bookings do
/// not exist in the model, so <c>No of Deliveries</c> is 0 until a booking projection is added.
/// </summary>
public sealed class FulfillmentReportQueryStore
{
    private readonly string? _fulfillmentCs;
    private readonly ILogger<FulfillmentReportQueryStore> _log;

    public FulfillmentReportQueryStore(IConfiguration configuration, ILogger<FulfillmentReportQueryStore> log)
    {
        // Optional: when the Fulfillment connstring is absent (e.g. minimal/CI compose) the endpoint
        // degrades to an empty result rather than 500-ing the whole report.
        _fulfillmentCs = configuration.GetConnectionString("Fulfillment");
        _log = log;
    }

    public async Task<IReadOnlyList<DeliverySlotRow>> GetDeliverySlotsAsync(
        string tenantId,
        DateOnly dateFrom,
        DateOnly dateTo,
        string? deliveryMode,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_fulfillmentCs))
        {
            _log.LogWarning("ConnectionStrings:Fulfillment not configured — delivery-slots report returns empty.");
            return Array.Empty<DeliverySlotRow>();
        }

        // BR03: group by Slot Name + First Day Of The Week + effective Delivery Mode.
        //   - First Day Of The Week = Monday of the slot's StartingDate week (date_trunc 'week').
        //   - No of Slots = count of configured slot rows in the group (best available capacity proxy).
        //   - No of Deliveries = 0: no order→slot booking linkage exists in the model yet (BR04 pending).
        //   - Effective mode = the slot's own Mode, falling back to its grouping's DeliveryMode.
        // BR02: deliveryMode is optional; empty/null = all modes.
        const string sql = """
            SELECT s."Name"                                              AS SlotName,
                   date_trunc('week', s."StartingDate")::date            AS FirstDayOfWeek,
                   COUNT(*)::int                                         AS NoOfSlots,
                   0                                                     AS NoOfDeliveries,
                   COALESCE(NULLIF(s."Mode", ''), g."DeliveryMode", '')  AS DeliveryMode
            FROM "FulfillmentSlots" s
            LEFT JOIN "FulfillmentGroupings" g
              ON g."Id" = s."GroupingId" AND g."TenantId" = s."TenantId"
            WHERE s."TenantId" = @TenantId
              AND s."StartingDate" >= @DateFrom
              AND s."StartingDate" <= @DateTo
              AND (
                    @DeliveryMode = ''
                 OR s."Mode" = @DeliveryMode
                 OR g."DeliveryMode" = @DeliveryMode
              )
            GROUP BY s."Name",
                     date_trunc('week', s."StartingDate"),
                     COALESCE(NULLIF(s."Mode", ''), g."DeliveryMode", '')
            ORDER BY FirstDayOfWeek DESC, s."Name"
            LIMIT 1000
            """;

        await using var conn = new NpgsqlConnection(_fulfillmentCs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<DeliverySlotRow>(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            // Dapper (this version) can't bind DateOnly; map to midnight-UTC DateTime against the date columns.
            DateFrom = dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            DateTo = dateTo.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            DeliveryMode = (deliveryMode ?? string.Empty).Trim(),
        }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    /// <summary>Matches the frontend JSON contract: { slotName, firstDayOfWeek, noOfSlots, noOfDeliveries, deliveryMode }.</summary>
    public sealed record DeliverySlotRow(
        string SlotName,
        DateTime FirstDayOfWeek,
        int NoOfSlots,
        int NoOfDeliveries,
        string DeliveryMode);
}
