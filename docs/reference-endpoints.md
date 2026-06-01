# Reference: endpoints

Every HTTP endpoint `MapTapInAuth()` mounts, scoped to `RoutesOptions.BasePath` (default `/auth`).

## Magic link

| Method | Path | Body | Returns |
|---|---|---|---|
| `POST` | `/auth/magic-link` | form: `email`, optional `returnUrl` | 302 → `/auth/sent?email=…` (or `&rate=1` if rate-limited, `&err=delivery` on send failure) |
| `GET` | `/auth/verify` | query: `id`, `t`, optional `tenant` | 302 → return URL (success) or `/auth/sign-in?error=…` (failure). If already authenticated and the token is just consumed, silent redirect home. |

The magic-link URL embedded in emails is `{Origin}{BasePath}/verify?id={tokenId}&t={token}` plus `&tenant={id}` when the issuing tenant is non-default.

## Email OTP

| Method | Path | Body | Returns |
|---|---|---|---|
| `POST` | `/auth/otp/request` | form: `email` | 302 → `/auth/otp?email=…` |
| `POST` | `/auth/otp/verify` | form: `email`, `code` | 302 → return URL or sign-in with error |

## Recovery codes — only when `RecoveryCode` flag is enabled

| Method | Path | Auth | Body | Returns |
|---|---|---|---|---|
| `POST` | `/auth/recovery/redeem` | public | form: `email`, `code` | 302 → return URL (success) or `/auth/recovery?error=invalid&email=…` |
| `POST` | `/auth/recovery/regenerate` | authenticated | — | 200 JSON: `{ "codes": [ "ABCDE-FGHJK", … ] }` (shown once) |
| `GET` | `/auth/recovery/count` | authenticated | — | 200 JSON: `{ "remaining": N }` |

## Passkeys — only when `Passkey` flag is enabled

| Method | Path | Auth | Body | Returns |
|---|---|---|---|---|
| `POST` | `/auth/passkey/register/options` | authenticated | — | 200 JSON: `CredentialCreateOptions` for `navigator.credentials.create()` |
| `POST` | `/auth/passkey/register` | authenticated | JSON: `AuthenticatorAttestationRawResponse`; optional query `?deviceName=` | 200 JSON: `{ id, deviceName, createdAt }` or 400 `verification_failed` |
| `POST` | `/auth/passkey/assert/options` | public | — | 200 JSON: `AssertionOptions` for `navigator.credentials.get()` |
| `POST` | `/auth/passkey/assert` | public | JSON: `AuthenticatorAssertionRawResponse` | 200 JSON: `{ redirect: "…" }` (cookie set) or 401 |
| `GET` | `/auth/passkey/me` | authenticated | — | 200 JSON: `[{ id, deviceName, createdAt, lastUsedAt }]` |
| `POST` | `/auth/passkey/{id:guid}/revoke` | authenticated | — | 200 JSON: `{ revoked: <id> }` or 404 if not owned |

## Sign-out

| Method | Path | Returns |
|---|---|---|
| `POST` | `/auth/sign-out` | 302 → `/auth/sign-in` (preferred — CSRF-safe via form post) |
| `GET` | `/auth/sign-out` | 302 → `/auth/sign-in` (convenience link form) |

## Admin (built-in Razor Pages — require `"TapInAuth.Admin"` policy)

| Method | Path | Notes |
|---|---|---|
| `GET` | `/auth/admin` | Overview with 24h counters + 10 most recent events |
| `GET` | `/auth/admin/audit` | Last 200 audit events |

## UI pages

| Path | Notes |
|---|---|
| `/auth/sign-in` | Sign-in page (passkey / magic-link / OTP / recovery, conditional on `Methods` flags) |
| `/auth/sent` | "Check your inbox" landing |
| `/auth/otp` | OTP entry |
| `/auth/recovery` | Recovery-code redemption |

## Tenant query convention

Whenever an endpoint redirects to another TapInAuth-internal page, the resolved tenant is preserved on the redirect URL via `?tenant={id}` (omitted when tenant is `"default"`). Forms in the built-in UI also embed the resolved tenant in their `action` URLs so a POST resolves to the same tenant as the GET that rendered it. Custom tenant resolvers should support reading the `tenant` query parameter (or a request-scoped equivalent) for this round trip to work.

## Headers / status codes

All endpoints set `Cache-Control: no-store` implicitly via the redirect responses. Errors return `400 Bad Request` with a JSON body `{ "error": "code" }` for diagnostic codes — they're stable across versions:

- `email_required`, `missing_fields`, `invalid_request`, `invalid_payload`, `no_email_claim`, `user_not_found`, `ceremony_expired`, `verification_failed`.
