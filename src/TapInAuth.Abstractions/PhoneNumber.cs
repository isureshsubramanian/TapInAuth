using System.Text;

namespace TapInAuth;

/// <summary>
/// Minimal E.164 phone-number normalization. We deliberately don't ship a full libphonenumber port —
/// that's a big dependency tree for a feature most apps need only for OTP. Hosts that need stricter
/// validation (region-aware, type-aware) can validate before calling <c>ITapInAuthUserStore.SetPhoneAsync</c>
/// and pass the canonical E.164 string we'll then store and use verbatim.
/// </summary>
public static class PhoneNumber
{
    /// <summary>
    /// Strip whitespace, dashes, parentheses, and dots; require a leading <c>+</c> and at least 8 digits.
    /// Returns true and emits the canonical form on success, false on rejection.
    /// </summary>
    public static bool TryNormalize(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var sb = new StringBuilder(raw.Length + 1);
        var sawPlus = false;
        var digitCount = 0;
        foreach (var c in raw)
        {
            if (c == '+' && sb.Length == 0)
            {
                sb.Append('+');
                sawPlus = true;
                continue;
            }
            if (char.IsDigit(c))
            {
                sb.Append(c);
                digitCount++;
            }
            // Any other character (whitespace, dash, paren, dot) is dropped silently.
        }

        // E.164 numbers are 8 to 15 digits after the country code; under 8 is almost certainly malformed.
        if (!sawPlus || digitCount < 8 || digitCount > 15)
        {
            return false;
        }

        normalized = sb.ToString();
        return true;
    }
}
