using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using dCMS.Core.Approvals;

namespace dCMS.Approval.Api.Routes.Subjects;

/// <summary>
/// Phase C: validation + application of Campaign approval transitions go through Promotions.Api
/// internal HTTP endpoints instead of a direct dcms_promotions connection string.
/// </summary>
public sealed class CampaignApprovalSubject(
    IHttpClientFactory httpClientFactory,
    PromotionsApiClientOptions options) : IApprovalSubject
{
    public string EntityType => "Campaign";

    public async Task<string?> ValidateAsync(
        string tenantId, string entityId, ApprovalAction action, JsonDocument payloadSnapshot, CancellationToken ct)
    {
        if (!IsConfigured(options))
            return "Promotions API client is not configured (set Promotions:BaseUrl + InternalPromotions:ApiKey).";

        var client = httpClientFactory.CreateClient(PromotionsApiClientOptions.HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{options.BaseUrl!.TrimEnd('/')}/internal/promotions/tenants/{tenantId}/campaigns/{entityId}/workflow-state");
        req.Headers.Add(PromotionsApiClientOptions.HeaderName, options.ApiKey);

        using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.NotFound) return "Campaign not found.";
        resp.EnsureSuccessStatusCode();

        var ws = await ReadWorkflowStateAsync(resp, ct).ConfigureAwait(false);
        if (ws is null) return "Campaign not found.";

        return action switch
        {
            ApprovalAction.Submit when !string.Equals(ws, "draft", StringComparison.OrdinalIgnoreCase)
                => $"Campaign must be draft to submit (current={ws}).",
            ApprovalAction.Approve or ApprovalAction.Reject or ApprovalAction.RequestChanges
                when !string.Equals(ws, "pending_approval", StringComparison.OrdinalIgnoreCase)
                => $"Campaign must be pending_approval to finalize (current={ws}).",
            _ => null,
        };
    }

    public async Task ApplyAsync(
        string tenantId, string entityId, ApprovalAction action, JsonDocument payloadSnapshot, string actedByUserId, CancellationToken ct)
    {
        var next = action switch
        {
            ApprovalAction.Submit => "pending_approval",
            ApprovalAction.Approve => "approved",
            ApprovalAction.Reject => "rejected",
            ApprovalAction.RequestChanges => "draft",
            _ => null,
        };
        if (next is null) return;
        if (!IsConfigured(options))
            throw new InvalidOperationException("Promotions API client is not configured.");

        var client = httpClientFactory.CreateClient(PromotionsApiClientOptions.HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"{options.BaseUrl!.TrimEnd('/')}/internal/promotions/tenants/{tenantId}/campaigns/{entityId}/workflow-transition")
        {
            Content = JsonContent.Create(new { toState = next, actorUserId = actedByUserId, comment = (string?)null }),
        };
        req.Headers.Add(PromotionsApiClientOptions.HeaderName, options.ApiKey);

        using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    internal static bool IsConfigured(PromotionsApiClientOptions o)
        => !string.IsNullOrWhiteSpace(o.BaseUrl) && !string.IsNullOrWhiteSpace(o.ApiKey);

    internal static async Task<string?> ReadWorkflowStateAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        await using var s = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct).ConfigureAwait(false);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return null;
        return data.TryGetProperty("workflowState", out var ws) ? ws.GetString() : null;
    }
}
