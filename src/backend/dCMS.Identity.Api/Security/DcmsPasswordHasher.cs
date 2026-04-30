using System.Security.Cryptography;
using System.Text;

namespace dCMS.Identity.Api.Security;

/// <summary>
/// PBKDF2-SHA256 password hasher. Encoded format: <c>pbkdf2_sha256$&lt;iters&gt;$&lt;salt-b64&gt;$&lt;hash-b64&gt;</c>.
/// 100k iterations (OWASP 2023 floor), 16-byte salt, 32-byte hash.
/// </summary>
public sealed class DcmsPasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const string Scheme = "pbkdf2_sha256";

    public string Hash(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentException("Password is required.", nameof(plaintext));

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var derived = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plaintext), salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"{Scheme}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(derived)}";
    }

    public bool Verify(string plaintext, string encoded)
    {
        if (string.IsNullOrWhiteSpace(plaintext) || string.IsNullOrWhiteSpace(encoded))
            return false;

        var parts = encoded.Split('$', StringSplitOptions.None);
        if (parts.Length != 4 || !string.Equals(parts[0], Scheme, StringComparison.Ordinal))
            return false;

        if (!int.TryParse(parts[1], out var iters) || iters < 10_000 || iters > 10_000_000)
            return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plaintext), salt, iters, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
