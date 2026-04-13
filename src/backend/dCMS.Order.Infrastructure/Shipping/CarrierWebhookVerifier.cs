using System.Security.Cryptography;
using System.Text;

namespace dCMS.Order.Infrastructure.Shipping;

public static class CarrierWebhookVerifier
{
    public static bool VerifyHmacSha256(string? signatureHeader, string secret, ReadOnlySpan<byte> bodyBytes)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret))
            return false;

        var sig = signatureHeader.Trim();
        if (sig.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            sig = sig["sha256=".Length..];

        if (!TryHexToBytes(sig, out var provided))
            return false;

        var key = Encoding.UTF8.GetBytes(secret);
        var computed = HMACSHA256.HashData(key, bodyBytes);
        return CryptographicOperations.FixedTimeEquals(provided, computed);
    }

    private static bool TryHexToBytes(string hex, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(hex))
            return false;

        hex = hex.Trim();
        if (hex.Length % 2 != 0)
            return false;

        try
        {
            bytes = Convert.FromHexString(hex);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

