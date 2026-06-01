using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TapInAuth;
using TapInAuth.AspNetCore.DependencyInjection;
using TapInAuth.Email.Smtp.DependencyInjection;
using TapInAuth.Samples.Mvc.Auth;
using TapInAuth.Samples.Mvc.Data;
using TapInAuth.Store.EntityFrameworkCore.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// ─── Host's own DbContext ──────────────────────────────────────────────────
// Could be anything. The TapInAuth entities are mapped alongside via ApplyTapInAuthConfiguration().
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=tapinauth-sample.db"));

// ─── Standard ASP.NET Core cookie authentication ───────────────────────────
// TapInAuth hands off to this scheme — it does NOT issue its own session cookie.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/auth/sign-in";
        o.LogoutPath = "/auth/sign-out";
        o.AccessDeniedPath = "/auth/sign-in";
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
    });

// Sample-only: hand-pick which emails get the TapInAuth admin role (drives the /auth/admin gate).
builder.Services.Configure<SampleAuthOptions>(builder.Configuration.GetSection(SampleAuthOptions.SectionName));
builder.Services.AddScoped<IClaimsTransformation, AdminRoleClaimsTransformation>();

// ─── Hermex: in-process dev SMTP + IMAP server + dashboard ────────────────
// This runs the SMTP server on localhost:2525 and a web dashboard at /hermex
// so you can see the magic-link and OTP emails this sample sends rendered in
// the browser — no Mailtrap account, no Docker container, nothing to install.
builder.Services.AddMail4Dev(o =>
{
    o.SmtpPort = 2525;
    o.EnableImap = true;        // also expose the captured mail over IMAP at localhost:1143
    o.ImapPort = 1143;
});

// ─── TapInAuth ─────────────────────────────────────────────────────────────
// Five-line setup: methods, theming, store, email provider.
// The SMTP sender below points at Hermex (localhost:2525). In production swap
// the Host/Port for your real provider (SendGrid SMTP, AWS SES SMTP, etc.) or
// replace .AddSmtpEmail(...) with the dedicated provider sub-package.
builder.Services.AddTapInAuth(options =>
    {
        options.Methods = TapInAuthMethod.Passkey | TapInAuthMethod.MagicLink | TapInAuthMethod.EmailOtp | TapInAuthMethod.RecoveryCode;
        options.Theme.Accent = "#2563EB";
        options.FromEmail = "no-reply@tapinauth.local";
        options.FromDisplayName = "TapInAuth Sample";

        // WebAuthn relying-party info. For local dev, the RP id is the host *without* the port,
        // and the origins must list the full URL(s) the browser uses.
        options.Relying.Id = "localhost";
        options.Relying.Name = "TapInAuth Sample";
        options.Relying.AllowedOrigins.Add("https://localhost:5001");
        options.Relying.AllowedOrigins.Add("http://localhost:5000");
    })
    .AddEfCoreStore<AppDbContext>()
    .AddEfCoreAuditSink<AppDbContext>()    // persist audit events to TapInAuthAuditEvents — feeds /auth/admin
    .AddSmtpEmail(smtp =>
    {
        smtp.Host = "localhost";
        smtp.Port = 2525;
        smtp.UseStartTls = false;     // Hermex defaults to plain (TLS optional)
        smtp.UseImplicitTls = false;
        smtp.FromAddress = "no-reply@tapinauth.local";
        smtp.FromDisplayName = "TapInAuth Sample";
    });

builder.Services.AddRazorPages();

var app = builder.Build();

// Sample-only: create the SQLite schema on first run. In real apps use migrations.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Mount Hermex dashboard + SMTP/IMAP listeners.
app.UseMail4Dev();

// Serves both the host's wwwroot AND any referenced RCL's _content/{PackageId}/... assets
// via the in-memory static-web-assets registry. Avoid MapStaticAssets() here — its dev-mode
// runtime handler expects RCL files to be physically copied into {host}/wwwroot/_content/...
// which doesn't happen on a fresh clean build and produces FileNotFoundException at request time.
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();             // Hosts the sample's Razor Pages (Home + Passkeys) AND
                                 // the TapInAuth.UI pages (sign-in, magic-link sent, OTP).
app.MapTapInAuth();              // Mounts /auth/* endpoints.

app.Run();
