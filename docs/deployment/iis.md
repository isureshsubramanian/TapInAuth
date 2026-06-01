# Deploy TapInAuth to IIS (on-prem Windows)

For on-prem .NET shops still on Windows Server + IIS.

## Prerequisites

- Windows Server 2022 (or Windows 11 for dev).
- IIS with the **ASP.NET Core Hosting Bundle** for .NET 10.
- SQL Server (Express, Standard, or Azure SQL via private endpoint).
- A TLS certificate (corporate CA or Let's Encrypt via win-acme).

## 1. Install the ASP.NET Core Hosting Bundle

Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) → ASP.NET Core Runtime → "Hosting Bundle". Install it on every web tier server. This adds the IIS module that proxies requests to a Kestrel process per app pool.

After install, restart IIS:

```powershell
net stop was /y
net start w3svc
```

## 2. Publish

From your build server / dev machine:

```powershell
dotnet publish samples\Mvc.Quickstart -c Release -o C:\publish\tapinauth
```

Copy `C:\publish\tapinauth\*` to the IIS server, e.g., `C:\inetpub\tapinauth`.

## 3. Create the IIS site

```powershell
Import-Module WebAdministration
New-WebAppPool -Name "tapinauth"
Set-ItemProperty "IIS:\AppPools\tapinauth" -Name managedRuntimeVersion -Value ""   # No managed runtime — ASP.NET Core is out-of-process
Set-ItemProperty "IIS:\AppPools\tapinauth" -Name processModel.identityType -Value ApplicationPoolIdentity

New-Website -Name "tapinauth" -PhysicalPath "C:\inetpub\tapinauth" -ApplicationPool "tapinauth" `
            -HostHeader "app.yourdomain.com" -Port 443 -Ssl
```

Bind the certificate in the IIS Manager → Site Bindings.

## 4. App pool permissions

Give the app pool identity NTFS read on `C:\inetpub\tapinauth` and (if you use a SQL Server in Windows-auth mode) DB access:

```sql
-- On the SQL instance:
CREATE LOGIN [IIS APPPOOL\tapinauth] FROM WINDOWS;
USE tapinauth;
CREATE USER [IIS APPPOOL\tapinauth] FOR LOGIN [IIS APPPOOL\tapinauth];
ALTER ROLE db_datareader ADD MEMBER [IIS APPPOOL\tapinauth];
ALTER ROLE db_datawriter ADD MEMBER [IIS APPPOOL\tapinauth];
ALTER ROLE db_ddladmin   ADD MEMBER [IIS APPPOOL\tapinauth];
```

## 5. Configuration

In `C:\inetpub\tapinauth\appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Server=sql01;Database=tapinauth;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Smtp": {
    "Host": "smtp.corp.yourcompany.com",
    "Port": 25,
    "UseStartTls": false,
    "FromAddress": "no-reply@yourcompany.com"
  },
  "TapInAuth": {
    "Methods": "Passkey, MagicLink, EmailOtp, RecoveryCode",
    "Relying": { "Id": "app.yourcompany.com", "AllowedOrigins": [ "https://app.yourcompany.com" ] }
  }
}
```

The **token pepper** should NOT live in this file. Use Windows DPAPI via the data-protection API, or pull from an enterprise vault on startup.

For DPAPI:

```csharp
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"C:\inetpub\tapinauth-keys"))
    .ProtectKeysWithDpapi();
```

## 6. Database migration

EF script approach works well in enterprise CI/CD:

```powershell
dotnet ef migrations script --idempotent -o migrate.sql --project src\Your.Web
sqlcmd -S sql01 -d tapinauth -E -i migrate.sql
```

## 7. HTTP / HTTPS redirect

In `web.config` (the published output already includes it; tweak as needed):

```xml
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="HTTP to HTTPS" stopProcessing="true">
          <match url=".*" />
          <conditions><add input="{HTTPS}" pattern="off" /></conditions>
          <action type="Redirect" url="https://{HTTP_HOST}/{R:0}" redirectType="Permanent" />
        </rule>
      </rules>
    </rewrite>
  </system.webServer>
</configuration>
```

## 8. Smoke test

Browse to `https://app.yourdomain.com/auth/sign-in`. Sign in via magic link. Email should land in your corporate SMTP relay (or via your SMTP provider).

## Operational notes

- **App pool recycle**: don't recycle on a fixed schedule unless you've persisted the data-protection keys to disk + DPAPI; otherwise every recycle invalidates all cookies.
- **Out-of-process**: the IIS module starts a child `dotnet` process. Logs land in `C:\inetpub\tapinauth\logs\stdout_*.log` when `stdoutLogEnabled=true` in web.config. Tail those during initial deployment troubleshooting.
- **Health checks**: hit `/healthz` (you'll need to add the endpoint) from your monitoring system.
- **Audit log retention**: same as other deployments — schedule a SQL Agent job to delete `TapInAuthAuditEvents` rows older than your retention window.
- **Backups**: standard SQL Server backup strategy applies — TapInAuth's tables don't need anything special.
