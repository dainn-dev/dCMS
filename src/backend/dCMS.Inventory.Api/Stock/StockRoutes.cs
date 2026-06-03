using dCMS.AspNetCore.Auth;
using dCMS.Core.Exceptions;
using dCMS.Inventory.Api.Http;
using dCMS.Inventory.Commands;
using dCMS.Inventory.Exceptions;
using dCMS.Inventory.Models;
using dCMS.Inventory.Persistence;
using dCMS.Inventory.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace dCMS.Inventory.Api.Stock;

public static class StockRoutes
{
    public static void MapStockRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();
        var g = app.MapGroup("/api/v1/tenants/{tenantId}/stores/{storeId}/stock")
            .WithTags("inventory-stock")
            .WithTenantStoreAccess(configuration);

        AuthRead(g.MapGet("/variants/{variantId}", GetVariantStockAcrossWarehouses), auth);
        Auth(g.MapPost("/set-on-hand", SetOnHand), auth);
        Auth(g.MapPost("/adjust", AdjustStock), auth);
        Auth(g.MapPost("/reserve", ReserveStock), auth);
        Auth(g.MapPost("/release", ReleaseStock), auth);
        Auth(g.MapPost("/bulk", BulkStock), auth);
    }

    private const int BulkMaxItems = 100;

    private static RouteHandlerBuilder AuthRead(RouteHandlerBuilder builder, bool authEnabled) =>
        authEnabled ? builder.RequireAuthorization(DcmsPolicies.InventoryRead) : builder;

    private static RouteHandlerBuilder Auth(RouteHandlerBuilder builder, bool authEnabled) =>
        authEnabled ? builder.RequireAuthorization(DcmsPolicies.InventoryWrite) : builder;

    private sealed record AdjustBody(string? VariantId, string? WarehouseId, int Delta, string? CreatedBy, string? ReferenceId,
        string? MovementType, string? Note);

    private sealed record ReserveBody(string VariantId, string WarehouseId, int Quantity, string? CreatedBy, string? ReferenceId);

    private sealed record ReleaseBody(string VariantId, string WarehouseId, int Quantity, string? CreatedBy, string? ReferenceId);

    private sealed record StockBulkItem(string Op, string VariantId, string WarehouseId, int? Delta, int? Quantity, string? CreatedBy,
        string? ReferenceId, string? MovementType, string? Note);

    private sealed record StockBulkBody(List<StockBulkItem>? Items, string? CreatedBy, string? ReferenceId);

    private static async Task<IResult> GetVariantStockAcrossWarehouses(
        string tenantId,
        string storeId,
        string variantId,
        IInventoryStockPersistence persistence,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(variantId))
            return ApiEnvelope.Error("validation", "variantId is required.", StatusCodes.Status400BadRequest);

        var id = variantId.Trim();
        var rows = await persistence.ListStockByVariantAsync(tenantId, storeId, id, cancellationToken).ConfigureAwait(false);
        var items = rows.Select(r => new
        {
            warehouseId = r.WarehouseId,
            warehouseName = r.WarehouseName,
            quantity = r.Quantity,
            reservedQuantity = r.ReservedQuantity,
            availableQuantity = r.AvailableQuantity
        }).ToList();

        return ApiEnvelope.Ok(new { variantId = id, items });
    }

    /// <summary>US-12: mixed adjust / reserve / release for up to <see cref="BulkMaxItems"/> variant+warehouse rows (partial success).</summary>
    private static async Task<IResult> BulkStock(
        string tenantId,
        string storeId,
        StockBulkBody body,
        StockService stock,
        CancellationToken cancellationToken)
    {
        if (body.Items is null || body.Items.Count == 0)
            return ApiEnvelope.Error("validation", "items are required.", StatusCodes.Status400BadRequest);
        if (body.Items.Count > BulkMaxItems)
            return ApiEnvelope.Error("validation", $"At most {BulkMaxItems} items per request.", StatusCodes.Status400BadRequest);

        var succeeded = new List<object>();
        var failed = new List<object>();
        var now = DateTimeOffset.UtcNow;
        var defaultCreatedBy = string.IsNullOrWhiteSpace(body.CreatedBy) ? "api" : body.CreatedBy.Trim();

        for (var i = 0; i < body.Items.Count; i++)
        {
            var item = body.Items[i];
            var op = (item.Op ?? "").Trim().ToLowerInvariant();
            var variantId = (item.VariantId ?? "").Trim();
            var warehouseId = (item.WarehouseId ?? "").Trim();
            var createdBy = string.IsNullOrWhiteSpace(item.CreatedBy) ? defaultCreatedBy : item.CreatedBy.Trim();
            var referenceId = item.ReferenceId?.Trim() ?? body.ReferenceId?.Trim();

            if (string.IsNullOrWhiteSpace(variantId) || string.IsNullOrWhiteSpace(warehouseId))
            {
                failed.Add(new { index = i, code = "validation", message = "variantId and warehouseId are required." });
                continue;
            }

            try
            {
                switch (op)
                {
                    case "adjust":
                        if (item.Delta is null)
                        {
                            failed.Add(new { index = i, code = "validation", message = "delta is required for op adjust." });
                            continue;
                        }

                        var bulkMovementType = ParseAdjustMovementType(item.MovementType);
                        var bulkReference = MergeAdjustReference(referenceId, item.Note);
                        await stock.AdjustStockAsync(
                                new AdjustStockCommand(tenantId, storeId, variantId, warehouseId, item.Delta.Value, createdBy,
                                    bulkReference, bulkMovementType), now, cancellationToken)
                            .ConfigureAwait(false);
                        succeeded.Add(new { index = i, op = "adjust", variantId, warehouseId });
                        break;
                    case "reserve":
                        if (item.Quantity is null or <= 0)
                        {
                            failed.Add(new { index = i, code = "validation", message = "positive quantity is required for op reserve." });
                            continue;
                        }

                        await stock.ReserveStockAsync(
                                new ReserveStockCommand(tenantId, storeId, variantId, warehouseId, item.Quantity.Value, createdBy,
                                    referenceId), now, cancellationToken)
                            .ConfigureAwait(false);
                        succeeded.Add(new { index = i, op = "reserve", variantId, warehouseId });
                        break;
                    case "release":
                        if (item.Quantity is null or <= 0)
                        {
                            failed.Add(new { index = i, code = "validation", message = "positive quantity is required for op release." });
                            continue;
                        }

                        await stock.ReleaseStockAsync(
                                new ReleaseStockCommand(tenantId, storeId, variantId, warehouseId, item.Quantity.Value, createdBy,
                                    referenceId), now, cancellationToken)
                            .ConfigureAwait(false);
                        succeeded.Add(new { index = i, op = "release", variantId, warehouseId });
                        break;
                    default:
                        failed.Add(new { index = i, code = "validation", message = "op must be adjust, reserve, or release." });
                        break;
                }
            }
            catch (VariantStockNotFoundException ex)
            {
                failed.Add(new { index = i, code = "not_found", message = ex.Message });
            }
            catch (OutOfStockException ex)
            {
                failed.Add(new { index = i, code = "out_of_stock", message = ex.Message });
            }
            catch (StockInvariantException ex)
            {
                failed.Add(new { index = i, code = "invalid_stock", message = ex.Message });
            }
            catch (StockConcurrencyException ex)
            {
                failed.Add(new { index = i, code = "concurrency", message = ex.Message });
            }
        }

        return ApiEnvelope.Ok(new { succeeded, failed },
            new { requested = body.Items.Count, succeeded = succeeded.Count, failed = failed.Count });
    }

    private const string DefaultWarehouseId = "main";

    private sealed record SetOnHandBody(string? VariantId, string? WarehouseId, int Quantity, string? CreatedBy);

    /// <summary>
    /// Sets the absolute on-hand quantity for a variant (backoffice product editor). When no warehouse is supplied the
    /// store's first warehouse is used, auto-provisioning a default "main" warehouse if the store has none yet.
    /// </summary>
    private static async Task<IResult> SetOnHand(
        string tenantId,
        string storeId,
        [FromBody] SetOnHandBody? body,
        IInventoryStockPersistence persistence,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return ApiEnvelope.Error("validation", "Request body is required.", StatusCodes.Status400BadRequest);

        var variantId = (body.VariantId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(variantId))
            return ApiEnvelope.Error("validation", "variantId is required.", StatusCodes.Status400BadRequest);
        if (body.Quantity < 0)
            return ApiEnvelope.Error("validation", "quantity must be zero or positive.", StatusCodes.Status400BadRequest);

        var warehouseId = (body.WarehouseId ?? "").Trim();
        if (warehouseId.Length == 0)
        {
            var warehouses = await persistence.ListWarehousesForStoreAsync(tenantId, storeId, cancellationToken)
                .ConfigureAwait(false);
            var first = warehouses.FirstOrDefault(w => w.IsActive) ?? warehouses.FirstOrDefault();
            if (first is not null)
            {
                warehouseId = first.Id;
            }
            else
            {
                warehouseId = DefaultWarehouseId;
                try
                {
                    await persistence.CreateWarehouseAsync(tenantId, storeId, warehouseId, "Main Warehouse", null,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (DuplicateWarehouseException)
                {
                    // Concurrent create — fine, the warehouse now exists.
                }
            }
        }

        try
        {
            var createdBy = string.IsNullOrWhiteSpace(body.CreatedBy) ? "api" : body.CreatedBy.Trim();
            var quantity = await persistence.SetOnHandQuantityAsync(tenantId, storeId, variantId, warehouseId,
                body.Quantity, createdBy, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            return ApiEnvelope.Ok(new { variantId, warehouseId, quantity });
        }
        catch (VariantStockNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (StockInvariantException ex)
        {
            return ApiEnvelope.Error("invalid_stock", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> AdjustStock(
        string tenantId,
        string storeId,
        [FromBody] AdjustBody? body,
        StockService stock,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return ApiEnvelope.Error("validation", "Request body is required.", StatusCodes.Status400BadRequest);

        if (string.IsNullOrWhiteSpace(body.VariantId) || string.IsNullOrWhiteSpace(body.WarehouseId))
            return ApiEnvelope.Error("validation", "variantId and warehouseId are required.", StatusCodes.Status400BadRequest);

        try
        {
            var movementType = ParseAdjustMovementType(body.MovementType);
            var reference = MergeAdjustReference(body.ReferenceId, body.Note);
            await stock.AdjustStockAsync(
                new AdjustStockCommand(tenantId, storeId, body.VariantId.Trim(), body.WarehouseId.Trim(), body.Delta,
                    string.IsNullOrWhiteSpace(body.CreatedBy) ? "api" : body.CreatedBy.Trim(), reference, movementType),
                DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            return ApiEnvelope.Ok(new { ok = true });
        }
        catch (VariantStockNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (OutOfStockException ex)
        {
            return OutOfStockEnvelope(ex);
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

    private static StockMovementType ParseAdjustMovementType(string? raw) =>
        (raw ?? "").Trim().ToLowerInvariant() switch
        {
            "import" => StockMovementType.Import,
            "return" => StockMovementType.Return,
            "adjustment" => StockMovementType.Adjustment,
            _ => StockMovementType.Adjustment
        };

    private static string? MergeAdjustReference(string? baseReference, string? note)
    {
        var br = (baseReference ?? "").Trim();
        var n = (note ?? "").Trim();
        if (br.Length == 0 && n.Length == 0)
            return null;
        if (n.Length == 0)
            return br.Length <= 512 ? br : br[..512];
        if (br.Length == 0)
            return n.Length <= 512 ? n : n[..512];
        var combined = br + " | " + n;
        return combined.Length <= 512 ? combined : combined[..512];
    }

    private static IResult OutOfStockEnvelope(OutOfStockException ex) =>
        ApiEnvelope.Error("out_of_stock", ex.Message, StatusCodes.Status422UnprocessableEntity,
            ex.Requested is null
                ? null
                : new { requested = ex.Requested, available = ex.Available, variantId = ex.VariantId });

    private static async Task<IResult> ReserveStock(
        string tenantId,
        string storeId,
        [FromBody] ReserveBody? body,
        StockService stock,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return ApiEnvelope.Error("validation", "Request body is required.", StatusCodes.Status400BadRequest);

        if (string.IsNullOrWhiteSpace(body.VariantId) || string.IsNullOrWhiteSpace(body.WarehouseId) || body.Quantity <= 0)
            return ApiEnvelope.Error("validation", "variantId, warehouseId and positive quantity are required.",
                StatusCodes.Status400BadRequest);

        try
        {
            await stock.ReserveStockAsync(
                new ReserveStockCommand(tenantId, storeId, body.VariantId.Trim(), body.WarehouseId.Trim(), body.Quantity,
                    string.IsNullOrWhiteSpace(body.CreatedBy) ? "api" : body.CreatedBy.Trim(), body.ReferenceId?.Trim()),
                DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            return ApiEnvelope.Ok(new { ok = true });
        }
        catch (VariantStockNotFoundException ex)
        {
            return ApiEnvelope.Error("not_found", ex.Message, StatusCodes.Status404NotFound);
        }
        catch (OutOfStockException ex)
        {
            return OutOfStockEnvelope(ex);
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

    private static async Task<IResult> ReleaseStock(
        string tenantId,
        string storeId,
        [FromBody] ReleaseBody? body,
        StockService stock,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return ApiEnvelope.Error("validation", "Request body is required.", StatusCodes.Status400BadRequest);

        if (string.IsNullOrWhiteSpace(body.VariantId) || string.IsNullOrWhiteSpace(body.WarehouseId) || body.Quantity <= 0)
            return ApiEnvelope.Error("validation", "variantId, warehouseId and positive quantity are required.",
                StatusCodes.Status400BadRequest);

        try
        {
            await stock.ReleaseStockAsync(
                new ReleaseStockCommand(tenantId, storeId, body.VariantId.Trim(), body.WarehouseId.Trim(), body.Quantity,
                    string.IsNullOrWhiteSpace(body.CreatedBy) ? "api" : body.CreatedBy.Trim(), body.ReferenceId?.Trim()),
                DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
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
