# How-to: SMS sign-in

TapInAuth treats phone as a **secondary identifier** in v1.0: a user must first register with email (via magic-link or email-OTP), then attach a verified phone number from the account page. Once verified, that user can sign in by phone going forward.

There is no phone-only signup path — see the rationale in the [SmsOtpService XML docs](https://github.com/tapinauth/tapinauth/blob/main/src/TapInAuth.Core/Services/SmsOtpService.cs).

## Enable the method

```csharp
using TapInAuth.Sms.Twilio.DependencyInjection;

builder.Services.AddTapInAuth(opts =>
{
    opts.Methods = TapInAuthMethod.MagicLink
                 | TapInAuthMethod.EmailOtp
                 | TapInAuthMethod.SmsOtp;
})
.AddEfCoreStore<AppDbContext>()
.AddSmtpEmail(builder.Configuration.GetSection("Smtp"))
.AddTwilioSms(builder.Configuration.GetSection("TapInAuth:Twilio"));
```

```json
"TapInAuth": {
  "Twilio": {
    "AccountSid": "AC...",
    "AuthToken":  "REDACTED",
    "FromNumber": "+14155550100"
  }
}
```

Without an `ISmsSender` registration, the `/auth/sms/request` endpoint will throw a DI error at request time. If you want to test the wiring without Twilio, swap in a no-op `ISmsSender` for development.

## What the user sees

A new **Phone** field appears on the sign-in page under the existing email options:

```
Sign in
─────────────
[ email field ] [ Email me a link ]
or use phone
[ phone field ] [ Text me a code ]
```

The flow:

1. User enters phone, submits → `POST /auth/sms/request`.
2. If the phone is registered to a user in the tenant, an OTP is sent. Unknown phones are silently dropped (no enumeration oracle).
3. Always redirects to `/auth/sms-otp?phone=...`.
4. User enters the code → `POST /auth/sms/verify`.
5. On success, the host's cookie handoff signs them in.

## Account-page phone management

The user needs an existing email session to attach a phone. The built-in `/auth/account` page renders an **Add phone** form when no phone is set, and a **Send verification code / Remove** pair when one is set.

Endpoints behind it:

| Endpoint                          | What it does                                                   |
| --------------------------------- | -------------------------------------------------------------- |
| `POST /auth/account/phone/set`    | Normalize + store phone, issue OTP, redirect to verify page    |
| `POST /auth/account/phone/clear`  | Remove the phone and reset the verified flag                   |
| `POST /auth/sms/request`          | Re-send a verification code for the stored phone               |
| `POST /auth/sms/verify`           | Verify the OTP — marks phone verified and signs the user in    |

Setting a phone resets `PhoneVerified` to false — re-verification is mandatory on every change. The store enforces a tenant-scoped unique constraint on `(TenantId, Phone)` so two users in the same tenant can't claim the same number.

## Claims emitted

When a user signs in by SMS (or signs in by any other method while having a phone on file), the `ClaimsPrincipal` carries:

| Claim type                              | Value                                  |
| --------------------------------------- | -------------------------------------- |
| `ClaimTypes.MobilePhone`                | E.164 phone (e.g. `+14155550100`)      |
| `tapinauth:phone_number`                | Same value, TapInAuth-namespaced alias |
| `tapinauth:phone_verified`              | `"true"` / `"false"`                   |
| `tapinauth:amr`                         | `"smsotp"` when signed in by SMS       |

## Phone format

We accept `+`, digits, and the cosmetic separators `space`, `-`, `(`, `)`, `.` — everything else is rejected. The stored form is `+` followed by digits only (`+14155550100`). If your audience expects national-format input, normalize on your end before calling `ITapInAuthUserStore.SetPhoneAsync`.

We deliberately don't ship a full libphonenumber port — that's a big dependency tree for what most apps only need for OTP. For region-aware validation, use libphonenumber-csharp in the host app and pass us the canonical E.164.

## Security model

Same defenses as email OTP:

- Per-phone rate limit on `IssueAsync` and `VerifyAsync` (uses the same `IRateLimiter` as email).
- Per-OTP attempt counter (`Security.MaxOtpAttempts`) — exhausted attempts consume the code.
- HMAC-SHA256 hash of the code is stored, never the raw code; constant-time compare on verify.
- Single-use: a code is consumed on the first successful verification.
- Unknown phones return the same redirect as known ones — no enumeration leak.
