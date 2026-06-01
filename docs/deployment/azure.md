# Deploy TapInAuth to Azure

End-to-end deployment to Azure App Service (Linux) with Azure SQL and managed secrets.

## Prerequisites

- An Azure subscription.
- `az` CLI signed in.
- A resource group: `az group create --name rg-tapinauth --location eastus`.
- A custom domain you'll point at the app (required for HTTPS-bound passkeys to work outside `localhost`).

## 1. Provision

```bash
# App Service Plan + Web App (Linux, .NET 10)
az appservice plan create -g rg-tapinauth -n plan-tapinauth --sku P0v3 --is-linux
az webapp create -g rg-tapinauth -p plan-tapinauth -n app-tapinauth --runtime "DOTNETCORE:10.0"

# Azure SQL
az sql server create -g rg-tapinauth -n sql-tapinauth -u sqladmin -p "{strong-password}"
az sql db create -g rg-tapinauth -s sql-tapinauth -n tapinauth --edition GeneralPurpose --compute-model Serverless --capacity 1

# Key Vault (for the token pepper + SMTP creds)
az keyvault create -g rg-tapinauth -n kv-tapinauth --enable-rbac-authorization true
az role assignment create --assignee $(az webapp identity assign -g rg-tapinauth -n app-tapinauth --query principalId -o tsv) \
    --role "Key Vault Secrets User" --scope $(az keyvault show -g rg-tapinauth -n kv-tapinauth --query id -o tsv)
```

## 2. Generate the token pepper

```bash
PEPPER=$(openssl rand -base64 48)
az keyvault secret set --vault-name kv-tapinauth --name TapInAuth--Security--TokenPepper --value "$PEPPER"
```

Also store SMTP creds:

```bash
az keyvault secret set --vault-name kv-tapinauth --name Smtp--Password --value "{your-smtp-password}"
```

## 3. App settings + Key Vault references

```bash
az webapp config appsettings set -g rg-tapinauth -n app-tapinauth --settings \
  ASPNETCORE_ENVIRONMENT=Production \
  ConnectionStrings__Default="Server=tcp:sql-tapinauth.database.windows.net,1433;Database=tapinauth;Authentication=Active Directory Default;TrustServerCertificate=False;Encrypt=True" \
  TapInAuth__Security__TokenPepper="@Microsoft.KeyVault(VaultName=kv-tapinauth;SecretName=TapInAuth--Security--TokenPepper)" \
  Smtp__Host="smtp.sendgrid.net" \
  Smtp__Port=587 \
  Smtp__Username="apikey" \
  Smtp__Password="@Microsoft.KeyVault(VaultName=kv-tapinauth;SecretName=Smtp--Password)" \
  Smtp__FromAddress="no-reply@yourdomain.com" \
  Smtp__UseStartTls=true \
  TapInAuth__Relying__Id="yourdomain.com" \
  TapInAuth__Relying__AllowedOrigins__0="https://yourdomain.com"
```

The DB connection uses **Active Directory Default** auth + the Web App's managed identity. Grant the identity SQL access:

```bash
# Add managed identity as a SQL contained user (run as the AAD admin of the server):
sqlcmd -S sql-tapinauth.database.windows.net -d tapinauth -G -Q "
  CREATE USER [app-tapinauth] FROM EXTERNAL PROVIDER;
  ALTER ROLE db_datareader ADD MEMBER [app-tapinauth];
  ALTER ROLE db_datawriter ADD MEMBER [app-tapinauth];
  ALTER ROLE db_ddladmin   ADD MEMBER [app-tapinauth];
"
```

## 4. HTTPS + custom domain

```bash
az webapp config hostname add -g rg-tapinauth --webapp-name app-tapinauth --hostname app.yourdomain.com
az webapp config ssl bind -g rg-tapinauth --name app-tapinauth --ssl-type SNI --certificate-thumbprint {…}
```

WebAuthn requires HTTPS (or `localhost`). The cert is required for passkeys to work in production.

## 5. Deploy

GitHub Actions: a starter workflow is included in the repo. Add a service-principal secret and let the workflow `az webapp deploy`. Or use the CLI:

```bash
dotnet publish samples/Mvc.Quickstart -c Release -o ./publish
az webapp deploy -g rg-tapinauth -n app-tapinauth --src-path ./publish --type zip
```

## 6. Database migration

EF migrations is the production path. Generate idempotent SQL once and run it during deployment:

```bash
dotnet ef migrations script --idempotent -o migrate.sql --project src/Your.Web
sqlcmd -S sql-tapinauth.database.windows.net -d tapinauth -G -i migrate.sql
```

Don't rely on `EnsureCreated()` in production — it doesn't handle schema changes between releases.

## 7. Smoke test

```bash
curl https://app.yourdomain.com/healthz
```

Open `https://app.yourdomain.com/auth/sign-in` in a browser. Sign in via magic link — your SMTP provider should deliver to your inbox.

## Operational notes

- **Application Insights**: add `Microsoft.ApplicationInsights.AspNetCore` and set `APPLICATIONINSIGHTS_CONNECTION_STRING`. Audit events flow through `ILogger` and into App Insights automatically when you use the `LoggingAuditSink`. If you switched to `AddEfCoreAuditSink`, you can either layer both (write to both DB and logger) or keep just the DB sink.
- **Always-on**: App Service plans below `Standard` go to sleep. Enable Always-on or use a `Standard` SKU.
- **Scale-out**: TapInAuth is stateless beyond the DB. Multi-instance App Service works as long as the cookie data-protection keys are persisted (use `AddDataProtection().PersistKeysToAzureBlobStorage(...)` + `ProtectKeysWithAzureKeyVault(...)`).
- **Logs**: `az webapp log tail -g rg-tapinauth -n app-tapinauth`.
