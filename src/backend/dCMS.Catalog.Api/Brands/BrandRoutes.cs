using dCMS.AspNetCore.Auth;
using dCMS.Catalog.Api.Http;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Catalog.Api.Brands;

/// <summary>
/// DAI-573: Brand CRUD — tenant-scoped master data.
/// Route group: /api/v1/tenants/{tenantId}/brands
/// Auth: CatalogRead (GET), CatalogWrite (POST/PUT/DELETE).
/// </summary>
public static class BrandRoutes
{
    public static void MapBrandRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();

        var g = app.MapGroup("/api/v1/tenants/{tenantId}/brands")
            .WithTags("catalog-brands")
            .WithTenantAccess(configuration);

        Auth(g.MapGet("",        ListBrands),   auth, write: false);
        Auth(g.MapGet("{code}",  GetBrand),     auth, write: false);
        Auth(g.MapPost("",       CreateBrand),  auth, write: true);
        Auth(g.MapPut("{code}",  UpdateBrand),  auth, write: true);
        Auth(g.MapDelete("{code}", DeleteBrand), auth, write: true);
    }

    private static RouteHandlerBuilder Auth(RouteHandlerBuilder b, bool authEnabled, bool write) =>
        authEnabled
            ? b.RequireAuthorization(write ? DcmsPolicies.CatalogWrite : DcmsPolicies.CatalogRead)
            : b;

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed record BrandDto(
        string  TenantId,
        string  Code,
        string  Name,
        bool    Active,
        string  ImageUrl,
        string  ImageAlt,
        /// <summary>Dynamic additional-info fields as a raw JSON object string.</summary>
        string  AdditionalInfo,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record BrandWriteRequest(
        string  Name,
        bool    Active,
        string? ImageUrl,
        string? ImageAlt,
        /// <summary>JSON object string e.g. <c>{"field-id-1":"value"}</c>. Null/empty preserved as <c>{}</c>.</summary>
        string? AdditionalInfo = null);

    private static BrandDto ToDto(Brand b) =>
        new(b.TenantId, b.Code, b.Name, b.Active, b.ImageUrl, b.ImageAlt, b.AdditionalInfo, b.CreatedAt, b.UpdatedAt);

    // ── Handlers ─────────────────────────────────────────────────────────────

    /// <summary>GET /api/v1/tenants/{tenantId}/brands</summary>
    private static async Task<IResult> ListBrands(
        string tenantId,
        IBrandPersistence brands,
        bool?   active   = null,
        string? search   = null,
        int     page     = 1,
        int     pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(1, page);

        var total = await brands.CountBrandsAsync(tenantId, active, search, cancellationToken)
            .ConfigureAwait(false);
        var items = await brands.ListBrandsAsync(tenantId, active, search, page, pageSize, cancellationToken)
            .ConfigureAwait(false);

        return ApiEnvelope.Ok(
            data: items.Select(ToDto),
            meta: new { total, page, pageSize });
    }

    /// <summary>GET /api/v1/tenants/{tenantId}/brands/{code}</summary>
    private static async Task<IResult> GetBrand(
        string tenantId,
        string code,
        IBrandPersistence brands,
        CancellationToken cancellationToken = default)
    {
        var brand = await brands.GetBrandAsync(tenantId, code.ToUpperInvariant(), cancellationToken)
            .ConfigureAwait(false);

        return brand is null
            ? ApiEnvelope.Error("not_found", $"Brand '{code}' not found.", StatusCodes.Status404NotFound)
            : ApiEnvelope.Ok(ToDto(brand));
    }

    /// <summary>POST /api/v1/tenants/{tenantId}/brands</summary>
    private static async Task<IResult> CreateBrand(
        string tenantId,
        [FromBody] CreateBrandRequest body,
        IBrandPersistence brands,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body.Code))
            return ApiEnvelope.Error("validation_error", "Brand code is required.", StatusCodes.Status400BadRequest);

        var normalizedCode = body.Code.Trim().ToUpperInvariant();

        if (!Brand.IsValidCode(normalizedCode))
            return ApiEnvelope.Error("validation_error",
                $"Brand code '{normalizedCode}' is invalid. Expected: 2–5 uppercase letters, dash, 1–6 digits (e.g. CAS-7721).",
                StatusCodes.Status400BadRequest);

        if (string.IsNullOrWhiteSpace(body.Name))
            return ApiEnvelope.Error("validation_error", "Brand name is required.", StatusCodes.Status400BadRequest);

        if (await brands.CodeExistsAsync(tenantId, normalizedCode, cancellationToken).ConfigureAwait(false))
            return ApiEnvelope.Error("conflict", $"Brand code '{normalizedCode}' already exists.", StatusCodes.Status409Conflict);

        Brand brand;
        try
        {
            brand = Brand.Create(
                tenantId, normalizedCode, body.Name,
                body.ImageUrl ?? string.Empty,
                body.ImageAlt ?? string.Empty,
                body.Active,
                DateTimeOffset.UtcNow,
                body.AdditionalInfo ?? "{}");
        }
        catch (ArgumentException ex)
        {
            return ApiEnvelope.Error("validation_error", ex.Message, StatusCodes.Status400BadRequest);
        }

        await brands.SaveBrandAsync(brand, cancellationToken).ConfigureAwait(false);

        return Results.Json(
            new { data = ToDto(brand), meta = (object?)null, error = (object?)null },
            statusCode: StatusCodes.Status201Created);
    }

    private sealed record CreateBrandRequest(
        string  Code,
        string  Name,
        bool    Active         = true,
        string? ImageUrl       = null,
        string? ImageAlt       = null,
        string? AdditionalInfo = null);

    /// <summary>PUT /api/v1/tenants/{tenantId}/brands/{code}</summary>
    private static async Task<IResult> UpdateBrand(
        string tenantId,
        string code,
        [FromBody] BrandWriteRequest body,
        IBrandPersistence brands,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return ApiEnvelope.Error("validation_error", "Brand name is required.", StatusCodes.Status400BadRequest);

        var brand = await brands.GetBrandAsync(tenantId, code.ToUpperInvariant(), cancellationToken)
            .ConfigureAwait(false);

        if (brand is null)
            return ApiEnvelope.Error("not_found", $"Brand '{code}' not found.", StatusCodes.Status404NotFound);

        try
        {
            brand.UpdateDetails(
                body.Name, body.Active,
                body.ImageUrl ?? string.Empty,
                body.ImageAlt ?? string.Empty,
                body.AdditionalInfo ?? brand.AdditionalInfo,   // preserve existing if not sent
                DateTimeOffset.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return ApiEnvelope.Error("validation_error", ex.Message, StatusCodes.Status400BadRequest);
        }

        await brands.SaveBrandAsync(brand, cancellationToken).ConfigureAwait(false);

        return ApiEnvelope.Ok(ToDto(brand));
    }

    /// <summary>DELETE /api/v1/tenants/{tenantId}/brands/{code}</summary>
    private static async Task<IResult> DeleteBrand(
        string tenantId,
        string code,
        IBrandPersistence brands,
        CancellationToken cancellationToken = default)
    {
        var deleted = await brands.DeleteBrandAsync(tenantId, code.ToUpperInvariant(), cancellationToken)
            .ConfigureAwait(false);

        return deleted
            ? Results.NoContent()
            : ApiEnvelope.Error("not_found", $"Brand '{code}' not found.", StatusCodes.Status404NotFound);
    }
}
