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