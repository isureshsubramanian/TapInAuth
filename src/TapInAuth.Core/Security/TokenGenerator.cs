using System.Security.Cryptography;

namespace TapInAuth.Core.Security;

/// <summary>
/// Cryptographically-strong generators for magic-link tokens and OTP codes.
/// </summary>
public static class TokenGenerator
{
    /// <summary>Generate a URL-safe base64 token of <paramref name="byteLength"/> random bytes (default 32).</summary>
    public static string GenerateMagicLinkToken(int byteLength = 32)
    {
        if (byteLength < 16)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength), "Minimum length is 16 bytes.");
        }
        var bytes = new byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Generate a numeric OTP of <paramref name="digits"/> length. Uses rejection sampling against a
    /// cryptographic RNG so each digit is uniformly distributed.
    /// </summary>
    public static string GenerateOtp(int digits = 6)
    {
        if (digits is < 4 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(digits), "Digits must be between 4 and 10.");
        }
        Span<char> code = stackalloc char[digits];
        Span<byte> buffer = stackalloc byte[1];
        for (var i = 0; i < digits; i++)
        {
            // Rejection sample to keep distribution uniform (0..9, reject 250..255 which would bias).
            byte b;
            do
            {
                RandomNumberGenerator.Fill(buffer);
                b = buffer[0];
            } while (b >= 250);
            code[i] = (char)('0' + (b % 10));
        }
        return new string(code);
    }

    /// <summary>Base64-URL encode without padding (RFC 4648 §5).</summary>
    public static string Base64UrlEncode(byte[] bytes)
    {
        var s = Convert.ToBase64String(bytes);
        return s.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>Base64-URL decode (RFC 4648 §5). Restores padding.</summary>
    public static byte[] Base64UrlDecode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var s = value.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
