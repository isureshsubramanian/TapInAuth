namespace TapInAuth.Options;

/// <summary>Endpoint and UI route configuration. All paths are relative to the application root.</summary>
public sealed class RoutesOptions
{
    /// <summary>The base path under which TapInAuth endpoints and UI live.</summary>
    public string BasePath { get; set; } = "/auth";

    /// <summary>The sign-in page path (under <see cref="BasePath"/>).</summary>
    public string SignIn { get; set; } = "/sign-in";

    /// <summary>The magic-link verification path (under <see cref="BasePath"/>).</summary>
    public string Verify { get; set; } = "/verify";

    /// <summary>The OTP entry path (under <see cref="BasePath"/>).</summary>
    public string Otp { get; set; } = "/otp";

    /// <summary>The SMS-OTP entry path (under <see cref="BasePath"/>).</summary>
    public string SmsOtp { get; set; } = "/sms-otp";

    /// <summary>The account page where users can manage credentials, recovery codes, and phone (under <see cref="BasePath"/>).</summary>
    public string Account { get; set; } = "/account";

    /// <summary>The "we sent you a link" landing path (under <see cref="BasePath"/>).</summary>
    public string MagicLinkSent { get; set; } = "/sent";

    /// <summary>The sign-out path (under <see cref="BasePath"/>).</summary>
    public string SignOut { get; set; } = "/sign-out";

    /// <summary>Where to redirect after a successful sign-in if no return URL is provided.</summary>
    public string DefaultReturnUrl { get; set; } = "/";
}
