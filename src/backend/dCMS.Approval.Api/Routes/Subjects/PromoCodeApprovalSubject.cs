using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using dCMS.Core.Approvals;

namespace dCMS.Approval.Api.Routes.Subjects;

/// <summary>
/// Phase C: validation + application of PromoCode approval transitions go through Promotions.Api
/// internal HTTP endpoints instead of a direct dcms_promotions connection string.
/// </summary>
public sealed class PromoCodeApprovalSubject(
    IHttpClientFactory httpClientFactory,
    PromotionsApiClientOptions options) : IApprovalSubject
{
    public string EntityType => "PromoCode";

    public async Task<string?> ValidateAsync(
        string tenantId, string entityId, ApprovalAction action, JsonDocument payloadSnapshot, CancellationToken ct)
    {
        if (!CampaignApprovalSubject.IsConfigured(options))
            return "Promotions API client is not configured (set Promotions:BaseUrl + InternalPromotions:ApiKey).";

        var client = httpClientFactory.CreateClient(PromotionsApiClientOptions.HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{options.BaseUrl!.TrimEnd('/')}/internal/promotions/tenants/{tenantId}/promo-codes/{entityId}/workflow-state");
        req.Headers.Add(PromotionsApiClientOptions.HeaderName, options.ApiKey);

        using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.NotFound) return "Promo code not found.";
        resp.EnsureSuccessStatusCode();

        var ws = await CampaignApprovalSubject.ReadWorkflowStateAsync(resp, ct).ConfigureAwait(false);
        if (ws is null) return "Promo code not found.";

        return action switch
        {
            ApprovalAction.Submit when !string.Equals(ws, "draft", StringComparison.OrdinalIgnoreCase)
                => $"Promo code must be draft to submit (current={ws}).",
            ApprovalAction.Approve or ApprovalAction.Reject or ApprovalAction.RequestChanges
                when !string.Equals(ws, "pending_approval", StringComparison.OrdinalIgnoreCase)
                => $"Promo code must be pending_approval to finalize (current={ws}).",
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
        if (!CampaignApprovalSubject.IsConfigured(options))
            throw new InvalidOperationException("Promotions API client is not configured.");

        var client = httpClientFactory.CreateClient(PromotionsApiClientOptions.HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"{options.BaseUrl!.TrimEnd('/')}/internal/promotions/tenants/{tenantId}/promo-codes/{entityId}/workflow-transition")
        {
            Content = JsonContent.Create(new { toState = next, actorUserId = actedByUserId, comment = (string?)null }),
        };
        req.Headers.Add(PromotionsApiClientOptions.HeaderName, options.ApiKey);

        using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }
}
