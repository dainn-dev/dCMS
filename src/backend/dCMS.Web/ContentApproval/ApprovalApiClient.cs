using System.Net.Http.Json;

namespace dCMS.Web.ContentApproval;

/// <summary>
/// DAI-721: Lightweight client for submitting Content approval requests from Umbraco to dCMS.Approval.Api.
/// </summary>
public sealed class ApprovalApiClient
{
    public const string HttpClientName = "dCMSApprovalApi";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public ApprovalApiClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<bool> SubmitContentApprovalAsync(
        string tenantId, Guid contentKey, string submittedByUserId, CancellationToken ct = default)
    {
        var baseUrl = _configuration["ContentApproval:ApprovalApiUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(tenantId))
            return false;

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var resp = await client.PostAsJsonAsync(
            $"{baseUrl.TrimEnd('/')}/api/v1/tenants/{tenantId}/approvals",
            new
            {
                entityType = "Content",
                entityId = contentKey.ToString(),
                payloadSnapshot = new { contentKey, submittedAt = DateTimeOffset.UtcNow },
                submittedByUserId,
            }, ct).ConfigureAwait(false);
        return resp.IsSuccessStatusCode;
    }
}
