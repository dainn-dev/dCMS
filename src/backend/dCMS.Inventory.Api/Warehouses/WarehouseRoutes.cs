using System.Text.RegularExpressions;
using dCMS.AspNetCore.Auth;
using dCMS.Inventory.Api.Http;
using dCMS.Inventory.Exceptions;
using dCMS.Inventory.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace dCMS.Inventory.Api.Warehouses;

public static class WarehouseRoutes
{
    private static readonly Regex WarehouseIdPattern = new("^[a-zA-Z0-9_-]{1,64}$", RegexOptions.Compiled);

    public static void MapWarehouseRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();
        var g = app.MapGroup("/api/v1/tenants/{tenantId}/stores/{storeId}/warehouses")
            .WithTags("inventory-warehouses")
            .WithTenantStoreAccess(configuration);

        AuthRead(g.MapGet("", ListWarehouses), auth);
        AuthWrite(g.MapPost("", CreateWarehouse), auth);
    }

    private static RouteHandlerBuilder AuthRead(RouteHandlerBuilder builder, bool authEnabled) =>
        authEnabled ? builder.RequireAuthorization(DcmsPolicies.InventoryRead) : builder;

    private static RouteHandlerBuilder AuthWrite(RouteHandlerBuilder builder, bool authEnabled) =>
        authEnabled ? builder.RequireAuthorization(DcmsPolicies.InventoryWrite) : builder;

    private static async Task<IResult> ListWarehouses(
        string tenantId,
        string storeId,
        IInventoryStockPersistence persistence,
        CancellationToken cancellationToken)
    {
        var list = await persistence.ListWarehousesForStoreAsync(tenantId, storeId, cancellationToken).ConfigureAwait(false);
        var items = list.Select(w => new { id = w.Id, name = w.Name, address = w.Address, isActive = w.IsActive }).ToList();
        return ApiEnvelope.Ok(new { items });
    }

    private sealed record CreateWarehouseBody(string? Id, string? Name, string? Address);

    private static async Task<IResult> CreateWarehouse(
        string tenantId,
        string storeId,
        [FromBody] CreateWarehouseBody? body,
        IInventoryStockPersistence persistence,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return ApiEnvelope.Error("validation", "Request body is required.", StatusCodes.Status400BadRequest);

        var id = (body.Id ?? "").Trim();
        var name = (body.Name ?? "").Trim();
        var address = string.IsNullOrWhiteSpace(body.Address) ? null : body.Address.Trim();

        if (!WarehouseIdPattern.IsMatch(id))
        {
            return ApiEnvelope.Error("validation", "id must be 1–64 characters: letters, digits, underscore or hyphen.",
                StatusCodes.Status400BadRequest);
        }

        if (name.Length is 0 or > 200)
            return ApiEnvelope.Error("validation", "name is required and must be at most 200 characters.",
                StatusCodes.Status400BadRequest);

        if (address is not null && address.Length > 500)
            return ApiEnvelope.Error("validation", "address must be at most 500 characters.", StatusCodes.Status400BadRequest);

        try
        {
            await persistence.CreateWarehouseAsync(tenantId, storeId, id, name, address, cancellationToken)
                .ConfigureAwait(false);
            return ApiEnvelope.Ok(new { id, name, address });
        }
        catch (DuplicateWarehouseException ex)
        {
            return ApiEnvelope.Error("duplicate_warehouse", ex.Message, StatusCodes.Status409Conflict);
        }
    }
}
