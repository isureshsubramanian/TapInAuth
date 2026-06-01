# Security Policy

## Reporting a Vulnerability

**Please do not file public GitHub issues for security vulnerabilities.**

Send a private report to **security@tapinauth.io** with:

- A description of the issue and its impact
- Steps to reproduce (or a proof-of-concept)
- The TapInAuth version and .NET TFM
- Your contact info (so we can credit you, if you wish)

We aim to:

- Acknowledge receipt within **3 business days**.
- Provide an initial assessment within **7 business days**.
- Coordinate disclosure and ship a fix in a patch release.

We are happy to credit reporters in the release notes and in our hall of fame.

## Supported Versions

| Version | Supported |
|---------|-----------|
| 0.x     | Yes (active development) |

Once we ship 1.0, we will maintain the latest two minor versions for security patches.

## Threat Model (summary)

TapInAuth is a passwordless authentication library. We treat the following as in-scope threats:

- **Credential theft**: passkeys are bound to the relying-party origin; magic-link and OTP tokens are stored hashed (HMAC-SHA256 with a per-instance pepper) and never returned to the host as plaintext after issuance.
- **Replay**: every token is single-use, short-TTL (magic link 10 min default, OTP 5 min default).
- **Brute force**: per-identifier and per-IP rate limits are enforced via `Microsoft.AspNetCore.RateLimiting`. Failed verifications use constant-time comparison.
- **Phishing**: passkeys (in 0.3+) are inherently phishing-resistant. For magic link / OTP we recommend pairing with bot defense (e.g., Cloudflare Turnstile) — a hook ships in 0.5.
- **CSRF**: all POST endpoints require antiforgery. Cookies default to `SameSite=Lax` (`Strict` for sensitive flows).
- **Multi-tenant data leakage**: every store query is partitioned by `TenantId`. EF Core global query filters enforce isolation; integration tests cover the cross-tenant case.

Out of scope (the host application's responsibility):

- TLS termination
- Email/SMS delivery and deliverability (we abstract the senders)
- Session cookie security (TapInAuth hands off to the host's existing auth scheme — the host owns cookie name, lifetime, SameSite, signing keys)
- Account-takeover signals beyond what `IRiskSignalProvider` exposes

A full threat model lives at [`docs/threat-model.md`](docs/threat-model.md) (to be added before 1.0).

## Disclosure Policy

We follow coordinated disclosure. Once a fix is ready, we publish a CVE, ship the patch, and document the issue. We will not embargo a fix for longer than 90 days from the initial report without your agreement.
