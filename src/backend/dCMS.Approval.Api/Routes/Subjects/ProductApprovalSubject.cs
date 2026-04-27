using System.Text.Json;
using Dapper;
using dCMS.Core.Approvals;
using Npgsql;

namespace dCMS.Approval.Api.Routes.Subjects;

public sealed class ProductApprovalSubject(string catalogConnectionString) : IApprovalSubject
{
    public string EntityType => "Product";

    public async Task<string?> ValidateAsync(
        string tenantId, string entityId, ApprovalAction action, JsonDocument payloadSnapshot, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(catalogConnectionString);
        var exists = await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            """SELECT COUNT(1) FROM "Products" WHERE "TenantId"=@TenantId AND "Id"=@Id;""",
            new { TenantId = tenantId, Id = entityId },
            cancellationToken: ct));

        return exists == 0 ? "Product not found." : null;
    }

    public async Task ApplyAsync(
        string tenantId, string entityId, ApprovalAction action, JsonDocument payloadSnapshot, string actedByUserId, CancellationToken ct)
    {
        bool? nextActive = action switch
        {
            ApprovalAction.Approve => true,
            ApprovalAction.Reject or ApprovalAction.RequestChanges => false,
            _ => null,
        };
        if (nextActive is null) return;

        await using var conn = new NpgsqlConnection(catalogConnectionString);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE "Products"
            SET "IsActive" = @Active
            WHERE "TenantId" = @TenantId AND "Id" = @Id;
            """,
            new { TenantId = tenantId, Id = entityId, Active = nextActive.Value },
            cancellationToken: ct));
    }
}
