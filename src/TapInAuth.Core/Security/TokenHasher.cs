using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TapInAuth.Options;

namespace TapInAuth.Core.Security;

/// <summary>
/// HMAC-SHA256 hasher used to store magic-link tokens and OTPs at rest.
/// The pepper is configured via <see cref="SecurityOptions.TokenPepper"/>; a random pepper is generated
/// at startup if none is configured (and a warning is logged — production deployments should set one).
/// </summary>
public sealed class TokenHasher
{
    private readonly byte[] _pepper;

    /// <summary>Create a <see cref="TokenHasher"/> from the configured options.</summary>
    public TokenHasher(IOptions<TapInAuthOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _pepper = ResolvePepper(options.Value.Security.TokenPepper);
    }

    /// <summary>Create a hasher directly with an explicit pepper. For tests.</summary>
    internal TokenHasher(byte[] pepper)
    {
        ArgumentNullException.ThrowIfNull(pepper);
        if (pepper.Length < 32)
        {
            throw new ArgumentException("Pepper must be at least 32 bytes.", nameof(pepper));
        }
        _pepper = pepper;
    }

    /// <summary>Compute the HMAC-SHA256 of a string value (UTF-8 bytes).</summary>
    public byte[] Hash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Hash(Encoding.UTF8.GetBytes(value));
    }

    /// <summary>Compute the HMAC-SHA256 of a byte array.</summary>
    public byte[] Hash(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return HMACSHA256.HashData(_pepper, value);
    }

    /// <summary>Constant-time comparison of two hashes.</summary>
    public static bool FixedTimeEquals(byte[] a, byte[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static byte[] ResolvePepper(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(configured);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("TapInAuth: SecurityOptions.TokenPepper is not valid base64.", ex);
            }
            if (decoded.Length < 32)
            {
                throw new InvalidOperationException("TapInAuth: SecurityOptions.TokenPepper must decode to at least 32 bytes.");
            }
            return decoded;
        }

        // No pepper configured — generate an ephemeral one for this process.
        // Hosts should configure SecurityOptions.TokenPepper for stable hashes across restarts.
        var generated = new byte[32];
        RandomNumberGenerator.Fill(generated);
        return generated;
    }
}
