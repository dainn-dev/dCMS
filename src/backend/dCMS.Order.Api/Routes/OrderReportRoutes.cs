using System.Text.Json;
using dCMS.AspNetCore.Auth;
using dCMS.Order.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Order.Api.Routes;

public static class OrderReportRoutes
{
    private static readonly JsonSerializerOptions JsonCamel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static void MapOrderReportRoutes(this WebApplication app)
    {
        app.MapGet("/api/orders/reports/transactions/summary", GetTransactionSummary)
            .WithName("GetTransactionSummary")
            .WithTags("reports")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapGet("/api/orders/reports/transactions/details", GetTransactionDetails)
            .WithName("GetTransactionDetails")
            .WithTags("reports")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapGet("/api/orders/reports/transactions/ecommerce", GetEcommercePayments)
            .WithName("GetEcommercePayments")
            .WithTags("reports")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapGet("/api/orders/reports/sales/by-category", GetSalesByCategory)
            .WithName("GetSalesByCategory")
            .WithTags("reports")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapGet("/api/orders/reports/sales/by-product", GetSalesByProduct)
            .WithName("GetSalesByProduct")
            .WithTags("reports")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);

        app.MapGet("/api/orders/reports/sales/by-tenant", GetSalesByTenant)
            .WithName("GetSalesByTenant")
            .WithTags("reports")
            .RequireAuthorization(DcmsPolicies.OrderAccess)
            .WithTenantStoreHeaderAccess(app.Configuration);
    }

    private static IResult EnvelopeOk(object data, object? meta = null) =>
        Results.Json(new { data, meta, error = (object?)null }, JsonCamel);

    private static IResult EnvelopeError(int statusCode, string code, string message) =>
        Results.Json(
            new { data = (object?)null, meta = (object?)null, error = new { code, message } },
            JsonCamel,
            statusCode: statusCode);

    private static bool TryGetTenantStore(HttpContext http, out string tenantId, out string storeId)
    {
        tenantId = http.Request.Headers["X-Tenant-Id"].FirstOrDefault()?.Trim() ?? "";
        storeId = http.Request.Headers["X-Store-Id"].FirstOrDefault()?.Trim() ?? "";
        return tenantId.Length > 0;
    }

    private static bool TryParseDateRange(string? from, string? to, out DateOnly dateFrom, out DateOnly dateTo)
    {
        dateFrom = default;
        dateTo = default;
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            return false;
        return DateOnly.TryParse(from.Trim(), out dateFrom) && DateOnly.TryParse(to.Trim(), out dateTo) && dateFrom <= dateTo;
    }

    private static async Task<IResult> GetTransactionSummary(
        HttpContext http,
        [FromServices] OrderReportQueryStore reports,
        [FromQuery] string? dateFrom,
        [FromQuery] string? dateTo,
        CancellationToken ct)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return EnvelopeError(400, "MISSING_TENANT", "X-Tenant-Id header is required.");
        if (!TryParseDateRange(dateFrom, dateTo, out var df, out var dt))
            return EnvelopeError(400, "INVALID_DATE_RANGE", "dateFrom and dateTo query params are required (yyyy-MM-dd).");

        var rows = await reports.GetTransactionSummaryAsync(tenantId, storeId, df, dt, ct).ConfigureAwait(false);
        return EnvelopeOk(rows);
    }

    private static async Task<IResult> GetTransactionDetails(
        HttpContext http,
        [FromServices] OrderReportQueryStore reports,
        [FromQuery] string? dateFrom,
        [FromQuery] string? dateTo,
        [FromQuery] string? memberQuery,
        [FromQuery] string? brandCode,
        [FromQuery] string? paymentMethod,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return EnvelopeError(400, "MISSING_TENANT", "X-Tenant-Id header is required.");
        if (!TryParseDateRange(dateFrom, dateTo, out var df, out var dt))
            return EnvelopeError(400, "INVALID_DATE_RANGE", "dateFrom and dateTo query params are required (yyyy-MM-dd).");

        var lim = limit.HasValue ? Math.Clamp(limit.Value, 1, 100) : 50;
        var page = await reports.GetTransactionDetailsAsync(tenantId, storeId, df, dt, memberQuery, brandCode, paymentMethod, cursor, lim, ct)
            .ConfigureAwait(false);
        return EnvelopeOk(page.Items, new { nextCursor = page.NextCursor });
    }

    private static async Task<IResult> GetEcommercePayments(
        HttpContext http,
        [FromServices] OrderReportQueryStore reports,
        [FromQuery] string? dateFrom,
        [FromQuery] string? dateTo,
        [FromQuery] string? paymentMethod,
        CancellationToken ct)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return EnvelopeError(400, "MISSING_TENANT", "X-Tenant-Id header is required.");
        if (!TryParseDateRange(dateFrom, dateTo, out var df, out var dt))
            return EnvelopeError(400, "INVALID_DATE_RANGE", "dateFrom and dateTo query params are required (yyyy-MM-dd).");

        var rows = await reports.GetEcommercePaymentsAsync(tenantId, storeId, df, dt, paymentMethod, ct).ConfigureAwait(false);
        return EnvelopeOk(rows);
    }

    private static async Task<IResult> GetSalesByCategory(
        HttpContext http,
        [FromServices] OrderReportQueryStore reports,
        [FromQuery] string? dateFrom,
        [FromQuery] string? dateTo,
        [FromQuery] string? brandCode,
        CancellationToken ct)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return EnvelopeError(400, "MISSING_TENANT", "X-Tenant-Id header is required.");
        if (!TryParseDateRange(dateFrom, dateTo, out var df, out var dt))
            return EnvelopeError(400, "INVALID_DATE_RANGE", "dateFrom and dateTo query params are required (yyyy-MM-dd).");

        var rows = await reports.GetSalesByCategoryAsync(tenantId, storeId, df, dt, brandCode, ct).ConfigureAwait(false);
        return EnvelopeOk(rows);
    }

    private static async Task<IResult> GetSalesByProduct(
        HttpContext http,
        [FromServices] OrderReportQueryStore reports,
        [FromQuery] string? dateFrom,
        [FromQuery] string? dateTo,
        [FromQuery] string? brandCode,
        [FromQuery] string? categoryId,
        CancellationToken ct)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return EnvelopeError(400, "MISSING_TENANT", "X-Tenant-Id header is required.");
        if (!TryParseDateRange(dateFrom, dateTo, out var df, out var dt))
            return EnvelopeError(400, "INVALID_DATE_RANGE", "dateFrom and dateTo query params are required (yyyy-MM-dd).");

        var rows = await reports.GetSalesByProductAsync(tenantId, storeId, df, dt, brandCode, categoryId, ct).ConfigureAwait(false);
        return EnvelopeOk(rows);
    }

    private static async Task<IResult> GetSalesByTenant(
        HttpContext http,
        [FromServices] OrderReportQueryStore reports,
        [FromQuery] string? dateFrom,
        [FromQuery] string? dateTo,
        CancellationToken ct)
    {
        if (!TryGetTenantStore(http, out var tenantId, out _))
            return EnvelopeError(400, "MISSING_TENANT", "X-Tenant-Id header is required.");
        if (!TryParseDateRange(dateFrom, dateTo, out var df, out var dt))
            return EnvelopeError(400, "INVALID_DATE_RANGE", "dateFrom and dateTo query params are required (yyyy-MM-dd).");

        var rows = await reports.GetSalesByTenantAsync(df, dt, ct).ConfigureAwait(false);
        return EnvelopeOk(rows);
    }
}
