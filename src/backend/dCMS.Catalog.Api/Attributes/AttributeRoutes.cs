using dCMS.AspNetCore.Auth;
using dCMS.Catalog.Api.Http;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Catalog.Api.Attributes;

/// <summary>
/// DAI-592: Attribute + AttributeValue management CRUD.
/// Tenant-scoped; no storeId (management layer).
/// Auth: CatalogRead (GET), CatalogWrite (POST/PUT/DELETE).
/// </summary>
public static class AttributeRoutes
{
    public static void MapAttributeRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();

        var g = app.MapGroup("/api/v1/tenants/{tenantId}/attributes")
            .WithTags("catalog-attributes")
            .WithTenantAccess(configuration);

        // ── Attribute CRUD ────────────────────────────────────────────────────
        Auth(g.MapGet("",          ListAttributes),    auth, write: false);
        Auth(g.MapGet("{id:int}",   GetAttribute),      auth, write: false);
        Auth(g.MapPost("",          CreateAttribute),   auth, write: true);
        Auth(g.MapPut("{id:int}",   UpdateAttribute),   auth, write: true);
        Auth(g.MapDelete("{id:int}", DeleteAttribute),  auth, write: true);

        // ── Value CRUD (nested) ───────────────────────────────────────────────
        Auth(g.MapGet("{id:int}/values",                     ListValues),   auth, write: false);
        Auth(g.MapPost("{id:int}/values",                    CreateValue),  auth, write: true);
        Auth(g.MapPut("{id:int}/values/{valueId:int}",       UpdateValue),  auth, write: true);
        Auth(g.MapDelete("{id:int}/values/{valueId:int}",    DeleteValue),  auth, write: true);
    }

    private static RouteHandlerBuilder Auth(RouteHandlerBuilder b, bool authEnabled, bool write) =>
        authEnabled
            ? b.RequireAuthorization(write ? DcmsPolicies.CatalogWrite : DcmsPolicies.CatalogRead)
            : b;

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed record AttributeDto(
        int    Id,
        string TenantId,
        string Name,
        string Code,
        string Type,
        bool   Required,
        string Description,
        int    SortOrder,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        IReadOnlyList<AttributeValueDto>? Values = null);

    private sealed record AttributeValueDto(
        int    Id,
        int    AttributeId,
        string Name,
        string Code,
        string ColorHex,
        string ImageUrl,
        int    SortOrder,
        DateTimeOffset CreatedAt);

    private sealed record AttributeWriteRequest(
        string  Name,
        string  Code,
        string  Type        = "TEXT",
        bool    Required    = false,
        string? Description = null,
        int     SortOrder   = 0);

    private sealed record ValueWriteRequest(
        string  Name,
        string? Code     = null,
        string? ColorHex = null,
        string? ImageUrl = null,
        int     SortOrder = 0);

    private static AttributeDto ToDto(CatalogAttributeRow a, IReadOnlyList<CatalogAttributeValueRow>? values = null) =>
        new(a.Id, a.TenantId, a.Name, a.Code, a.Type, a.Required, a.Description, a.SortOrder, a.CreatedAt, a.UpdatedAt,
            values?.Select(ToValueDto).ToList());

    private static AttributeValueDto ToValueDto(CatalogAttributeValueRow v) =>
        new(v.Id, v.AttributeId, v.Name, v.Code, v.ColorHex, v.ImageUrl, v.SortOrder, v.CreatedAt);

    // ── Attribute handlers ────────────────────────────────────────────────────

    private static async Task<IResult> ListAttributes(
        string tenantId, ICatalogPersistence catalog,
        int page = 1, int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 500);
        page     = Math.Max(1, page);
        var total = await catalog.CountAttributesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var items = await catalog.ListAttributesAsync(tenantId, page, pageSize, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(items.Select(a => ToDto(a)), new { total, page, pageSize });
    }

    private static async Task<IResult> GetAttribute(
        string tenantId, int id, ICatalogPersistence catalog,
        CancellationToken cancellationToken = default)
    {
        var attr = await catalog.GetAttributeByIdAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        if (attr is null) return ApiEnvelope.Error("not_found", $"Attribute {id} not found.", StatusCodes.Status404NotFound);
        var values = await catalog.ListAttributeValuesAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(ToDto(attr, values));
    }

    private static async Task<IResult> CreateAttribute(
        string tenantId, [FromBody] AttributeWriteRequest body,
        ICatalogPersistence catalog, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return ApiEnvelope.Error("validation_error", "Name is required.", StatusCodes.Status400BadRequest);

        var normalizedCode = body.Code.Trim().ToLowerInvariant();
        if (!CatalogAttributeRow.IsValidCode(normalizedCode))
            return ApiEnvelope.Error("validation_error",
                $"Code '{normalizedCode}' is invalid. Use snake_case (e.g. color_primary).",
                StatusCodes.Status400BadRequest);

        if (!CatalogAttributeRow.ValidTypes.Contains(body.Type))
            return ApiEnvelope.Error("validation_error",
                $"Type '{body.Type}' is invalid. Valid: TEXT, COLOR, IMAGE, SELECT, BOOLEAN.",
                StatusCodes.Status400BadRequest);

        if (await catalog.AttributeCodeExistsAsync(tenantId, normalizedCode, null, cancellationToken).ConfigureAwait(false))
            return ApiEnvelope.Error("conflict", $"Attribute code '{normalizedCode}' already exists.", StatusCodes.Status409Conflict);

        var now = DateTimeOffset.UtcNow;
        var row = new CatalogAttributeRow(0, tenantId, body.Name.Trim(), normalizedCode,
            body.Type.ToUpperInvariant(), body.Required, body.Description ?? "", body.SortOrder, now, now);

        var newId = await catalog.CreateAttributeAsync(row, cancellationToken).ConfigureAwait(false);
        var created = await catalog.GetAttributeByIdAsync(newId, tenantId, cancellationToken).ConfigureAwait(false);

        return Results.Json(
            new { data = ToDto(created!, []), meta = (object?)null, error = (object?)null },
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateAttribute(
        string tenantId, int id, [FromBody] AttributeWriteRequest body,
        ICatalogPersistence catalog, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return ApiEnvelope.Error("validation_error", "Name is required.", StatusCodes.Status400BadRequest);

        var existing = await catalog.GetAttributeByIdAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return ApiEnvelope.Error("not_found", $"Attribute {id} not found.", StatusCodes.Status404NotFound);

        var normalizedCode = body.Code.Trim().ToLowerInvariant();
        if (!CatalogAttributeRow.IsValidCode(normalizedCode))
            return ApiEnvelope.Error("validation_error",
                $"Code '{normalizedCode}' is invalid.", StatusCodes.Status400BadRequest);

        if (!CatalogAttributeRow.ValidTypes.Contains(body.Type))
            return ApiEnvelope.Error("validation_error",
                $"Type '{body.Type}' is invalid.", StatusCodes.Status400BadRequest);

        if (normalizedCode != existing.Code &&
            await catalog.AttributeCodeExistsAsync(tenantId, normalizedCode, id, cancellationToken).ConfigureAwait(false))
            return ApiEnvelope.Error("conflict", $"Code '{normalizedCode}' already exists.", StatusCodes.Status409Conflict);

        var updated = existing with
        {
            Name = body.Name.Trim(), Code = normalizedCode,
            Type = body.Type.ToUpperInvariant(), Required = body.Required,
            Description = body.Description ?? existing.Description, SortOrder = body.SortOrder,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await catalog.UpdateAttributeAsync(updated, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(ToDto(updated));
    }

    private static async Task<IResult> DeleteAttribute(
        string tenantId, int id, ICatalogPersistence catalog,
        CancellationToken cancellationToken = default)
    {
        var deleted = await catalog.DeleteAttributeAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        return deleted
            ? Results.NoContent()
            : ApiEnvelope.Error("not_found", $"Attribute {id} not found.", StatusCodes.Status404NotFound);
    }

    // ── Value handlers ────────────────────────────────────────────────────────

    private static async Task<IResult> ListValues(
        string tenantId, int id, ICatalogPersistence catalog,
        CancellationToken cancellationToken = default)
    {
        var attr = await catalog.GetAttributeByIdAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        if (attr is null) return ApiEnvelope.Error("not_found", $"Attribute {id} not found.", StatusCodes.Status404NotFound);
        var values = await catalog.ListAttributeValuesAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(values.Select(ToValueDto), new { total = values.Count });
    }

    private static async Task<IResult> CreateValue(
        string tenantId, int id, [FromBody] ValueWriteRequest body,
        ICatalogPersistence catalog, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return ApiEnvelope.Error("validation_error", "Name is required.", StatusCodes.Status400BadRequest);

        var attr = await catalog.GetAttributeByIdAsync(id, tenantId, cancellationToken).ConfigureAwait(false);
        if (attr is null) return ApiEnvelope.Error("not_found", $"Attribute {id} not found.", StatusCodes.Status404NotFound);

        var row = new CatalogAttributeValueRow(0, id,
            body.Name.Trim(), (body.Code ?? "").Trim().ToLowerInvariant(),
            (body.ColorHex ?? "").Trim(), (body.ImageUrl ?? "").Trim(),
            body.SortOrder, DateTimeOffset.UtcNow);

        var newId = await catalog.CreateAttributeValueAsync(row, cancellationToken).ConfigureAwait(false);
        var created = row with { Id = newId };
        return Results.Json(
            new { data = ToValueDto(created), meta = (object?)null, error = (object?)null },
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateValue(
        string tenantId, int id, int valueId, [FromBody] ValueWriteRequest body,
        ICatalogPersistence catalog, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return ApiEnvelope.Error("validation_error", "Name is required.", StatusCodes.Status400BadRequest);

        var row = new CatalogAttributeValueRow(valueId, id,
            body.Name.Trim(), (body.Code ?? "").Trim().ToLowerInvariant(),
            (body.ColorHex ?? "").Trim(), (body.ImageUrl ?? "").Trim(),
            body.SortOrder, DateTimeOffset.UtcNow);

        var ok = await catalog.UpdateAttributeValueAsync(row, tenantId, cancellationToken).ConfigureAwait(false);
        return ok
            ? ApiEnvelope.Ok(ToValueDto(row))
            : ApiEnvelope.Error("not_found", $"Value {valueId} not found.", StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> DeleteValue(
        string tenantId, int id, int valueId, ICatalogPersistence catalog,
        CancellationToken cancellationToken = default)
    {
        var deleted = await catalog.DeleteAttributeValueAsync(valueId, id, tenantId, cancellationToken).ConfigureAwait(false);
        return deleted
            ? Results.NoContent()
            : ApiEnvelope.Error("not_found", $"Value {valueId} not found.", StatusCodes.Status404NotFound);
    }
}
