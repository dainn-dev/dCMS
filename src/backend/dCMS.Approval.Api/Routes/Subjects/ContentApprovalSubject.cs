using System.Net.Http.Json;
using System.Text.Json;
using dCMS.Core.Approvals;

namespace dCMS.Approval.Api.Routes.Subjects;

/// <summary>
/// DAI-721: Content approval subject. The Approval.Api process can't reach Umbraco's IContentService
/// directly, so on Approve we POST to an internal callback in dCMS.Web which performs the publish.
/// </summary>
public sealed class ContentApprovalSubject : IApprovalSubject
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ContentApprovalCallbackOptions _options;

    public ContentApprovalSubject(IHttpClientFactory httpClientFactory, ContentApprovalCallbackOptions options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public string EntityType => "Content";

    public Task<string?> ValidateAsync(
        string tenantId, string entityId, ApprovalAction action, JsonDocument payloadSnapshot, CancellationToken ct)
    {
        if (!Guid.TryParse(entityId, out _))
            return Task.FromResult<string?>("Content entityId must be an Umbraco content GUID.");
        return Task.FromResult<string?>(null);
    }

    public async Task ApplyAsync(
        string tenantId, string entityId, ApprovalAction action, JsonDocument payloadSnapshot, string actedByUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.CallbackUrl) || string.IsNullOrWhiteSpace(_options.ApiKey))
            return; // disabled — no-op

        var verb = action switch
        {
            ApprovalAction.Approve => "publish",
            ApprovalAction.Reject or ApprovalAction.RequestChanges => "unpublish",
            _ => null,
        };
        if (verb is null) return;

        var client = _httpClientFactory.CreateClient(ContentApprovalCallbackOptions.HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_options.CallbackUrl.TrimEnd('/')}/{verb}")
        {
            Content = JsonContent.Create(new
            {
                tenantId,
                contentKey = entityId,
                actedByUserId,
            }),
        };
        req.Headers.Add("X-Internal-Api-Key", _options.ApiKey);

        using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }
}

public sealed class ContentApprovalCallbackOptions
{
    public const string HttpClientName = "ContentApprovalCallback";

    public string? CallbackUrl { get; set; }
    public string? ApiKey { get; set; }
}
