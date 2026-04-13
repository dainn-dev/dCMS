using System.Text.Json;
using dCMS.Order.Api.Contracts;
using dCMS.Order.Core.Domain;
using dCMS.Order.Core.Integration;
using dCMS.Order.Core.Ordering;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Order.Api.Routes;

public static class OrderHttpRoutes
{
    public static void MapOrderHttpRoutes(this WebApplication app) =>
        app.MapPost("/api/orders", CreateOrder)
            .WithName("CreateOrder")
            .WithTags("orders");

    private static async Task<IResult> CreateOrder(
        HttpContext http,
        [FromBody] CreateOrderApiRequest? body,
        [FromServices] IOrderService orders,
        CancellationToken cancellationToken)
    {
        if (body is null)
        {
            return Results.Json(
                new { error = new { code = "validation", message = "Request body is required." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var idempotencyKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Json(
                new { error = new { code = "MISSING_IDEMPOTENCY_KEY", message = "Idempotency-Key header is required." } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var tenantId = http.Request.Headers["X-Tenant-Id"].FirstOrDefault()?.Trim() ?? "";
        var storeId = http.Request.Headers["X-Store-Id"].FirstOrDefault()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(storeId))
        {
            return Results.Json(
                new
                {
                    error = new
                    {
                        code = "MISSING_TENANT_OR_STORE",
                        message = "X-Tenant-Id and X-Store-Id headers are required.",
                    },
                },
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TryBuildCommand(tenantId, storeId, idempotencyKey, body, out var command, out var errorMessage, out var errorCode))
        {
            return Results.Json(
                new { error = new { code = errorCode, message = errorMessage } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var result = await orders.CreateOrderAsync(command, cancellationToken).ConfigureAwait(false);
            var statusCode = result.IsIdempotentReplay ? StatusCodes.Status200OK : StatusCodes.Status201Created;
            return Results.Json(
                new
                {
                    orderId = result.Order.Id,
                    status = ToApiStatus(result.Order.Status),
                    paymentUrl = result.PaymentUrl,
                },
                statusCode: statusCode);
        }
        catch (OutOfStockException ex)
        {
            return Results.Json(
                new
                {
                    error = new
                    {
                        code = "OUT_OF_STOCK",
                        message = ex.Message,
                        variantId = ex.VariantId,
                        warehouseId = ex.WarehouseId,
                        requested = ex.Requested,
                        available = ex.Available,
                    },
                },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (PaymentInitException ex)
        {
            return Results.Json(
                new { error = new { code = "PAYMENT_INIT_FAILED", message = ex.Message } },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static bool TryBuildCommand(
        string tenantId,
        string storeId,
        string idempotencyKey,
        CreateOrderApiRequest body,
        out CreateOrderCommand command,
        out string errorMessage,
        out string errorCode)
    {
        errorCode = "validation";
        errorMessage = "";
        command = null!;

        if (string.IsNullOrWhiteSpace(body.CustomerId))
        {
            errorMessage = "customerId is required.";
            return false;
        }

        if (body.Lines is null || body.Lines.Count == 0)
        {
            errorMessage = "At least one line is required.";
            return false;
        }

        if (body.ShippingAddress is null)
        {
            errorMessage = "shippingAddress is required.";
            return false;
        }

        var ship = body.ShippingAddress;
        if (string.IsNullOrWhiteSpace(ship.Line1) || string.IsNullOrWhiteSpace(ship.City) ||
            string.IsNullOrWhiteSpace(ship.Region) || string.IsNullOrWhiteSpace(ship.PostalCode) ||
            string.IsNullOrWhiteSpace(ship.CountryCode))
        {
            errorMessage = "shippingAddress.line1, city, region, postalCode, and countryCode are required.";
            return false;
        }

        var lines = new List<CreateOrderLine>();
        foreach (var line in body.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.ProductId) || string.IsNullOrWhiteSpace(line.VariantId) ||
                string.IsNullOrWhiteSpace(line.WarehouseId) || line.UnitPrice is null)
            {
                errorMessage = "Each line requires productId, variantId, warehouseId, and unitPrice.";
                return false;
            }

            if (line.Quantity <= 0)
            {
                errorMessage = "Each line quantity must be positive.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(line.UnitPrice.Currency))
            {
                errorMessage = "unitPrice.currency is required.";
                return false;
            }

            var lineId = string.IsNullOrWhiteSpace(line.LineId) ? Guid.NewGuid().ToString("N") : line.LineId.Trim();
            var snap = VariantSnapshotJson(line.VariantSnapshot);
            var name = string.IsNullOrWhiteSpace(line.ProductNameSnapshot) ? line.ProductId.Trim() : line.ProductNameSnapshot.Trim();

            lines.Add(new CreateOrderLine(
                lineId,
                line.ProductId.Trim(),
                line.VariantId.Trim(),
                line.WarehouseId.Trim(),
                line.Quantity,
                new Money(line.UnitPrice.Amount, line.UnitPrice.Currency.Trim()),
                name,
                snap));
        }

        var shipping = new ShippingAddress(
            ship.Line1.Trim(),
            string.IsNullOrWhiteSpace(ship.Line2) ? null : ship.Line2.Trim(),
            ship.City.Trim(),
            ship.Region.Trim(),
            ship.PostalCode.Trim(),
            ship.CountryCode.Trim());

        var orderId = Guid.NewGuid().ToString();
        command = new CreateOrderCommand(
            orderId,
            tenantId,
            storeId,
            body.CustomerId.Trim(),
            idempotencyKey,
            lines,
            shipping,
            DateTimeOffset.UtcNow);

        return true;
    }

    private static string VariantSnapshotJson(JsonElement variantSnapshot) =>
        variantSnapshot.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => "{}",
            JsonValueKind.String => string.IsNullOrEmpty(variantSnapshot.GetString()) ? "{}" : variantSnapshot.GetString()!,
            _ => variantSnapshot.GetRawText(),
        };

    private static string ToApiStatus(OrderStatus s) =>
        s switch
        {
            OrderStatus.PaymentPending => "payment_pending",
            OrderStatus.Confirmed => "confirmed",
            OrderStatus.Shipped => "shipped",
            OrderStatus.Cancelled => "cancelled",
            _ => s.ToString().ToLowerInvariant(),
        };
}
