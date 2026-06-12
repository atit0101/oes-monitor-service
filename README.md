# OES Monitor Service

ระบบ Observability สำหรับ OES Platform — Metrics, Logs, Traces + Data Quality

## Stack

| Service | Port | หน้าที่ |
|---------|------|---------|
| **Grafana** | 3000 | Dashboard UI |
| **Prometheus** | 9090 | Metrics storage (90d) |
| **MonitorApi** | 5000 | .NET 8 aggregator + data quality |
| **Loki** | 3100 | Log storage |
| **Tempo** | 3200 | Trace storage |
| **Blackbox Exporter** | 9115 | HTTP probing |
| **cAdvisor** | 8088 | Container resource metrics |
| **SQL Exporter** | 9399 | MSSQL deep metrics |
| **OTEL Collector** | 4319 | รับ traces จาก .NET services |
| **Promtail** | - | Log collector จาก Docker |

---

## Quick Start

### 1. Copy และแก้ไข .env

```bash
cp .env.example .env
# แก้ไข HOST_DEV, HOST_UAT, MSSQL_PASSWORD
```

### 2. แก้ไข prometheus.yml

เปลี่ยน `HOST_DEV` และ `HOST_UAT` ใน `prometheus/prometheus.yml`:
```yaml
- http://43.229.77.19:8080/health   # DEV IP จริง
```

### 3. Deploy DEV Monitor Stack

```bash
docker compose up -d
```

### 4. Deploy UAT Monitor Stack

```bash
docker compose -f docker-compose.uat.yml --env-file .env.uat up -d
```

### 5. สร้าง EF Core Migration (ครั้งแรก)

```bash
cd MonitorApi
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## Grafana Dashboards

เข้าที่ http://localhost:3000 (admin / admin123)

| Dashboard | URL |
|-----------|-----|
| Service Overview | /d/oes-01 |
| Dependency Topology | /d/oes-02 |
| DB Verification | /d/oes-03 |
| Data Quality | /d/oes-04 |
| Container Resources | /d/oes-05 |
| MSSQL Performance | /d/oes-06 |
| Logs | /d/oes-07 |
| Traces | /d/oes-08 |

---

## MonitorApi Endpoints

```
GET /api/health?env=DEV              — สถานะทุก service
GET /api/health/{name}/history       — history uptime
GET /api/topology?env=DEV            — dependency graph
GET /api/db-verification?env=DEV     — DB name verification
GET /api/db-verification/all         — DEV + UAT รวม
GET /api/data-quality?env=DEV        — data quality violations
GET /metrics                         — Prometheus metrics
GET /health                          — MonitorApi self health
```

Swagger UI: http://localhost:5000/swagger

---

## การเพิ่ม /health/detail ใน .NET Services (Phase 2)

แต่ละ service ใน `oes-api` ต้องเพิ่ม:

```xml
<!-- .csproj -->
<PackageReference Include="AspNetCore.HealthChecks.SqlServer" Version="8.*" />
<PackageReference Include="AspNetCore.HealthChecks.Uris" Version="8.*" />
<PackageReference Include="AspNetCore.HealthChecks.UI.Client" Version="8.*" />
```

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddSqlServer(connStr, name: "database", tags: ["db"])
    .AddUrlGroup(new Uri("http://user-service:8080/health"), name: "user-service");

app.MapHealthChecks("/health/detail", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    AllowCachingResponses = false,
});
```

---

## การเพิ่ม OpenTelemetry ใน .NET Services (Phase 5)

```xml
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.9.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.9.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.SqlClient" Version="1.9.*" />
```

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation()
        .AddOtlpExporter(o =>
            o.Endpoint = new Uri("http://otel-collector:4317")));
```

---

## Jenkins Deployment Annotation

เพิ่มใน `Jenkinsfile.development` และ `Jenkinsfile.uat` หลัง deploy:

```groovy
stage('Grafana Annotation') {
    steps {
        sh '''
            curl -s -X POST http://MONITOR_HOST:3000/api/annotations \
                -H "Authorization: Bearer ${GRAFANA_API_KEY}" \
                -H "Content-Type: application/json" \
                -d "{\\"text\\":\\"Deploy: ${IMAGE_NAME} #${BUILD_NUMBER}\\",\\"tags\\":[\\"deploy\\",\\"${ENVIRONMENT}\\"]}"
        '''
    }
}
```

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  Monitor Stack (standalone)                              │
│                                                         │
│  Grafana :3000 ──── Prometheus :9090                    │
│       │                  │                              │
│       ├── Loki :3100      ├── Blackbox :9115            │
│       ├── Tempo :3200     ├── cAdvisor :8088            │
│       └── MonitorApi :5000├── SQL Exporter :9399        │
│                  │        └── OTEL Collector :4319       │
│                  └── SQLite (local)                     │
└─────────────────────────────────────────────────────────┘
          │ probe HTTP              │ receive OTEL traces
          ↓                        ↓
   DEV services              .NET services
   UAT services              (core-insurance, etc.)
```
