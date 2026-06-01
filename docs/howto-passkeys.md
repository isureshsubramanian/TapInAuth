# How-to: passkeys

WebAuthn / FIDO2 passkeys via the `Fido2.AspNet` protocol library, wrapped with TapInAuth's tenant/store/audit/handoff scaffolding.

## Enable

```csharp
builder.Services.AddTapInAuth(o =>
{
    o.Methods |= TapInAuthMethod.Passkey;

    o.Relying.Id   = "yourdomain.com";        // the apex domain — passkeys are bound to this
    o.Relying.Name = "Your App";
    o.Relying.AllowedOrigins.Add("https://yourdomain.com");
    o.Relying.AllowedOrigins.Add("https://app.yourdomain.com");
})
.AddEfCoreStore<AppDbContext>()
.AddSmtpEmail(...);
```

## What's required

- **HTTPS or `localhost`.** Browsers refuse the WebAuthn API on plain HTTP.
- **A correct relying-party ID.** `Relying.Id` must be the apex domain or a registrable suffix of every origin you allow. `yourdomain.com` covers `www.yourdomain.com` and `app.yourdomain.com`. `app.yourdomain.com` does NOT cover `www.yourdomain.com`.
- **At least one allowed origin.** Each entry in `AllowedOrigins` must include the scheme and (if non-default) port.

## What ships

- `POST /auth/passkey/register/options` — start a registration ceremony (authenticated; current user adds a passkey).
- `POST /auth/passkey/register` — verify the attestation and store the credential.
- `POST /auth/passkey/assert/options` — start an assertion (sign-in) ceremony.
- `POST /auth/passkey/assert` — verify the assertion and hand off the principal to your cookie scheme.
- `GET  /auth/passkey/me` — JSON list of the signed-in user's passkeys.
- `POST /auth/passkey/{id}/revoke` — remove one.

Plus a JS helper at `/_content/TapInAuth.UI/tapinauth-passkey.js`:

```html
<script src="/_content/TapInAuth.UI/tapinauth-passkey.js"></script>
<script>
    await window.TapInAuth.registerPasskey({ deviceName: "Alice's iPhone" });
    await window.TapInAuth.signInWithPasskey({});
</script>
```

The "Sign in with a passkey" button on the built-in sign-in page is already wired to this.

## Conditional UI (autofill)

The built-in sign-in page already sets `autocomplete="email webauthn"` on the email input. Browsers that support conditional mediation will surface passkeys inline in the autofill dropdown as the user focuses the field. No extra setup.

## Multi-tenant relying-party

In a SaaS with per-tenant subdomains (`acme.yourdomain.com`), set the tenant's RP ID via `TenantContext.RelyingPartyId`. The default global `Relying.Id` is the fallback. Passkeys registered on Acme cannot be used on Globex — that's the WebAuthn protocol enforcing tenancy for you.

## Ceremony state

Passkey ceremonies require server-side challenge state. TapInAuth stores it in an **HMAC-signed, short-TTL cookie** via ASP.NET Core Data Protection — no Redis or session storage required. Cookie name is `tapin.auth.passkey.{register|assert}`, 5-minute TTL, `HttpOnly`, `SameSite=Lax`, `Secure` on HTTPS.

## Storage

Credentials live in `TapInAuthCredentials`:

| Column | Notes |
|---|---|
| `TenantId` + `CredentialId` | unique together; tenancy-isolated |
| `PublicKey` | COSE-encoded |
| `SignatureCounter` | monotonic; cloned-authenticator detection |
| `Aaguid` | authenticator model id (null for none-attestation) |
| `DeviceName` | user-friendly label, optional |
| `CreatedAt` / `LastUsedAt` | timestamps |

## Removing a passkey

```javascript
await fetch("/auth/passkey/" + id + "/revoke", { method: "POST", credentials: "same-origin" });
```

The endpoint verifies the credential belongs to the signed-in user before deleting (defense in depth — the global query would already prevent cross-user theft, but this is an explicit check).

## What's not (yet) shipped

- The "passkey + email fallback" combined sign-in flow as a single button — currently the sign-in page has a "Sign in with a passkey" button alongside the magic-link / OTP forms.
- AAGUID enrichment from the FIDO Metadata Service (so "YubiKey 5C NFC" shows as a friendly name instead of a Guid).
- Cross-device passkey QR-code flow — that's the browser's job today; the library just exposes assertion options.

These are post-1.0 enhancements. Today's coverage is enough for self-registered users and a real production sign-in.
