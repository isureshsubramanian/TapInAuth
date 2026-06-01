# How-to: theming

TapInAuth ships a CSS design-token system. You set an accent color and a logo; everything else (cards, buttons, inputs, dividers, focus rings, error banners, dark/light mode) is computed from those. Override any of it without forking.

## Quick win

```csharp
builder.Services.AddTapInAuth(o =>
{
    o.Theme.Accent  = "#2563EB";                     // your brand
    o.Logo.Path     = "wwwroot/img/your-logo.svg";   // SVG preferred
    o.Theme.Mode    = ThemeMode.Auto;                // Auto / Light / Dark
});
```

That's enough to brand the entire sign-in flow.

## Design tokens

The full list, exposed as CSS custom properties so any host page can consume them by linking `/_content/TapInAuth.UI/tapinauth-theme.css`:

| Token | Default (light) | Default (dark) | Notes |
|---|---|---|---|
| `--tap-accent` | `#2563EB` | same | Buttons, focus rings, links |
| `--tap-radius` | `14px` | same | Buttons / inputs |
| `--tap-card-radius` | `18px` | same | Cards / panels |
| `--tap-font` | Inter, system stack | same | Set in options |
| `--tap-bg` | `#F9FAFB` | `#0B0F19` | Page background |
| `--tap-surface` | `#FFFFFF` | `#111827` | Card / panel surface |
| `--tap-text` | `#111827` | `#F9FAFB` | Primary text |
| `--tap-text-muted` | `#6B7280` | `#9CA3AF` | Secondary text |
| `--tap-border` | `#E5E7EB` | `#1F2937` | Borders / dividers |
| `--tap-input-bg` | `#FFFFFF` | `#0F172A` | Input fields |
| `--tap-shadow` | subtle | deeper | Card shadow |
| `--tap-success` / `--tap-danger` | green / red | adjusted | Status colors |

All token defaults live in `tapinauth-theme.css`. The auth layout writes per-host overrides inline (so options-controlled tokens always take effect) and merges with the user's preference from `tapinauth-theme.js`.

## Dark / light / auto

`TapInAuthOptions.Theme.Mode`:

- `Auto` (default) — follows `prefers-color-scheme` from the user agent.
- `Light` — forced light regardless of OS.
- `Dark` — forced dark.

A per-user toggle ships in the corner of the auth card. It cycles Auto → Light → Dark and persists in `localStorage`. Apply it before paint to avoid a flash:

```html
<script>
    (function () {
        try {
            var saved = localStorage.getItem("tapinauth.theme");
            if (saved === "light" || saved === "dark" || saved === "auto") {
                document.documentElement.setAttribute("data-theme", saved);
            }
        } catch (_) {}
    })();
</script>
```

(Both built-in layouts already do this. Copy it into your own pages if you want them to share the toggle state.)

## Logo handling

`Logo.Path` is a relative path to an asset in your `wwwroot/`. SVG is strongly preferred (resolution-independent, can use `currentColor` to inherit the brand color, scales cleanly in the card and the app-bar). The auth card constrains it to `MaxWidthPx` (240) × `MaxHeightPx` (80) with `object-fit: contain`.

For multi-tenant deployments, set `TenantContext.LogoPath` in your `ITenantResolver` to override per tenant. The auth layout picks tenant > global > none.

## Customizing beyond tokens

The shared CSS is in `tapinauth-theme.css`. Override any class in your host's stylesheet:

```css
:root {
    --tap-radius: 4px;          /* sharper corners */
    --tap-font: 'IBM Plex Sans', sans-serif;
}
.tap-card { padding: 56px; }    /* roomier card */
.tap-btn  { letter-spacing: 0.05em; text-transform: uppercase; }
```

Load your override AFTER the TapInAuth stylesheet. Since per-host inline overrides are emitted in the layout's `<head>`, you can still beat them with `!important` if you must, but most layouts won't need to.

## Using the design system in your own pages

The `samples/Mvc.Quickstart/Pages/Shared/_Layout.cshtml` and `samples/SaaS.MultiTenant/Pages/Shared/_Layout.cshtml` show how to:

1. `<link>` the shared CSS.
2. Emit the same `:root { --tap-*: ... }` block the auth layout uses so your app pages match the sign-in card.
3. Include the theme-toggle JS so user preference syncs across pages.

Copy those files into your host and you have a consistent design system from sign-in through your dashboard with zero per-page work.
