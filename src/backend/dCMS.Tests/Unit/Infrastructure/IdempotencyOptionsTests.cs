using dCMS.Infrastructure.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace dCMS.Tests.Unit.Infrastructure;

public sealed class IdempotencyOptionsTests
{
    [Theory]
    [InlineData("POST", "/api/v1/tenants/t/stores/s/products/bulk", "/products", true)]
    [InlineData("PUT", "/api/v1/tenants/t/stores/s/products/p1", "/products", true)]
    [InlineData("POST", "/api/v1/tenants/t/stores/s/stock/adjust", "/stock", true)]
    [InlineData("POST", "/api/v1/tenants/t/stores/s/stock/bulk", "/stock", true)]
    [InlineData("GET", "/api/v1/tenants/t/stores/s/products", "/products", false)]
    [InlineData("POST", "/api/v1/tenants/t/stores/s/stock/bulk", "/products", false)]
    [InlineData("POST", "/other/api/v1/x", "/products", false)]
    public void MatchesRequest_respects_method_path_and_substrings(string method, string path, string substring, bool expected)
    {
        var o = new IdempotencyOptions { PathSubstrings = [substring], RequireApiV1Prefix = true };
        o.MatchesRequest(method, path).Should().Be(expected);
    }

    [Theory]
    [InlineData("POST", "/api/orders", "/api/orders", true)]
    [InlineData("POST", "/api/orders/550e8400-e29b-41d4-a716-446655440000/cancel", "/api/orders", true)]
    [InlineData("GET", "/api/orders", "/api/orders", false)]
    [InlineData("POST", "/api/v1/other", "/api/orders", false)]
    public void MatchesRequest_order_service_paths_when_v1_prefix_not_required(
        string method,
        string path,
        string substring,
        bool expected)
    {
        var o = new IdempotencyOptions { PathSubstrings = [substring], RequireApiV1Prefix = false };
        o.MatchesRequest(method, path).Should().Be(expected);
    }

    [Fact]
    public void MatchesRequest_returns_false_when_no_substrings_configured()
    {
        var o = new IdempotencyOptions { PathSubstrings = [] };
        o.MatchesRequest(HttpMethods.Post, "/api/v1/tenants/t/stores/s/products/bulk").Should().BeFalse();
    }
}
