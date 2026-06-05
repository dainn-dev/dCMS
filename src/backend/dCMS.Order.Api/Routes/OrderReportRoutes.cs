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

        app.MapGet("/api/orders/reports/transactions/overview", GetTransactionsOverview)
            .WithName("GetTransactionsOverview")
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

    private static async Task<IResult> GetTransactionsOverview(
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

        var row = await reports.GetTransactionsOverviewAsync(tenantId, storeId, df, dt, ct).ConfigureAwait(false);
        return EnvelopeOk(row);
    }

    private static async Task<IResult> GetTransactionDetails(
        HttpContext http,
        [FromServices] OrderReportQueryStore reports,
        [FromQuery] string? dateFrom,
        [FromQuery] string? dateTo,
        [FromQuery] string? memberQuery,
        [FromQuery] string? brandCode,
        [FromQuery] string? paymentMethod,
        [FromQuery] string? receiptNumber,
        [FromQuery] string? source,
        [FromQuery] string? orderPromoCode,
        [FromQuery] string? itemPromoCode,
        [FromQuery] string? rebatesCode,
        [FromQuery] string? paymentType,
        [FromQuery] string? billingCountry,
        [FromQuery] string? membershipType,
        [FromQuery] string? membershipTier,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return EnvelopeError(400, "MISSING_TENANT", "X-Tenant-Id header is required.");
        if (!TryParseDateRange(dateFrom, dateTo, out var df, out var dt))
            return EnvelopeError(400, "INVALID_DATE_RANGE", "dateFrom and dateTo query params are required (yyyy-MM-dd).");

        // PR1: filters whose backing columns/services land in PR2/PR3 reject explicitly rather than silently drop.
        if (!string.IsNullOrWhiteSpace(receiptNumber))
            return EnvelopeError(400, "FILTER_NOT_AVAILABLE", "receiptNumber filter is available after PR2 (receipt sequence rollout).");
        if (!string.IsNullOrWhiteSpace(source))
            return EnvelopeError(400, "FILTER_NOT_AVAILABLE", "source filter is available after PR2 (storefront telemetry rollout).");
        if (!string.IsNullOrWhiteSpace(rebatesCode))
            return EnvelopeError(400, "FILTER_NOT_AVAILABLE", "rebatesCode filter is reserved; rebate domain not yet implemented.");
        if (!string.IsNullOrWhiteSpace(membershipType))
            return EnvelopeError(400, "FILTER_NOT_AVAILABLE", "membershipType filter is available after PR3 (Loyalty Membership domain rollout).");
        if (!string.IsNullOrWhiteSpace(membershipTier))
            return EnvelopeError(400, "FILTER_NOT_AVAILABLE", "membershipTier filter is available after PR3 (Loyalty Membership domain rollout).");
        // paymentMethod lives in the Payment DB (PaymentTransactions) which the details query does not join;
        // reject explicitly instead of silently returning all rows. Use paymentType for the in-Order composition.
        if (!string.IsNullOrWhiteSpace(paymentMethod))
            return EnvelopeError(400, "FILTER_NOT_AVAILABLE", "paymentMethod filter is not yet available; use paymentType instead.");

        var filter = new TransactionDetailFilter(
            MemberQuery: memberQuery,
            BrandCode: brandCode,
            PaymentMethod: paymentMethod,
            OrderPromoCode: orderPromoCode,
            ItemPromoCode: itemPromoCode,
            BillingCountry: billingCountry,
            PaymentType: paymentType);

        var lim = limit.HasValue ? Math.Clamp(limit.Value, 1, 100) : 50;
        var page = await reports.GetTransactionDetailsAsync(tenantId, storeId, df, dt, filter, cursor, lim, ct)
            .ConfigureAwait(false);
        return EnvelopeOk(page.Items, new { nextCursor = page.NextCursor });
    }

    private static async Task<IResult> GetEcommercePayments(
        HttpContext http,
        [FromServices] OrderReportQueryStore reports,
        [FromQuery] string? dateFrom,
        [FromQuery] string? dateTo,
        [FromQuery] string? orderNumber,
        [FromQuery] string? referenceNumber,
        [FromQuery] string? gatewayName,
        [FromQuery] string? paymentMethod,
        [FromQuery] string? paymentStatus,
        [FromQuery] decimal? amountMin,
        [FromQuery] decimal? amountMax,
        CancellationToken ct)
    {
        // Ecommerce Payments BRD §2 search criteria. Payment Date range is required.
        if (!TryGetTenantStore(http, out var tenantId, out var storeId))
            return EnvelopeError(400, "MISSING_TENANT", "X-Tenant-Id header is required.");
        if (!TryParseDateRange(dateFrom, dateTo, out var df, out var dt))
            return EnvelopeError(400, "INVALID_DATE_RANGE", "dateFrom and dateTo query params are required (yyyy-MM-dd).");

        var filter = new EcommercePaymentFilter(
            OrderNumber: orderNumber,
            ReferenceNumber: referenceNumber,
            GatewayName: gatewayName,
            PaymentMethod: paymentMethod,
            PaymentStatus: paymentStatus,
            AmountMin: amountMin,
            AmountMax: amountMax);

        var rows = await reports.GetEcommercePaymentsAsync(tenantId, storeId, df, dt, filter, ct).ConfigureAwait(false);
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
