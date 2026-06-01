using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TapInAuth;
using TapInAuth.AspNetCore.DependencyInjection;
using TapInAuth.Email.Smtp.DependencyInjection;
using TapInAuth.Samples.BlazorServer.Components;
using TapInAuth.Samples.BlazorServer.Data;
using TapInAuth.Store.EntityFrameworkCore.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=tapinauth-blazor.db"));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/auth/sign-in";
        o.LogoutPath = "/auth/sign-out";
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddMail4Dev(o =>
{
    o.SmtpPort = 2525;
    o.EnableImap = true;
    o.ImapPort = 1143;
});

builder.Services.AddTapInAuth(options =>
    {
        options.Methods = TapInAuthMethod.Passkey | TapInAuthMethod.MagicLink | TapInAuthMethod.EmailOtp | TapInAuthMethod.RecoveryCode;
        options.Theme.Accent = "#7C3AED";          // purple to distinguish from the other samples
        options.FromEmail = "no-reply@tapinauth.local";
        options.FromDisplayName = "TapInAuth Blazor Sample";

        options.Relying.Id = "localhost";
        options.Relying.Name = "TapInAuth Blazor Sample";
        options.Relying.AllowedOrigins.Add("https://localhost:5301");
        options.Relying.AllowedOrigins.Add("http://localhost:5300");
    })
    .AddEfCoreStore<AppDbContext>()
    .AddEfCoreAuditSink<AppDbContext>()
    .AddSmtpEmail(smtp =>
    {
        smtp.Host = "localhost";
        smtp.Port = 2525;
        smtp.UseStartTls = false;
        smtp.FromAddress = "no-reply@tapinauth.local";
        smtp.FromDisplayName = "TapInAuth Blazor Sample";
    });

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseMail4Dev();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapTapInAuth();   // /auth/* HTTP endpoints

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(TapInAuth.UI.Blazor.Components.TapInAuthCard).Assembly);

app.Run();
