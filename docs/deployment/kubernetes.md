# Deploy TapInAuth to Kubernetes

Production-grade deployment with Helm. Single namespace, multiple replicas, external Postgres, ingress with cert-manager.

## Prerequisites

- A k8s cluster (EKS / GKE / AKS / kind for local).
- `kubectl` + `helm` installed.
- An ingress controller (nginx / traefik).
- `cert-manager` installed for Let's Encrypt.
- A PostgreSQL instance (in-cluster via the Bitnami chart, or external — RDS / Cloud SQL / Crunchy / Neon).

## Namespace + secrets

```bash
kubectl create namespace tapinauth

# Token pepper
kubectl -n tapinauth create secret generic tapinauth-secrets \
  --from-literal=TokenPepper=$(openssl rand -base64 48) \
  --from-literal=DbConnectionString="Host=postgres;Database=tapinauth;Username=tapinauth;Password=…" \
  --from-literal=SmtpPassword="…"
```

For larger setups: ExternalSecrets + Vault / AWS Secrets Manager / Azure Key Vault.

## Deployment

`tapinauth.deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: tapinauth
  namespace: tapinauth
spec:
  replicas: 3
  strategy: { type: RollingUpdate, rollingUpdate: { maxUnavailable: 0, maxSurge: 1 } }
  selector:
    matchLabels: { app: tapinauth }
  template:
    metadata:
      labels: { app: tapinauth }
    spec:
      containers:
        - name: app
          image: ghcr.io/yourorg/tapinauth:1.0.0
          ports: [{ containerPort: 8080 }]
          env:
            - { name: ASPNETCORE_ENVIRONMENT, value: Production }
            - { name: ASPNETCORE_URLS, value: "http://+:8080" }
            - { name: ASPNETCORE_FORWARDEDHEADERS_ENABLED, value: "true" }
            - name: ConnectionStrings__Default
              valueFrom: { secretKeyRef: { name: tapinauth-secrets, key: DbConnectionString } }
            - name: TapInAuth__Security__TokenPepper
              valueFrom: { secretKeyRef: { name: tapinauth-secrets, key: TokenPepper } }
            - name: Smtp__Password
              valueFrom: { secretKeyRef: { name: tapinauth-secrets, key: SmtpPassword } }
            - { name: Smtp__Host, value: "smtp.sendgrid.net" }
            - { name: Smtp__Port, value: "587" }
            - { name: Smtp__Username, value: "apikey" }
            - { name: Smtp__UseStartTls, value: "true" }
            - { name: Smtp__FromAddress, value: "no-reply@yourdomain.com" }
            - { name: TapInAuth__Relying__Id, value: "yourdomain.com" }
            - { name: TapInAuth__Relying__AllowedOrigins__0, value: "https://app.yourdomain.com" }
          readinessProbe:
            httpGet: { path: /healthz, port: 8080 }
            initialDelaySeconds: 5
            periodSeconds: 5
          livenessProbe:
            httpGet: { path: /healthz, port: 8080 }
            initialDelaySeconds: 30
            periodSeconds: 30
          resources:
            requests: { cpu: 100m, memory: 192Mi }
            limits:   { cpu: 500m, memory: 512Mi }
          volumeMounts:
            - { name: dp-keys, mountPath: /keys }
      volumes:
        - name: dp-keys
          persistentVolumeClaim: { claimName: tapinauth-dp-keys }
```

## Service + Ingress

```yaml
apiVersion: v1
kind: Service
metadata: { name: tapinauth, namespace: tapinauth }
spec:
  selector: { app: tapinauth }
  ports: [{ port: 80, targetPort: 8080 }]
---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: tapinauth
  namespace: tapinauth
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  ingressClassName: nginx
  tls: [{ hosts: [app.yourdomain.com], secretName: tapinauth-tls }]
  rules:
    - host: app.yourdomain.com
      http:
        paths:
          - { path: /, pathType: Prefix, backend: { service: { name: tapinauth, port: { number: 80 } } } }
```

## Data-protection key persistence

Cookies are signed with data-protection keys. Across replicas, those keys MUST be shared, or users will be signed out every time a request hits a different pod.

Three patterns:

1. **PVC + ReadWriteMany** — cheap; requires RWX-capable storage class (NFS, EFS, AzureFiles).
2. **Redis** — `Microsoft.AspNetCore.DataProtection.StackExchangeRedis`. Best for large clusters.
3. **Cloud-native KMS** — Azure Key Vault, AWS KMS, GCP KMS. Most secure.

```csharp
builder.Services.AddDataProtection()
    .PersistKeysToStackExchangeRedis(
        ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!),
        "tapinauth-dp-keys");
```

## Database migrations

Use a k8s Job that runs before the deployment is updated. Helm `pre-upgrade` hook is the cleanest:

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: tapinauth-migrate-{{ .Release.Revision }}
  annotations:
    "helm.sh/hook": pre-upgrade,pre-install
    "helm.sh/hook-weight": "-5"
    "helm.sh/hook-delete-policy": before-hook-creation,hook-succeeded
spec:
  template:
    spec:
      restartPolicy: Never
      containers:
        - name: migrate
          image: ghcr.io/yourorg/tapinauth:1.0.0
          command: ["dotnet", "Mvc.Quickstart.dll", "--migrate"]
          env: # same DB secret as the app
            - name: ConnectionStrings__Default
              valueFrom: { secretKeyRef: { name: tapinauth-secrets, key: DbConnectionString } }
```

## Horizontal scaling

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata: { name: tapinauth, namespace: tapinauth }
spec:
  scaleTargetRef: { apiVersion: apps/v1, kind: Deployment, name: tapinauth }
  minReplicas: 3
  maxReplicas: 20
  metrics:
    - type: Resource
      resource: { name: cpu, target: { type: Utilization, averageUtilization: 70 } }
```

## Operational notes

- **Health endpoint**: add one (e.g., `app.MapGet("/healthz", () => Results.Ok());`). The deployment manifest above expects it.
- **Forwarded headers**: with the ingress in front, you need `app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto })` so audit events log the real client IP.
- **Distributed rate limiter**: the default `InMemoryRateLimiter` doesn't share state across pods. For real protection, swap in a Redis-backed `IRateLimiter` implementation.
- **Audit log retention**: the `TapInAuthAuditEvents` table grows unbounded. Schedule a CronJob to delete rows older than your retention window.
