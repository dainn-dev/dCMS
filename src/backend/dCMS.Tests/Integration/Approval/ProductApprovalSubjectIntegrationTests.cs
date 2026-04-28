using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Integration.Approval;

/// <summary>
/// Phase C: <see cref="dCMS.Approval.Api.Routes.Subjects.ProductApprovalSubject"/> no longer talks
/// to dcms_catalog directly — it calls Catalog.Api over HTTP. The previous DB-level integration
/// tests were tied to the old constructor signature and have been retired in favor of API-level
/// coverage at the Catalog.Api `/internal/catalog/...` endpoints.
/// </summary>
public sealed class ProductApprovalSubjectIntegrationTests
{
    [Fact]
    public void EntityType_is_Product()
    {
        var subject = new dCMS.Approval.Api.Routes.Subjects.ProductApprovalSubject(
            new HttpClientFactoryStub(),
            new dCMS.Approval.Api.Routes.Subjects.CatalogApiClientOptions());
        subject.EntityType.Should().Be("Product");
    }

    private sealed class HttpClientFactoryStub : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }
}
