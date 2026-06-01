# TapInAuth — SaaS Multi-Tenant Sample

Demonstrates **tenant-aware** TapInAuth: a single deployment serving three tenants (Acme, Globex, Initech), each with its own logo, theme accent, and isolated user table.

## Run it

```bash
dotnet run --project samples/SaaS.MultiTenant
```

Then open:

- https://localhost:5101?tenant=acme    — Acme branding
- https://localhost:5101?tenant=globex  — Globex branding
- https://localhost:5101?tenant=initech — Initech branding
- https://localhost:5101/hermex          — the Hermex inbox (shared across tenants)

Each tenant has an independent user store: an `acme` user with email `alice@acme.com` is invisible to the `globex` tenant.

## How tenancy works

- `CatalogTenantResolver` resolves the current tenant from the first DNS label (`acme.localhost`) or the `?tenant=` query string for single-port dev.
- TapInAuth's EF Core store filters every query by `TenantId` and enforces `(TenantId, Email)` as unique. Two `alice@acme.com` users in different tenants are allowed; two in the same tenant are not.
- WebAuthn relying-party IDs (when passkeys land in 0.3) will be per-tenant via `TenantContext.RelyingPartyId`.

## Production deployment

In production you'd:
- Map `acme.app.example.com`, `globex.app.example.com`, etc. (each tenant a subdomain).
- Set the cookie domain to `.app.example.com` so the host cookie is shared, but the relying-party ID stays per-subdomain (passkey isolation).
- Replace `InMemoryTenantCatalog` with a database-backed catalog and a logo CDN per tenant.
