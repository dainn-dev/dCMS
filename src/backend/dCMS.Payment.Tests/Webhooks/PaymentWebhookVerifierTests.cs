using System.Text;
using dCMS.Payment.Infrastructure.Webhooks;

namespace dCMS.Payment.Tests.Webhooks;

public sealed class PaymentWebhookVerifierTests
{
    [Fact]
    public void VerifyHmacSha256_accepts_hex_with_or_without_prefix()
    {
        const string secret = "whsec_test";
        var body = Encoding.UTF8.GetBytes("{\"a\":1}");
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hex = Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
        var header = "sha256=" + hex;

        Assert.True(PaymentWebhookVerifier.VerifyHmacSha256(header, secret, body));
        Assert.True(PaymentWebhookVerifier.VerifyHmacSha256(hex, secret, body));
        Assert.False(PaymentWebhookVerifier.VerifyHmacSha256(header, "wrong", body));
        Assert.False(PaymentWebhookVerifier.VerifyHmacSha256("sha256=deadbeef", secret, body));
    }
}
