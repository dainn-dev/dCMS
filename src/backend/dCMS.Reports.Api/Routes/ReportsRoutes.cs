using System.Text.Json;
using dCMS.AspNetCore.Auth;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Reports.Api.Routes;

public static class ReportsRoutes
{
    private static readonly JsonSerializerOptions JsonCamel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static void MapReportsRoutes(this WebApplication app)
    {
        app.MapGet("/api/v1/reports/sales", GetSales)
            .WithName("GetSales")
            .WithTags("reports")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapGet("/api/v1/reports/abandon-cart", GetAbandonCart)
            .WithName("GetAbandonCart")
            .WithTags("reports")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapGet("/api/v1/reports/restock-subscriptions", GetRestockSubscriptions)
            .WithName("GetRestockSubscriptions")
            .WithTags("reports")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapGet("/api/v1/reports/delivery-slots", GetDeliverySlots)
            .WithName("GetDeliverySlots")
            .WithTags("reports")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);
    }

    private static IResult Ok(object data, object? meta = null) =>
        Results.Json(new { data, meta, error = (object?)null }, JsonCamel);

    private static IResult Err(int statusCode, string code, string message) =>
        Results.Json(new { data = (object?)null, meta = (object?)null, error = new { code, message } }, JsonCamel, statusCode: statusCode);

    private static bool TryTenantStore(HttpContext http, out string tenantId, out string storeId)
    {
        tenantId = http.Request.Headers["X-Tenant-Id"].FirstOrDefault()?.Trim() ?? "";
        storeId = http.Request.Headers["X-Store-Id"].FirstOrDefault()?.Trim() ?? "";
        return tenantId.Length > 0;
    }

    private static bool TryDateRange(string? from, string? to, out DateOnly df, out DateOnly dt)
    {
        df = default;
        dt = default;
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            return false;
        return DateOnly.TryParse(from.Trim(), out df) && DateOnly.TryParse(to.Trim(), out dt) && df <= dt;
    }

    private static async Task<IResult> GetSales(
        HttpContext http,
        [FromServices] AnalyticsReportQueryStore store,
        [FromQuery] string? groupBy,
        [FromQuery] string? dateFrom,
        [FromQuery] string? dateTo,
        CancellationToken ct)
    {
        if (!TryTenantStore(http, out var tenantId, out var storeId))
            return Err(400, "MISSING_TENANT", "X-Tenant-Id header is required.");
        if (!TryDateRange(dateFrom, dateTo, out var df, out var dt))
            return Err(400, "INVALID_DATE_RANGE", "dateFrom and dateTo are required (yyyy-MM-dd).");
        var gb = (groupBy ?? "store").Trim().ToLowerInvariant();

        try
        {
            var rows = await store.GetSalesAsync(tenantId, storeId, df, dt, gb, ct).ConfigureAwait(false);
            return Ok(rows);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Err(400, "INVALID_GROUP_BY", ex.Message);
        }
    }

    private static async Task<IResult> GetAbandonCart(
        HttpContext http,
        [FromServices] AnalyticsReportQueryStore store,
        [FromQuery] string? dateFrom,
        [FromQuery] string? dateTo,
        CancellationToken ct)
    {
        if (!TryTenantStore(http, out var tenantId, out var storeId))
            return Err(400, "MISSING_TENANT", "X-Tenant-Id header is required.");
        if (!TryDateRange(dateFrom, dateTo, out var df, out var dt))
            return Err(400, "INVALID_DATE_RANGE", "dateFrom and dateTo are required (yyyy-MM-dd).");

        var rows = await store.GetAbandonCartAsync(tenantId, storeId, df, dt, ct).ConfigureAwait(false);
        return Ok(rows);
    }

    private static async Task<IResult> GetRestockSubscriptions(
        HttpContext http,
        [FromServices] AnalyticsReportQueryStore store,
        CancellationToken ct)
    {
        if (!TryTenantStore(http, out var tenantId, out var storeId))
            return Err(400, "MISSING_TENANT", "X-Tenant-Id header is required.");

        var rows = await store.GetRestockSubscriptionsAsync(tenantId, storeId, ct).ConfigureAwait(false);
        return Ok(rows);
    }

    private static Task<IResult> GetDeliverySlots(
        HttpContext http,
        [FromQuery] string? dateFrom,
        [FromQuery] string? dateTo,
        CancellationToken ct)
    {
        // DAI-711: Will be backed by analytics/projection of fulfillment slots + bookings.
        if (!TryTenantStore(http, out _, out _))
            return Task.FromResult<IResult>(Err(400, "MISSING_TENANT", "X-Tenant-Id header is required."));
        if (!TryDateRange(dateFrom, dateTo, out _, out _))
            return Task.FromResult<IResult>(Err(400, "INVALID_DATE_RANGE", "dateFrom and dateTo are required (yyyy-MM-dd)."));
        return Task.FromResult<IResult>(Ok(Array.Empty<object>()));
    }
}

