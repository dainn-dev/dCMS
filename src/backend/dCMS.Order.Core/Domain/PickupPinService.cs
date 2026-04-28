using System.Security.Cryptography;

namespace dCMS.Order.Core.Domain;

/// <summary>
/// DAI-696 — generate / hash / verify a 6-digit pickup PIN.
/// Hashes use PBKDF2-SHA256 with a per-PIN random salt; storage format is
/// <c>v1$&lt;iter&gt;$&lt;saltB64&gt;$&lt;hashB64&gt;</c>. Constant-time compare on verify.
/// </summary>
public static class PickupPinService
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    /// <summary>Generate a cryptographically random 6-digit PIN (zero-padded).</summary>
    public static string GeneratePin()
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        var n = BitConverter.ToUInt32(bytes) % 1_000_000u;
        return n.ToString("D6");
    }

    public static string Hash(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
            throw new ArgumentException("PIN is required.", nameof(pin));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"v1${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string pin, string? stored)
    {
        if (string.IsNullOrEmpty(pin) || string.IsNullOrEmpty(stored))
            return false;

        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "v1")
            return false;
        if (!int.TryParse(parts[1], out var iter) || iter <= 0)
            return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(pin, salt, iter, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
