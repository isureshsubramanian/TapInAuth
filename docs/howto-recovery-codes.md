# How-to: recovery codes

Recovery codes are the rescue path for "I lost my phone / hardware key / passkey." A batch of one-time codes the user prints or saves, redeemable as a sign-in method.

## Enable

```csharp
builder.Services.AddTapInAuth(o =>
{
    o.Methods |= TapInAuthMethod.RecoveryCode;
    o.Security.RecoveryCodeCount  = 10;   // codes per batch (4–20, default 10)
    o.Security.RecoveryCodeLength = 10;   // characters per code (8–20, default 10)
})
.AddEfCoreStore<AppDbContext>();
```

## What ships

- `POST /auth/recovery/redeem` — public; takes `email` + `code` form fields, signs the user in on success.
- `POST /auth/recovery/regenerate` — authenticated; wipes the user's prior batch and returns the new plaintext codes **once**.
- `GET  /auth/recovery/count` — authenticated; returns `{ "remaining": N }`.
- `/auth/recovery` — built-in Razor Page for end-users to redeem a code (linked from the sign-in page).

## UX pattern

The standard flow your app needs to implement:

1. After the user adds a passkey or a second factor for the first time, prompt them to **Generate recovery codes**. POST to `/auth/recovery/regenerate`. Show the returned codes **once** — never persist them client-side, never email them.

2. On the sign-in page, expose a "Lost your device? Use a recovery code" link (already present in the built-in UI when the method is enabled). It routes to `/auth/recovery`.

3. On the user's account page, show how many codes remain (`GET /auth/recovery/count`) and offer a **Regenerate** button. Regeneration wipes prior codes and produces a fresh batch.

See [`samples/Mvc.Quickstart/Pages/Recovery.cshtml`](../samples/Mvc.Quickstart/Pages/Recovery.cshtml) for a complete management UI.

## Code format

Crockford-style alphabet (`23456789ABCDEFGHJKMNPQRSTVWXYZ`) — no `0/O/1/I/L` to avoid read-aloud confusion. A 10-character code is split at the midpoint with a hyphen for readability: `ABCDE-FGHJK`.

## Normalization

The redemption endpoint accepts mixed-case, with or without the hyphen, and with internal whitespace. Internally, codes are normalized to uppercase alphanumeric before hashing.

## Storage

`TapInAuthRecoveryCodes` table — `TenantId`, `UserId`, `CodeHash` (HMAC-SHA256, never raw), `CreatedAt`, `ConsumedAt`. Each code is single-use.

## Rate limiting

Recovery-redeem is rate-limited per-`(tenant, email)` via the configured `IRateLimiter`. Default: 10 attempts per 15-minute window. Tune via `Security.MaxSignInsPerWindow` / `Security.RateLimitWindow`.

## Operational notes

- A user with **zero** recovery codes can only sign in via their other registered methods. If they've lost all of them, an admin needs to provision a sign-in some other way (e.g., manually verify identity, send a magic link out-of-band, then have the user regenerate from the account page).
- Regeneration wipes ALL prior codes — there's no "add a code" operation. The model is "one active batch."
- The audit feed records every regenerate and every redemption (`AuditEventType.CredentialRegistered` for regenerate, `AuditEventType.OtpVerified` for redeem).
