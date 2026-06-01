# Deploy TapInAuth to AWS

Two paths: **App Runner** (simplest — managed container) or **ECS Fargate** (more control). Both with RDS PostgreSQL and Secrets Manager.

## Prerequisites

- AWS CLI configured.
- A custom domain managed in Route 53 (HTTPS is required for passkeys).
- ACM certificate for that domain in `us-east-1` (App Runner) or your target region (ECS).

## Option A — App Runner

App Runner builds from a container or directly from a GitHub repo. Recommended for simple deployments.

### 1. Provision RDS + Secrets

```bash
# PostgreSQL
aws rds create-db-instance \
  --db-instance-identifier tapinauth \
  --db-instance-class db.t4g.small \
  --engine postgres --engine-version 16 \
  --master-username tapinauth --master-user-password "{strong}" \
  --allocated-storage 20

# Token pepper
PEPPER=$(openssl rand -base64 48)
aws secretsmanager create-secret --name tapinauth/token-pepper --secret-string "$PEPPER"

# SMTP password (SES SMTP credentials are different from your IAM creds — generate them in the SES console)
aws secretsmanager create-secret --name tapinauth/smtp-password --secret-string "{ses-smtp-password}"
```

### 2. Container

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish samples/Mvc.Quickstart -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Mvc.Quickstart.dll"]
```

Push to ECR:

```bash
aws ecr create-repository --repository-name tapinauth
docker build -t tapinauth .
docker tag tapinauth:latest <acct>.dkr.ecr.<region>.amazonaws.com/tapinauth:latest
aws ecr get-login-password | docker login --username AWS --password-stdin <acct>.dkr.ecr.<region>.amazonaws.com
docker push <acct>.dkr.ecr.<region>.amazonaws.com/tapinauth:latest
```

### 3. App Runner service

In the AWS console: **App Runner → Create service → Source: ECR → tapinauth:latest**. Set environment variables (use Secrets Manager refs for sensitive values):

| Key | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__Default` | `Host=tapinauth.…rds.amazonaws.com;Database=tapinauth;Username=tapinauth;Password={from secret}` |
| `TapInAuth__Security__TokenPepper` | from `tapinauth/token-pepper` |
| `Smtp__Host` | `email-smtp.<region>.amazonaws.com` |
| `Smtp__Port` | `587` |
| `Smtp__Username` | your SES SMTP username |
| `Smtp__Password` | from `tapinauth/smtp-password` |
| `TapInAuth__Relying__Id` | `yourdomain.com` |
| `TapInAuth__Relying__AllowedOrigins__0` | `https://app.yourdomain.com` |

Bind the custom domain in the App Runner service settings. App Runner provisions and renews ACM certs automatically for App Runner-managed domains.

### 4. Migrate the DB

App Runner doesn't have a built-in migration step. Run the EF idempotent migration script from a CI job or a bastion before each deploy:

```bash
dotnet ef migrations script --idempotent -o migrate.sql --project src/Your.Web
psql "$DB_URL" -f migrate.sql
```

## Option B — ECS Fargate

Use when you need VPC peering, sidecars, or finer scaling controls. The Docker image is the same. Set up:

1. ECR repo (above).
2. ECS cluster with a Fargate service.
3. ALB with the ACM cert; route HTTPS 443 → target group on container port 8080.
4. RDS in the same VPC; security group allowing 5432 from the ECS service.
5. Task definition env vars: same set as App Runner (sourced from Secrets Manager via `secrets:` in the task def).

CDK / Terraform recommended over console clickops for ECS — the surface area is too wide for a brittle CLI script.

## Operational notes

- **Email**: AWS SES requires moving out of the sandbox before sending to arbitrary recipients. SES SMTP credentials are distinct from your IAM access key — generate them in the SES console.
- **Data-protection keys**: store in S3 + KMS so multi-instance Fargate keeps consistent cookie signing.
  ```csharp
  builder.Services.AddDataProtection()
      .PersistKeysToAWSSystemsManager("/tapinauth/dp-keys")
      .ProtectKeysWithAWSKms("arn:aws:kms:…");
  ```
- **Audit log retention**: the `TapInAuthAuditEvents` table grows unbounded. Schedule a Lambda or pg-cron job to delete rows older than your retention policy.
- **Backups**: RDS automated backups + at least one cross-region snapshot per week.
