# Concepts: multi-tenancy

TapInAuth is **tenant-aware from day one**. Single-tenant apps don't think about it. SaaS apps get isolation, per-tenant branding, and per-tenant WebAuthn relying-party IDs without changing any storage code.

## The model

- Every store call (`ITapInAuthUserStore`, `ICredentialStore`, `IMagicLinkTokenStore`, `IOtpCodeStore`, `IRecoveryCodeStore`, `IAuditQuery`) takes a `TenantContext`.
- Every EF Core entity has a `TenantId` column with a filtered unique index on `(TenantId, …)`.
- A user with email `alice@acme.com` in tenant `acme` is a different record from `alice@acme.com` in tenant `globex`. Their passkeys, OTP records, magic-link tokens, recovery codes, and audit events are partitioned.

## Single-tenant apps

Do nothing. The default `NullTenantResolver` returns `TenantContext.Default` (a singleton with `Id="default"`). Every record gets stamped with `"default"`. You never see the column unless you go looking.

## Multi-tenant apps

Register a custom resolver:

```csharp
builder.Services.AddTapInAuth(...)
    .AddEfCoreStore<AppDbContext>()
    .AddTenantResolver<SubdomainTenantResolver>();
```

A resolver returns a `TenantContext` (or null → default). The built-in `SubdomainTenantResolver` reads the first DNS label (`acme.example.com` → `"acme"`). Or write your own to read a route segment, a host header, or a claim.

## Per-tenant branding

The `TenantContext` carries optional overrides:

```csharp
public sealed record TenantContext(
    string Id,
    string? DisplayName = null,
    string? RelyingPartyId = null,   // optional per-tenant WebAuthn RP id
    string? LogoPath = null,         // optional per-tenant logo
    string? ThemeAccent = null);     // optional per-tenant accent color
```

The TapInAuth UI layout consults `ITenantResolver` and uses these overrides when present, falling back to the global `TapInAuthOptions` defaults otherwise. So Acme's sign-in card shows Acme's logo in Acme's brand color; Globex's sign-in card shows Globex's. Same library, same code.

## Subdomain vs query-string resolution

Production: **subdomain only.** Cookies are naturally scoped per-subdomain, WebAuthn RP IDs are subdomain-specific, and tenant pollution is impossible across the wire.

Development: subdomain resolution requires editing `/etc/hosts` for every tenant. The `samples/SaaS.MultiTenant` sample shows a pattern that adds a `?tenant=` query-string fallback **gated behind `IHostEnvironment.IsDevelopment()`** so the override exists in dev only.

## Isolation guarantees

Whichever resolver you use, the store enforces tenant isolation at the query layer:

- `FindByEmailAsync(tenant, email)` → `WHERE TenantId = tenant.Id AND Email = email`. Returns null if no match within the tenant, even if the same email exists in another tenant.
- `MakeAssertionAsync` for passkeys is callable only when the credential's `TenantId` matches the request's resolved tenant.
- `ListRecentAsync` on the audit query is tenant-scoped — an admin in Acme cannot see Globex's audit events.

## Cross-tenant attempts

If a signed-in Acme user navigates to a Globex URL (e.g., `globex.app.example.com`), the cookie's `tapinauth:tenant=acme` claim differs from the resolved tenant. The `samples/SaaS.MultiTenant` sample ships a `TenantClaimGuardMiddleware` that detects this, signs the user out, and bounces them to the sign-in page for the new tenant. Drop it into your own pipeline if you want the same behavior.

## Self-service signup per tenant

`SecurityOptions.AllowSignUp` (default `true`) lets unknown emails self-provision a user in the resolved tenant on first magic-link / OTP request. For invite-only SaaS, set `AllowSignUp = false` and provision users via your own admin flow before they try to sign in. Unknown-email sign-in attempts are silently dropped (no email sent, no user created).

## Per-tenant WebAuthn relying-party

In SaaS deployments with subdomain-per-tenant (`acme.app.example.com`, `globex.app.example.com`), set `TenantContext.RelyingPartyId` to the tenant's subdomain. Passkeys registered for Acme cannot be used to sign in to Globex — the WebAuthn protocol binds credentials to their RP.

## Migration from single-tenant

You don't need to. The `default` tenant is already present in every record. If you decide to go multi-tenant later, an `UPDATE TapInAuthUsers SET TenantId = '…'` plus a resolver registration is the entire data migration.
