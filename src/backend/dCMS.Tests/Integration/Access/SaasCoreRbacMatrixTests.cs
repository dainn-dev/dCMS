using System.Net.Http.Headers;
using dCMS.Tests.Integration.Access;
using dCMS.Tests.Integration.Promotions;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Integration.Access;

/// <summary>Parametrized RBAC matrix rows — promotions service block.</summary>
[Collection("PromotionsApiAuth")]
public sealed class SaasCoreRbacMatrixTests(PromotionsApiAuthFixture fixture)
{
    private void Skip() =>
        Xunit.Skip.IfNot(fixture.IsReady && fixture.Factory is not null, "Docker / Testcontainers not available.");

    [Theory]
    [MemberData(nameof(PromotionsMatrixData))]
    public async Task Promotions_matrix_row(SaasCoreAccessCase c)
    {
        Skip();
        c.Service.Should().Be("promotions");

        using var client = fixture.Factory!.CreateClient();
        if (c.Role is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", SaasCoreJwtFactory.MintForRole(c.Role, c.TokenTenantId));
        }

        var path = c.PathTemplate.Replace("{tenantId}", c.RouteTenantId);
        using var request = new HttpRequestMessage(new HttpMethod(c.Method), path);
        var response = await client.SendAsync(request);

        await SaasCoreHttpAssert.AssertAsync(response, c.ExpectedStatus, c.ExpectedErrorCode);
    }

    public static IEnumerable<object[]> PromotionsMatrixData() => SaasCoreRbacMatrixCases.PromotionsCases();
}
