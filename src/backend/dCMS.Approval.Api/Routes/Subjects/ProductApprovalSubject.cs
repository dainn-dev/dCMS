using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using dCMS.Core.Approvals;

namespace dCMS.Approval.Api.Routes.Subjects;

/// <summary>
/// Phase C: Product validation + activation goes through Catalog.Api internal HTTP endpoints
/// instead of a direct dcms_catalog connection string.
/// </summary>
public sealed class ProductApprovalSubject(
    IHttpClientFactory httpClientFactory,
    CatalogApiClientOptions options) : IApprovalSubject
{
    public string EntityType => "Product";

    public async Task<string?> ValidateAsync(
        string tenantId, string entityId, ApprovalAction action, JsonDocument payloadSnapshot, CancellationToken ct)
    {
        if (!IsConfigured(options))
            return "Catalog API client is not configured (set Catalog:BaseUrl + InternalCatalog:ApiKey).";

        var client = httpClientFactory.CreateClient(CatalogApiClientOptions.HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{options.BaseUrl!.TrimEnd('/')}/internal/catalog/tenants/{tenantId}/products/{entityId}/exists");
        req.Headers.Add(CatalogApiClientOptions.HeaderName, options.ApiKey);

        using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.NotFound) return "Product not found.";
        resp.EnsureSuccessStatusCode();

        await using var s = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct).ConfigureAwait(false);
        var exists = doc.RootElement.TryGetProperty("data", out var data)
            && data.TryGetProperty("exists", out var existsProp)
            && existsProp.ValueKind == JsonValueKind.True;
        return exists ? null : "Product not found.";
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
        if (!IsConfigured(options))
            throw new InvalidOperationException("Catalog API client is not configured.");

        var client = httpClientFactory.CreateClient(CatalogApiClientOptions.HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"{options.BaseUrl!.TrimEnd('/')}/internal/catalog/tenants/{tenantId}/products/{entityId}/activation")
        {
            Content = JsonContent.Create(new { isActive = nextActive.Value }),
        };
        req.Headers.Add(CatalogApiClientOptions.HeaderName, options.ApiKey);

        using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    private static bool IsConfigured(CatalogApiClientOptions o)
        => !string.IsNullOrWhiteSpace(o.BaseUrl) && !string.IsNullOrWhiteSpace(o.ApiKey);
}
