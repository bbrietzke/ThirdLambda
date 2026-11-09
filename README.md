# ThirdLambda


##### Creating Migrations

```bash
dotnet ef migrations add InitialCreate --context SqliteAppDbContext    --output-dir Data/Migrations/Sqlite
dotnet ef migrations add InitialCreate --context SqlServerAppDbContext --output-dir Data/Migrations/SqlServer
```

##### Kubernetes ConfigMap options

```
Database__Provider=SqlServer
ConnectionStrings__SqlServer=Server=sqlserver.sqlserver.svc.cluster.local;Database=ThirdLambda;User Id=thirdlambda;Password=changeme;TrustServerCertificate=True
```


##### Kubernetes probes for the living

```yaml
ports:
  - name: grpc
    containerPort: 5000
  - name: mgmt
    containerPort: 8081
startupProbe:
  httpGet: { path: /healthz/live, port: mgmt }
  periodSeconds: 5
  failureThreshold: 30        # up to 150s to start before liveness takes over
livenessProbe:
  httpGet: { path: /healthz/live, port: mgmt }
  periodSeconds: 10
  failureThreshold: 3
readinessProbe:
  httpGet: { path: /healthz/ready, port: mgmt }
  periodSeconds: 10
  failureThreshold: 3
```