using dCMS.Core.Exceptions;
using dCMS.Inventory.Api.Http;
using dCMS.Inventory.Commands;
using dCMS.Inventory.Exceptions;
using dCMS.Inventory.Persistence;
using dCMS.Inventory.Services;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Inventory.Api.Internal;

public static class InternalInventoryRoutes
{
    public static void MapInternalInventoryRoutes(this WebApplication app)
    {
        var g = app.MapGroup("/internal/inventory")
            .WithTags("inventory-internal")
            .AddEndpointFilter<InternalInventoryApiKeyEndpointFilter>()
            .DisableRateLimiting();

        g.MapPost("/check", InternalCheck).AllowAnonymous();
        g.MapPost("/reserve", InternalReserve).AllowAnonymous();
        g.MapPost("/release", InternalRelease).AllowAnonymous();
    }

    private sealed record InternalCheckBody(string? TenantId, string? StoreId, string? VariantId, string? WarehouseId, int Quantity);

    private sealed record InternalMutationBody(string? TenantId, string? StoreId, string? VariantId, string? WarehouseId, int Quantity,
        string? CreatedBy, string? ReferenceId);

    private static async Task<IResult> InternalCheck(
        [FromBody] InternalCheckBody? body,
        IInventoryStockPersistence persistence,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return ApiEnvelope.Error("validation", "Request body is required.", StatusCodes.Status400BadRequest);

        var tenantId = (body.TenantId ?? "").Trim();
        var storeId = (body.StoreId ?? "").Trim();
        var variantId = (body.VariantId ?? "").Trim();
        var warehouseId = (body.WarehouseId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(storeId) ||
            string.IsNullOrWhiteSpace(variantId) || string.IsNullOrWhiteSpace(warehouseId))
        {
            return ApiEnvelope.Error("validation", "tenantId, storeId, variantId and warehouseId are required.",
                StatusCodes.Status400BadRequest);
        }

        if (body.Quantity < 0)
            return ApiEnvelope.Error("validation", "quantity must be zero or positive.", StatusCodes.Status400BadRequest);

        var stock = await persistence.GetStockAsync(tenantId, storeId, variantId, warehouseId, cancellationToken)
            .ConfigureAwait(false);
        var available = stock?.AvailableQuantity ?? 0;
        var found = stock is not null;
        var sufficient = found && available >= body.Quantity;

        return ApiEnvelope.Ok(new
        {
            tenantId,
            storeId,
            variantId,
            warehouseId,
            requested = body.Quantity,
            available,
            found,
            sufficient
        });
    }

    private static async Task<IResult> InternalReserve(
        [FromBody] InternalMutationBody? body,
        StockService stock,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return ApiEnvelope.Error("validation", "Request body is required.", StatusCodes.Status400BadRequest);

        var tenantId = (body.TenantId ?? "").Trim();
        var storeId = (body.StoreId ?? "").Trim();
        var variantId = (body.VariantId ?? "").Trim();
        var warehouseId = (body.WarehouseId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(storeId) ||
            string.IsNullOrWhiteSpace(variantId) || string.IsNullOrWhiteSpace(warehouseId) || body.Quantity <= 0)
        {
            return ApiEnvelope.Error("validation", "tenantId, storeId, variantId, warehouseId and positive quantity are required.",
                StatusCodes.Status400BadRequest);
        }

        var createdBy = string.IsNullOrWhiteSpace(body.CreatedBy) ? "internal-inventory" : body.CreatedBy.Trim();

        try
        {
            await stock.ReserveStockAsync(
                    new ReserveStockCommand(tenantId, storeId, variantId, warehouseId, body.Quantity, createdBy,
                        body.ReferenceId?.Trim()), DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new { ok = true });
        }
        catch (VariantStockNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (OutOfStockException ex)
        {
            return ApiEnvelope.Error("out_of_stock", ex.Message, StatusCodes.Status422UnprocessableEntity);
        }
        catch (StockInvariantException ex)
        {
            return ApiEnvelope.Error("invalid_stock", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (StockConcurrencyException ex)
        {
            return ApiEnvelope.Error("concurrency", ex.Message, StatusCodes.Status409Conflict);
        }
    }

    private static async Task<IResult> InternalRelease(
        [FromBody] InternalMutationBody? body,
        StockService stock,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return ApiEnvelope.Error("validation", "Request body is required.", StatusCodes.Status400BadRequest);

        var tenantId = (body.TenantId ?? "").Trim();
        var storeId = (body.StoreId ?? "").Trim();
        var variantId = (body.VariantId ?? "").Trim();
        var warehouseId = (body.WarehouseId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(storeId) ||
            string.IsNullOrWhiteSpace(variantId) || string.IsNullOrWhiteSpace(warehouseId) || body.Quantity <= 0)
        {
            return ApiEnvelope.Error("validation", "tenantId, storeId, variantId, warehouseId and positive quantity are required.",
                StatusCodes.Status400BadRequest);
        }

        var createdBy = string.IsNullOrWhiteSpace(body.CreatedBy) ? "internal-inventory" : body.CreatedBy.Trim();

        try
        {
            await stock.ReleaseStockAsync(
                    new ReleaseStockCommand(tenantId, storeId, variantId, warehouseId, body.Quantity, createdBy,
                        body.ReferenceId?.Trim()), DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new { ok = true });
        }
        catch (VariantStockNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (StockInvariantException ex)
        {
            return ApiEnvelope.Error("invalid_stock", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (StockConcurrencyException ex)
        {
            return ApiEnvelope.Error("concurrency", ex.Message, StatusCodes.Status409Conflict);
        }
    }
}
