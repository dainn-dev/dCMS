using Dapper;
using dCMS.Catalog.Api.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace dCMS.Catalog.Api.Internal;

/// <summary>
/// Phase C: cross-service product endpoints used by dCMS.Approval.Api so the approval flow
/// no longer needs a direct connection string to dcms_catalog.
/// </summary>
public static class InternalCatalogRoutes
{
    public static void MapInternalCatalogRoutes(this WebApplication app, string catalogConnectionString)
    {
        var g = app.MapGroup("/internal/catalog")
            .WithTags("catalog-internal")
            .AddEndpointFilter<InternalCatalogApiKeyEndpointFilter>()
            .DisableRateLimiting();

        g.MapGet("/tenants/{tenantId}/products/{id}/exists",
            async (string tenantId, string id, CancellationToken ct) =>
            {
                await using var conn = new NpgsqlConnection(catalogConnectionString);
                var exists = await conn.ExecuteScalarAsync<long>(new CommandDefinition(
                    """SELECT COUNT(1) FROM "Products" WHERE "TenantId"=@TenantId AND "Id"=@Id;""",
                    new { TenantId = tenantId, Id = id },
                    cancellationToken: ct)).ConfigureAwait(false);
                return ApiEnvelope.Ok(new { id, exists = exists > 0 });
            }).AllowAnonymous();

        g.MapGet("/tenants/{tenantId}/products/{id}/category",
            async (string tenantId, string id, CancellationToken ct) =>
            {
                await using var conn = new NpgsqlConnection(catalogConnectionString);
                var categoryId = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
                    """SELECT "CategoryId" FROM "Products" WHERE "TenantId"=@TenantId AND "Id"=@Id;""",
                    new { TenantId = tenantId, Id = id },
                    cancellationToken: ct)).ConfigureAwait(false);
                if (categoryId is null)
                    return ApiEnvelope.Error("not_found", "Product not found.", StatusCodes.Status404NotFound);
                return ApiEnvelope.Ok(new { id, categoryId });
            }).AllowAnonymous();

        g.MapPost("/tenants/{tenantId}/products/{id}/activation",
            async (string tenantId, string id, [FromBody] ActivationBody body, CancellationToken ct) =>
            {
                await using var conn = new NpgsqlConnection(catalogConnectionString);
                var rows = await conn.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE "Products"
                    SET "IsActive" = @Active
                    WHERE "TenantId" = @TenantId AND "Id" = @Id;
                    """,
                    new { TenantId = tenantId, Id = id, Active = body.IsActive },
                    cancellationToken: ct)).ConfigureAwait(false);
                if (rows == 0)
                    return ApiEnvelope.Error("not_found", "Product not found.", StatusCodes.Status404NotFound);
                return ApiEnvelope.Ok(new { id, isActive = body.IsActive });
            }).AllowAnonymous();
    }

    private sealed record ActivationBody(bool IsActive);
}
