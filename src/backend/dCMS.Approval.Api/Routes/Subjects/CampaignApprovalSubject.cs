using System.Text.Json;
using Dapper;
using dCMS.Core.Approvals;
using Npgsql;

namespace dCMS.Approval.Api.Routes.Subjects;

public sealed class CampaignApprovalSubject(string catalogConnectionString) : IApprovalSubject
{
    public string EntityType => "Campaign";

    public async Task<string?> ValidateAsync(string tenantId, string entityId, ApprovalAction action, JsonDocument payloadSnapshot, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(catalogConnectionString);
        var ws = await conn.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            """SELECT "WorkflowState" FROM "Campaigns" WHERE "TenantId"=@TenantId AND "Id"=@Id LIMIT 1;""",
            new { TenantId = tenantId, Id = entityId },
            cancellationToken: ct));

        if (ws is null)
            return "Campaign not found.";

        return action switch
        {
            ApprovalAction.Submit when !string.Equals(ws, "draft", StringComparison.OrdinalIgnoreCase)
                => $"Campaign must be draft to submit (current={ws}).",
            ApprovalAction.Approve or ApprovalAction.Reject or ApprovalAction.RequestChanges
                when !string.Equals(ws, "pending_approval", StringComparison.OrdinalIgnoreCase)
                => $"Campaign must be pending_approval to finalize (current={ws}).",
            _ => null
        };
    }

    public async Task ApplyAsync(string tenantId, string entityId, ApprovalAction action, JsonDocument payloadSnapshot, string actedByUserId, CancellationToken ct)
    {
        var next = action switch
        {
            ApprovalAction.Submit => "pending_approval",
            ApprovalAction.Approve => "approved",
            ApprovalAction.Reject => "rejected",
            ApprovalAction.RequestChanges => "draft",
            _ => null
        };

        if (next is null)
            return;

        await using var conn = new NpgsqlConnection(catalogConnectionString);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE "Campaigns"
            SET "WorkflowState"=@NextState, "UpdatedAt"=now()
            WHERE "TenantId"=@TenantId AND "Id"=@Id;
            """,
            new { TenantId = tenantId, Id = entityId, NextState = next },
            cancellationToken: ct));
    }
}

