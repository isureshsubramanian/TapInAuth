# Reference: options

Every knob on `TapInAuthOptions`.

## `TapInAuthOptions`

```csharp
public class TapInAuthOptions
{
    public const string SectionName = "TapInAuth";

    public TapInAuthMethod Methods { get; set; }    // [Flags]; default: MagicLink | EmailOtp
    public LogoOptions     Logo    { get; }
    public ThemeOptions    Theme   { get; }
    public RoutesOptions   Routes  { get; }
    public SecurityOptions Security{ get; }
    public RpInfo          Relying { get; }         // WebAuthn relying-party
    public TelemetryOptions Telemetry { get; }

    public string? FromEmail        { get; set; }
    public string? FromDisplayName  { get; set; }
}
```

## `TapInAuthMethod` flags

| Flag | Notes |
|---|---|
| `Passkey` | WebAuthn / FIDO2 |
| `MagicLink` | Email magic link |
| `EmailOtp` | 6-digit email OTP |
| `SmsOtp` | SMS OTP (sender ships; full sign-in flow pending) |
| `RecoveryCode` | Single-use recovery codes |
| `All` | All of the above |

## `LogoOptions`

| Field | Default | Notes |
|---|---|---|
| `Path` | `null` | Relative path under `wwwroot/`. SVG preferred. |
| `DarkPath` | `null` | Optional separate dark-mode logo. |
| `MaxWidthPx` | `240` | Max rendered width. |
| `MaxHeightPx` | `80` | Max rendered height. |
| `AltText` | `null` | Defaults to tenant display name or "Logo". |

## `ThemeOptions`

| Field | Default | Notes |
|---|---|---|
| `Accent` | `#2563EB` | Required; primary brand color. |
| `BackgroundDark` | `#0B0F19` | Dark-mode page bg. |
| `SurfaceDark` | `#111827` | Dark-mode card bg. |
| `BackgroundLight` | `#F9FAFB` | Light-mode page bg. |
| `SurfaceLight` | `#FFFFFF` | Light-mode card bg. |
| `Radius` | `14px` | Button/input radius. |
| `CardRadius` | `18px` | Card/panel radius. |
| `FontFamily` | Inter + system stack | CSS font-family. |
| `MonoFontFamily` | JetBrains Mono + system | For code-like text. |
| `Mode` | `Auto` | `Auto` / `Light` / `Dark`. |

## `RoutesOptions`

| Field | Default | Notes |
|---|---|---|
| `BasePath` | `/auth` | Prefix for every TapInAuth endpoint. |
| `SignIn` | `/sign-in` | Path under base. |
| `Verify` | `/verify` | Magic-link verification. |
| `Otp` | `/otp` | OTP entry page. |
| `MagicLinkSent` | `/sent` | "Check your inbox" landing. |
| `SignOut` | `/sign-out` | Sign-out endpoint. |
| `DefaultReturnUrl` | `/` | Where to land after sign-in if no return URL. |

## `SecurityOptions`

| Field | Default | Notes |
|---|---|---|
| `MagicLinkLifetime` | `10 min` | TTL for issued magic links. |
| `OtpLifetime` | `5 min` | TTL for OTP codes. |
| `OtpDigits` | `6` | 4–10. |
| `MaxOtpAttempts` | `5` | After this many wrong tries, the OTP is invalidated. |
| `TokenPepper` | `null` (random per process) | Base64-encoded ≥32 bytes. Set in production for stable hashes across restarts. |
| `MaxSignInsPerWindow` | `10` | Per-identifier sign-in attempts in the window. |
| `MaxMagicLinkIssuancesPerWindow` | `5` | Per-identifier magic-link sends. |
| `MaxOtpIssuancesPerWindow` | `5` | Per-identifier OTP sends. |
| `RateLimitWindow` | `15 min` | Rolling window for the rate limiter. |
| `AllowSignUp` | `true` | Unknown email on first sign-in creates a user (set false for invite-only). |
| `PurgeInterval` | `1 hr` | Background cleanup of expired tokens. |
| `RecoveryCodeCount` | `10` | Codes per regenerated batch (4–20). |
| `RecoveryCodeLength` | `10` | Characters per code (8–20). |
| `AdminRole` | `TapInAuthAdmin` | Role required for `/auth/admin`. |

## `RpInfo`

| Field | Default | Notes |
|---|---|---|
| `Id` | `null` | WebAuthn relying-party ID. Set to your apex domain. |
| `Name` | `TapInAuth` | Shown to the user during passkey ceremonies. |
| `AllowedOrigins` | empty | Each origin you allow (scheme + host + optional port). |

## `TelemetryOptions`

| Field | Default | Notes |
|---|---|---|
| `Enabled` | `false` | Opt-in. Anonymous, aggregate counters only. |
| `EndpointUrl` | `telemetry.tapinauth.io/ingest` | Where to POST counters. |
| `FlushInterval` | `24h` | Batching cadence. |

Honors `DOTNET_CLI_TELEMETRY_OPTOUT`, `DO_NOT_TRACK`, and `TAPIN_TELEMETRY=0` env vars.

## Bind from configuration

```csharp
builder.Services.AddTapInAuth(builder.Configuration.GetSection(TapInAuthOptions.SectionName));
```

```json
{
  "TapInAuth": {
    "Methods": "Passkey, MagicLink, EmailOtp, RecoveryCode",
    "FromEmail": "no-reply@yourdomain.com",
    "FromDisplayName": "Your App",
    "Theme": { "Accent": "#2563EB", "Mode": "Auto" },
    "Logo":  { "Path": "img/logo.svg" },
    "Relying": { "Id": "yourdomain.com", "Name": "Your App", "AllowedOrigins": [ "https://yourdomain.com" ] },
    "Security": { "TokenPepper": "{base64}", "AllowSignUp": false, "AdminRole": "Acme.Admin" }
  }
}
```
