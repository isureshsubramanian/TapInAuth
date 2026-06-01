# Deploy TapInAuth with Docker / docker-compose

Self-host with Docker for a single-VM deployment or local dev replica of production.

## Dockerfile

Multi-stage, no SDK in the runtime image:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Packages.props", "global.json", "nuget.config", "./"]
COPY src/ src/
COPY samples/ samples/
RUN dotnet restore samples/Mvc.Quickstart/Mvc.Quickstart.csproj
RUN dotnet publish samples/Mvc.Quickstart/Mvc.Quickstart.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Mvc.Quickstart.dll"]
```

Build:

```bash
docker build -t tapinauth-app .
```

## docker-compose

```yaml
# docker-compose.yml
services:
  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: tapinauth
      POSTGRES_USER: tapinauth
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - db-data:/var/lib/postgresql/data
    restart: unless-stopped

  app:
    build: .
    depends_on: [db]
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Default: "Host=db;Database=tapinauth;Username=tapinauth;Password=${DB_PASSWORD}"
      TapInAuth__Security__TokenPepper: ${TOKEN_PEPPER}
      Smtp__Host: smtp.sendgrid.net
      Smtp__Port: "587"
      Smtp__Username: apikey
      Smtp__Password: ${SMTP_PASSWORD}
      Smtp__UseStartTls: "true"
      Smtp__FromAddress: no-reply@yourdomain.com
      TapInAuth__Relying__Id: yourdomain.com
      TapInAuth__Relying__AllowedOrigins__0: https://yourdomain.com
    expose: ["8080"]
    restart: unless-stopped

  # Reverse proxy + TLS termination
  caddy:
    image: caddy:2-alpine
    depends_on: [app]
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy-data:/data
      - caddy-config:/config
    ports: ["80:80", "443:443"]
    restart: unless-stopped

volumes:
  db-data:
  caddy-data:
  caddy-config:
```

`.env` (NOT in source control):

```
DB_PASSWORD=…
TOKEN_PEPPER=…   # openssl rand -base64 48
SMTP_PASSWORD=…
```

`Caddyfile`:

```
yourdomain.com {
    reverse_proxy app:8080
}
```

Caddy fetches and renews Let's Encrypt certs automatically. **HTTPS is required for passkeys.**

## Bring it up

```bash
docker compose up -d
docker compose logs -f app
```

## Database migration on container start

The simplest pattern: run EF migrations from a separate `migrator` container that runs once before the app starts.

```yaml
  migrator:
    build: .
    depends_on: [db]
    entrypoint: ["dotnet", "Mvc.Quickstart.dll", "--migrate"]
    environment:
      ConnectionStrings__Default: "Host=db;Database=tapinauth;Username=tapinauth;Password=${DB_PASSWORD}"
    restart: "no"
```

Then in `Program.cs`:

```csharp
if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    return;
}
```

## Data-protection key persistence

By default, ASP.NET Core stores the cookie data-protection keys in `~/.aspnet/DataProtection-Keys`. In a single-container Docker setup the keys are inside the container's filesystem — fine as long as the container is long-lived. **For multi-container or rolling deploys you must persist them externally:**

```yaml
  app:
    ...
    volumes:
      - dp-keys:/keys
    environment:
      ...
      DOTNET_DP_KEYS_PATH: /keys
```

```csharp
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Environment.GetEnvironmentVariable("DOTNET_DP_KEYS_PATH")!));
```

Or use S3 / Redis / a KMS-backed key.

## Hardening checklist

- ✅ HTTPS via reverse proxy (Caddy / nginx / Traefik).
- ✅ Database password from `.env` (or, better, Docker secrets / external secret store).
- ✅ Persistent token pepper (don't let it regenerate on container restart).
- ✅ Persistent data-protection keys volume.
- ✅ Container runs as non-root (add `USER app` after `FROM`).
- ✅ Backup the DB volume.
- ✅ Set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` so the app sees the real client IP behind the proxy.
