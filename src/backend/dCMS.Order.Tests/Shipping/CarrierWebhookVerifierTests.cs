using System.Security.Cryptography;
using System.Text;
using dCMS.Order.Infrastructure.Shipping;
using Xunit;

namespace dCMS.Order.Tests.Shipping;

public sealed class CarrierWebhookVerifierTests
{
    [Fact]
    public void VerifyHmacSha256_accepts_sha256_prefix_and_compares_constant_time()
    {
        var secret = "super-secret";
        var body = """{"trackingNumber":"TN1","status":"DELIVERED","occurredAt":"2026-04-13T00:00:00Z"}""";
        var bytes = Encoding.UTF8.GetBytes(body);

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), bytes);
        var header = "sha256=" + Convert.ToHexString(expected).ToLowerInvariant();

        Assert.True(CarrierWebhookVerifier.VerifyHmacSha256(header, secret, bytes));
        Assert.False(CarrierWebhookVerifier.VerifyHmacSha256(header, "wrong", bytes));
        Assert.False(CarrierWebhookVerifier.VerifyHmacSha256("sha256=deadbeef", secret, bytes));
    }
}

