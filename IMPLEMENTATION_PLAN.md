# OES Monitor Service — แผนดำเนินการฉบับสมบูรณ์

> วันที่วางแผน: 2026-06-12  
> เป้าหมาย: ระบบ Observability ครบ 3 มิติ (Metrics, Logs, Traces) + Data Quality สำหรับ DEV และ UAT

---

## ภาพรวม Infrastructure ที่ต้อง Monitor

| ประเภท | รายการ | Environment |
|--------|--------|-------------|
| Angular Web | `oes-web-oic` (port 4000), `oes-web-insurance` | DEV, UAT |
| .NET API | `core-insurance` :8080, `core-oic` :8081 | DEV, UAT |
| .NET API | `user-service` :8082, `notification-service` :8083 | DEV, UAT |
| .NET API | `audit-log-service` :8084, `assessment-service` :8085 | DEV, UAT |
| .NET API | `dashboard-service`, `report-service` | DEV, UAT |
| Database | MSSQL (DEV: 43.229.77.19, UAT: 43.229.134.32) | DEV, UAT |
| SMTP | Mailpit (DEV), Real SMTP (UAT) | DEV, UAT |

---

## โครงสร้างโปรเจกต์สุดท้าย

```
oes-monitor-service/
├── MonitorApi/                        # Phase 2: .NET 8 Aggregator API
│   ├── Controllers/
│   │   ├── HealthSummaryController.cs
│   │   ├── TopologyController.cs
│   │   ├── DbVerificationController.cs
│   │   └── DataQualityController.cs
│   ├── Workers/
│   │   ├── HealthAggregatorWorker.cs
│   │   └── DataQualityWorker.cs
│   ├── Models/
│   ├── Data/                          # SQLite
│   ├── appsettings.json               # service registry DEV
│   ├── appsettings.uat.json           # service registry UAT
│   └── Dockerfile
│
├── prometheus/
│   └── prometheus.yml                 # scrape: blackbox + cadvisor + sql_exporter
│
├── loki/
│   └── loki-config.yml                # Phase 4: log storage
│
├── promtail/
│   └── promtail-config.yml            # Phase 4: log collector จาก Docker
│
├── tempo/
│   └── tempo-config.yml               # Phase 5: trace storage
│
├── otel-collector/
│   └── otel-config.yml                # Phase 5: รับ traces จาก .NET services
│
├── grafana/
│   └── provisioning/
│       ├── datasources/
│       │   ├── prometheus.yml
│       │   ├── loki.yml
│       │   ├── tempo.yml
│       │   └── json-api.yml           # MonitorApi
│       └── dashboards/
│           ├── 01-service-overview.json
│           ├── 02-dependency-topology.json
│           ├── 03-db-verification.json
│           ├── 04-data-quality.json
│           ├── 05-logs.json
│           └── 06-traces.json
│
├── sql-exporter/
│   └── config.yml                     # Phase 3: MSSQL deep metrics
│
├── docker-compose.yml                 # DEV monitor stack
├── docker-compose.uat.yml             # UAT monitor stack
└── IMPLEMENTATION_PLAN.md             # ไฟล์นี้
```

---

## Phase 1 — Infrastructure Monitoring Stack
**ระยะเวลา: 1–2 วัน**  
**ผลลัพธ์: Grafana dashboard พร้อมดู service up/down + response time**

### 1.1 สร้าง docker-compose.yml หลัก

```yaml
services:
  grafana:
    image: grafana/grafana:latest
    ports: ["3000:3000"]
    volumes:
      - grafana-data:/var/lib/grafana
      - ./grafana/provisioning:/etc/grafana/provisioning

  prometheus:
    image: prom/prometheus:latest
    ports: ["9090:9090"]
    volumes:
      - ./prometheus/prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus

  blackbox-exporter:
    image: prom/blackbox-exporter:latest
    ports: ["9115:9115"]

  cadvisor:
    image: gcr.io/cadvisor/cadvisor:latest
    ports: ["8088:8080"]
    volumes:
      - /:/rootfs:ro
      - /var/run:/var/run:ro
      - /sys:/sys:ro
      - /var/lib/docker/:/var/lib/docker:ro
```

### 1.2 prometheus.yml — scrape config

```yaml
scrape_configs:
  # HTTP probing — DEV
  - job_name: dev-http
    metrics_path: /probe
    params: { module: [http_2xx] }
    static_configs:
      - targets:
          - http://HOST_DEV:8080/health    # core-insurance
          - http://HOST_DEV:8081/health    # core-oic
          - http://HOST_DEV:8082/health    # user-service
          - http://HOST_DEV:8083/health    # notification-service
          - http://HOST_DEV:8084/health    # audit-log-service
          - http://HOST_DEV:8085/health    # assessment-service
          - http://HOST_DEV:4000           # oes-web-oic
        labels: { environment: DEV }
    relabel_configs: [ ... blackbox relabel ... ]

  # HTTP probing — UAT
  - job_name: uat-http
    # same targets → UAT IPs
    labels: { environment: UAT }

  # Container metrics
  - job_name: cadvisor
    static_configs:
      - targets: [cadvisor:8080]

  # SSL cert expiry
  - job_name: ssl-certs
    metrics_path: /probe
    params: { module: [tcp_connect] }
    static_configs:
      - targets: [oesweb.co:443, uat.oesweb.co:443]
```

### 1.3 Grafana Dashboard: 01-service-overview
- Stat panels: Up/Down per service (สี แดง/เขียว)
- Time series: response time 24h
- Table: environment | service | status | uptime % | last check
- Filter: DEV / UAT dropdown

---

## Phase 2 — Application Layer Health Checks
**ระยะเวลา: 2–3 วัน**  
**ผลลัพธ์: รู้ว่า service เชื่อม DB ถูกตัวไหม และ dependency ทำงานได้ไหม**

### 2.1 เพิ่ม NuGet packages ใน services ที่มี DB

```xml
<!-- เพิ่มใน: CoreInsurance, CoreOIC, UserService, AuditLogService, AssessmentService -->
<PackageReference Include="AspNetCore.HealthChecks.SqlServer" Version="8.*" />
<PackageReference Include="AspNetCore.HealthChecks.Uris" Version="8.*" />
<PackageReference Include="AspNetCore.HealthChecks.UI.Client" Version="8.*" />
```

### 2.2 เพิ่ม /health/detail endpoint ในทุก service

```csharp
// Program.cs ของแต่ละ service
builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection"),
        name: "database",
        healthCheckResultDescription: "MSSQL connectivity + DB name verification",
        configure: options => { /* check DB name matches expected */ },
        tags: ["db"]
    )
    // เฉพาะ services ที่ call service อื่น:
    .AddUrlGroup(new Uri(cfg["enpoint_userservice"] + "health"),
        name: "user-service", tags: ["dependency"])
    .AddUrlGroup(new Uri(cfg["AuditLog:BaseUrl"] + "health"),
        name: "audit-log-service", tags: ["dependency"]);

app.MapHealthChecks("/health/detail", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    AllowCachingResponses = false
});
```

### 2.3 Dependency Map (ว่า service ไหนเรียกใคร)

```
core-insurance     → MSSQL, user-service, audit-log-service, notification-service
core-oic           → MSSQL, audit-log-service
user-service       → MSSQL, audit-log-service
audit-log-service  → MSSQL
assessment-service → MSSQL, user-service, notification-service
notification-service → SMTP (Mailpit/Real)
report-service     → MSSQL
dashboard-service  → core-insurance, core-oic
```

### 2.4 ตัวอย่าง /health/detail response

```json
{
  "status": "Degraded",
  "entries": {
    "database": {
      "status": "Healthy",
      "data": {
        "server": "43.229.77.19,1433",
        "database": "OES_Insurance",
        "expectedDatabase": "OES_Insurance",
        "dbMatch": true,
        "environment": "DEV"
      }
    },
    "user-service": { "status": "Healthy", "data": { "statusCode": 200 } },
    "audit-log-service": {
      "status": "Unhealthy",
      "exception": "Connection refused",
      "data": { "url": "http://audit-log-service:8080" }
    }
  }
}
```

### 2.5 สร้าง MonitorApi (.NET 8)

```
MonitorApi/
├── Workers/HealthAggregatorWorker.cs  — poll /health/detail ทุก 30s
├── Controllers/
│   ├── HealthSummaryController.cs     — GET /api/health (all services)
│   ├── TopologyController.cs          — GET /api/topology (graph)
│   └── DbVerificationController.cs   — GET /api/db-verification
├── Data/MonitorDbContext.cs           — SQLite
└── appsettings.json                   — service registry
```

**appsettings.json — service registry:**
```json
{
  "Services": [
    {
      "name": "core-insurance",
      "displayName": "Core Insurance API",
      "environments": {
        "DEV": {
          "healthDetailUrl": "http://43.229.77.19:8080/health/detail",
          "expectedDb": "OES_Insurance"
        },
        "UAT": {
          "healthDetailUrl": "http://43.229.134.32:8080/health/detail",
          "expectedDb": "OES_Insurance_UAT"
        }
      }
    }
  ]
}
```

### 2.6 Grafana Dashboards

**02-dependency-topology** — Node Graph panel
- Nodes: แต่ละ service (สี = health status)
- Edges: การเชื่อมต่อระหว่าง service (สี = connection status)
- คลิก node → เห็น detail

**03-db-verification** — Table panel
| Service | DB Server | Actual DB | Expected DB | Match | Environment |
|---------|-----------|-----------|-------------|-------|-------------|
| core-insurance | 43.229.77.19 | OES_Insurance | OES_Insurance | ✅ | DEV |
| core-insurance | 43.229.134.32 | OES_Insurance | OES_Insurance_UAT | ❌ | UAT |

---

## Phase 3 — MSSQL Deep Monitoring
**ระยะเวลา: 1 วัน**  
**ผลลัพธ์: รู้ slow query, connection pool, deadlock**

### 3.1 เพิ่ม sql_exporter ใน docker-compose

```yaml
sql-exporter:
  image: burningalchemist/sql_exporter:latest
  volumes:
    - ./sql-exporter/config.yml:/etc/sql_exporter/config.yml
```

### 3.2 sql-exporter/config.yml

```yaml
jobs:
  - name: mssql_health
    connections: ["sqlserver://sa:pass@43.229.77.19:1433"]
    queries:
      - name: slow_queries
        help: "Queries averaging over 1 second"
        values: [avg_elapsed_ms, execution_count]
        query: |
          SELECT TOP 10
            total_elapsed_time/execution_count/1000 AS avg_elapsed_ms,
            execution_count,
            SUBSTRING(st.text,1,100) AS query_preview
          FROM sys.dm_exec_query_stats qs
          CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
          ORDER BY avg_elapsed_ms DESC

      - name: connection_pool
        help: "Active connections per database"
        values: [connection_count]
        query: |
          SELECT DB_NAME(database_id) AS db_name, COUNT(*) AS connection_count
          FROM sys.dm_exec_sessions
          WHERE is_user_process = 1
          GROUP BY database_id

      - name: deadlocks
        help: "Deadlock count since last restart"
        values: [deadlock_count]
        query: |
          SELECT cntr_value AS deadlock_count
          FROM sys.dm_os_performance_counters
          WHERE counter_name = 'Number of Deadlocks/sec'
          AND instance_name = '_Total'
```

---

## Phase 4 — Log Aggregation (Loki + Promtail)
**ระยะเวลา: 1 วัน**  
**ผลลัพธ์: ดู log ทุก container ใน Grafana ค้นหาได้ เชื่อม trace ได้**

### 4.1 เพิ่มใน docker-compose

```yaml
loki:
  image: grafana/loki:latest
  ports: ["3100:3100"]
  volumes:
    - ./loki/loki-config.yml:/etc/loki/local-config.yaml
    - loki-data:/loki

promtail:
  image: grafana/promtail:latest
  volumes:
    - /var/run/docker.sock:/var/run/docker.sock
    - ./promtail/promtail-config.yml:/etc/promtail/config.yml
```

### 4.2 promtail-config.yml

```yaml
scrape_configs:
  - job_name: docker
    docker_sd_configs:
      - host: unix:///var/run/docker.sock
    relabel_configs:
      - source_labels: [__meta_docker_container_name]
        target_label: container
      - source_labels: [__meta_docker_container_label_com_docker_compose_service]
        target_label: service
    pipeline_stages:
      - json:
          expressions:
            level: level
            traceId: TraceId
            correlationId: CorrelationId
      - labels:
          level:
          traceId:
          correlationId:
```

### 4.3 เพิ่ม Structured Logging ใน .NET services

```csharp
// Program.cs ทุก service — ให้ log เป็น JSON format
builder.Logging.AddJsonConsole(opts =>
{
    opts.IncludeScopes = true;
    opts.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});
```

**Grafana Dashboard 05-logs:**
- Log stream ทุก service
- Filter ตาม: service, level (Error/Warning/Info), correlationId, traceId
- คลิก correlationId → เห็น log ทุก service ที่เกี่ยวข้องกับ request นั้น

---

## Phase 5 — Distributed Tracing (Grafana Tempo + OpenTelemetry)
**ระยะเวลา: 1–2 วัน**  
**ผลลัพธ์: trace request ข้าม service ได้ รู้พังตรงจุดไหน**

> AssessmentService มี OpenTelemetry อยู่แล้ว — ต้องเพิ่ม backend รับเท่านั้น

### 5.1 เพิ่มใน docker-compose

```yaml
tempo:
  image: grafana/tempo:latest
  ports: ["3200:3200", "4317:4317"]   # 4317 = OTLP gRPC
  volumes:
    - ./tempo/tempo-config.yml:/etc/tempo/tempo.yaml
    - tempo-data:/tmp/tempo

otel-collector:
  image: otel/opentelemetry-collector-contrib:latest
  ports: ["4318:4318"]                # OTLP HTTP
  volumes:
    - ./otel-collector/otel-config.yml:/etc/otelcol-contrib/config.yaml
```

### 5.2 เพิ่ม OpenTelemetry ใน services ที่ยังไม่มี

```csharp
// Program.cs — services ที่ยังไม่มี OTEL
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation()        // trace SQL queries
        .AddOtlpExporter(o =>
            o.Endpoint = new Uri("http://otel-collector:4317")));
```

### 5.3 การใช้งาน

```
User กด Submit Assessment
  → HTTP POST /api/assessments (core-insurance)
    → TraceId: abc123
    → HTTP call user-service/api/users/123
      → TraceId: abc123 (ต่อเนื่อง)
    → SQL INSERT INTO Assessments
      → TraceId: abc123

Grafana Tempo: ค้นหา TraceId abc123
→ เห็น waterfall: core-insurance (120ms) → user-service (15ms) → MSSQL (89ms)
→ รู้ทันทีว่า bottleneck คือ SQL query
```

---

## Phase 6 — Data Observability (ส่วนที่สำคัญที่สุด)
**ระยะเวลา: 3–4 วัน**  
**ผลลัพธ์: รู้ว่า data ผิด/ถูก, มาจาก function ไหน, ก่อน/หลัง change เป็นอะไร**

### 6.1 เพิ่ม OldValue/NewValue ใน AuditLog Schema

```sql
-- Migration: เพิ่ม column ใน AuditLogs table
ALTER TABLE AuditLogs ADD
    OldValue      NVARCHAR(MAX) NULL,   -- JSON snapshot ก่อน save
    NewValue      NVARCHAR(MAX) NULL,   -- JSON snapshot หลัง save
    ChangedFields NVARCHAR(500) NULL;   -- ["Status","Score","UpdatedAt"]
```

```csharp
// AuditLog.cs entity
public string? OldValue { get; set; }
public string? NewValue { get; set; }
public string? ChangedFields { get; set; }
```

### 6.2 EF Core SaveChanges Interceptor (Auto Audit — ไม่ต้อง call manual)

```csharp
// Shared/AuditInterceptor.cs — ใช้ร่วมกันทุก service
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IAuditLogger _auditLogger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private List<AuditEntry> _pendingEntries = new();

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, ...)
    {
        // จับ OriginalValues ก่อน save (สำคัญ: ต้องทำก่อน SaveChanges)
        _pendingEntries = eventData.Context!.ChangeTracker
            .Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => new AuditEntry
            {
                EntityName    = e.Metadata.ClrType.Name,
                EntityId      = string.Join(",", e.Properties
                                    .Where(p => p.Metadata.IsPrimaryKey())
                                    .Select(p => p.CurrentValue)),
                ActionType    = e.State.ToString(),    // Added/Modified/Deleted
                OldValue      = e.State != EntityState.Added
                                    ? JsonSerializer.Serialize(e.OriginalValues.ToObject())
                                    : null,
                NewValue      = e.State != EntityState.Deleted
                                    ? JsonSerializer.Serialize(e.CurrentValues.ToObject())
                                    : null,
                ChangedFields = e.State == EntityState.Modified
                                    ? JsonSerializer.Serialize(
                                        e.Properties
                                            .Where(p => p.IsModified)
                                            .Select(p => p.Metadata.Name))
                                    : null,
                CorrelationId = _httpContextAccessor.HttpContext?.TraceIdentifier,
                TraceId       = Activity.Current?.TraceId.ToString(),
            })
            .ToList();

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, ...)
    {
        // ส่ง audit log หลัง save สำเร็จ (fire-and-forget ไม่บล็อก request)
        foreach (var entry in _pendingEntries)
        {
            _ = _auditLogger.WriteAsync(new AuditLogCommand
            {
                ServiceName   = _serviceName,
                EntityName    = entry.EntityName,
                EntityId      = entry.EntityId,
                ActionType    = entry.ActionType,
                OldValue      = entry.OldValue,
                NewValue      = entry.NewValue,
                ChangedFields = entry.ChangedFields,
                CorrelationId = entry.CorrelationId,
                TraceId       = entry.TraceId,
                ResultStatus  = "SUCCESS",
            });
        }
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
```

**ลงทะเบียน interceptor:**
```csharp
// DependencyInjection.cs ทุก service
services.AddDbContext<CoreInsuranceDbContext>((sp, opts) => opts
    .UseSqlServer(connStr)
    .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

services.AddScoped<AuditSaveChangesInterceptor>();
```

### 6.3 Data Quality Rules — MonitorApi

```csharp
// MonitorApi/DataQuality/DataQualityRule.cs
public class DataQualityRule
{
    public string RuleName { get; set; }
    public string Description { get; set; }
    public string Database { get; set; }    // "OES_Assessment"
    public string SqlQuery { get; set; }    // SELECT COUNT(*) AS violation_count FROM ...
    public int    WarningThreshold { get; set; }
    public int    CriticalThreshold { get; set; }
    public string Environment { get; set; } // DEV / UAT
}
```

**ตัวอย่าง rules ที่ต้องมี:**
```sql
-- Rule 1: Orphaned assessments (ไม่มี user)
SELECT COUNT(*) AS violation_count
FROM Assessments a
LEFT JOIN Users u ON a.CreatedByUserId = u.Id
WHERE u.Id IS NULL;

-- Rule 2: Score นอก range
SELECT COUNT(*) AS violation_count
FROM AssessmentScores
WHERE Score < 0 OR Score > 100;

-- Rule 3: Submission ไม่มี data วันนี้ (data not flowing)
SELECT CASE WHEN COUNT(*) = 0 THEN 1 ELSE 0 END AS violation_count
FROM Submissions
WHERE CreatedAt >= CAST(GETDATE() AS DATE)
  AND DATEPART(HOUR, GETDATE()) >= 10;

-- Rule 4: NULL ในของที่ไม่ควร NULL
SELECT COUNT(*) AS violation_count
FROM Submissions
WHERE CompanyCode IS NULL AND Status NOT IN ('Draft');

-- Rule 5: Cross-service integrity
-- UserId ใน CoreInsurance ไม่มีใน UserService
SELECT COUNT(*) AS violation_count
FROM [OES_Insurance].dbo.Submissions s
WHERE NOT EXISTS (
    SELECT 1 FROM [OES_Users].dbo.Users u WHERE u.Id = s.CreatedByUserId
);
```

**DataQualityWorker** — run rules ทุก 5 นาที expose เป็น Prometheus metrics:
```
data_quality_violations{rule="orphaned_assessments",env="DEV"} 0
data_quality_violations{rule="score_out_of_range",env="UAT"} 3
data_quality_violations{rule="data_not_flowing",env="DEV"} 1
```

### 6.4 Grafana Dashboard: 04-data-quality

- Table: Rule | Description | Violations | Severity | Environment | Last Check
- Alert: ถ้า violation > threshold → สีแดง
- History graph: violations per rule ย้อนหลัง 7 วัน

### 6.5 AuditLog Search Dashboard (ค้นหา Data Lineage)

```
สถานการณ์: "record Assessment ID=42 ข้อมูลผิด — ใครแก้? แก้อะไร?"

Grafana AuditLog panel:
  Filter: EntityName=Assessment, EntityId=42
  Result:
    2026-06-10 14:32:01 | core-oic | UPDATE | user: somchai | 
    OldValue: {"Score":85,"Status":"Draft"} | 
    NewValue: {"Score":0,"Status":"Submitted"} |
    ChangedFields: ["Score","Status"] |
    ApiName: PUT /api/assessments/42 |
    CorrelationId: abc-123

  → คลิก CorrelationId abc-123
  → เห็น trace ทั้ง request นั้น ข้าม services
```

---

## Phase 7 — Alerting
**ระยะเวลา: 1 วัน**  
**ผลลัพธ์: แจ้งเตือนอัตโนมัติเมื่อระบบมีปัญหา**

### 7.1 Grafana Alerting Rules

```yaml
# alert rules
groups:
  - name: service-health
    rules:
      - alert: ServiceDown
        expr: probe_success{job=~"dev-http|uat-http"} == 0
        for: 2m
        labels: { severity: critical }
        annotations:
          summary: "{{ $labels.instance }} DOWN บน {{ $labels.environment }}"

      - alert: HighResponseTime
        expr: probe_duration_seconds > 2
        for: 5m
        labels: { severity: warning }
        annotations:
          summary: "{{ $labels.instance }} response time > 2s"

      - alert: DataQualityViolation
        expr: data_quality_violations{severity="critical"} > 0
        for: 0m
        labels: { severity: critical }
        annotations:
          summary: "Data quality violation: {{ $labels.rule }}"

      - alert: DBConnectionFailed
        expr: health_check_database_status == 0
        for: 1m
        labels: { severity: critical }
        annotations:
          summary: "{{ $labels.service }} ต่อ DB ไม่ได้ บน {{ $labels.environment }}"

      - alert: SSLCertExpiringSoon
        expr: probe_ssl_earliest_cert_expiry - time() < 86400 * 30
        for: 0m
        labels: { severity: warning }
        annotations:
          summary: "SSL cert ของ {{ $labels.instance }} จะหมดใน 30 วัน"
```

### 7.2 Notification Channel

```yaml
# Grafana contact points
- Email → ทีม dev (service down, data quality critical)
- Line Notify → on-call (service down > 5 นาที)
```

### 7.3 Deployment Annotation (Jenkins → Grafana)

```groovy
// เพิ่มใน Jenkinsfile.development และ Jenkinsfile.uat หลัง deploy สำเร็จ
stage('Notify Grafana') {
    steps {
        sh '''
            curl -X POST http://GRAFANA_HOST:3000/api/annotations \
                -H "Authorization: Bearer ${GRAFANA_API_KEY}" \
                -H "Content-Type: application/json" \
                -d "{
                    \"text\": \"Deploy: ${IMAGE_NAME} build #${BUILD_NUMBER}\",
                    \"tags\": [\"deploy\", \"${ENVIRONMENT}\"],
                    \"time\": $(date +%s)000
                }"
        '''
    }
}
```

---

## Phase 8 — Security & Long-term Storage
**ระยะเวลา: 1 วัน**

### 8.1 Secure /health/detail Endpoint

```csharp
// ป้องกัน /health/detail ไม่ให้เข้าจาก external network
app.MapHealthChecks("/health/detail", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
}).RequireHost("monitor-api", "localhost", "*.internal");

// หรือใช้ API Key header
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/health/detail"),
    branch => branch.UseMiddleware<InternalNetworkOnlyMiddleware>());
```

### 8.2 Prometheus Long-term Retention

```yaml
# prometheus command flags
command:
  - --storage.tsdb.retention.time=90d    # เก็บ 90 วัน (default 15 วัน)
  - --storage.tsdb.retention.size=20GB
```

### 8.3 Grafana RBAC

```
Team: DEV-Team   → เห็นได้: DEV dashboards เท่านั้น
Team: UAT-Team   → เห็นได้: UAT dashboards เท่านั้น
Team: Admin      → เห็นได้: ทุก dashboard
```

---

## สรุป Timeline การดำเนินการ

| Phase | งาน | ระยะเวลา | ผลลัพธ์ |
|-------|-----|----------|---------|
| **1** | Infrastructure Stack (Prometheus + Grafana + Blackbox + cAdvisor) | 1–2 วัน | ดู service up/down + container resources |
| **2** | Application Health Checks + MonitorApi | 2–3 วัน | DB verification + dependency topology |
| **3** | MSSQL Deep Monitoring (sql_exporter) | 1 วัน | slow query + deadlock + connection pool |
| **4** | Log Aggregation (Loki + Promtail) | 1 วัน | ค้นหา log ทุก service ใน Grafana |
| **5** | Distributed Tracing (Tempo + OTEL) | 1–2 วัน | trace request ข้าม services |
| **6** | Data Observability (AuditLog + EF Core + Data Quality) | 3–4 วัน | รู้ data มาจากไหน ผิดตรงไหน |
| **7** | Alerting + Jenkins Annotation | 1 วัน | แจ้งเตือนอัตโนมัติ |
| **8** | Security + Long-term Storage | 1 วัน | production-ready |

**รวม: ~12–15 วันทำงาน**

---

## Grafana Dashboards สุดท้าย (8 หน้า)

| # | ชื่อ | ข้อมูล | Use Case |
|---|------|---------|----------|
| 01 | Service Overview | Prometheus | ดู up/down ทุก service แยก DEV/UAT |
| 02 | Dependency Topology | MonitorApi | graph service dependencies + status |
| 03 | DB Verification | MonitorApi | เช็คว่า service เชื่อม DB ถูกตัว |
| 04 | Data Quality | MonitorApi | violations, orphaned records, anomalies |
| 05 | Container Resources | cAdvisor | CPU/Memory/Network per container |
| 06 | MSSQL Performance | sql_exporter | slow query, deadlock, connections |
| 07 | Logs | Loki | ค้นหา log ทุก service, filter by correlationId |
| 08 | Traces | Tempo | waterfall trace ข้าม services |

---

## Docker Services สุดท้ายใน Monitor Stack

```
monitor-api         :5000   — .NET 8 aggregator + data quality
grafana             :3000   — Dashboard UI
prometheus          :9090   — Metrics storage (90d retention)
blackbox-exporter   :9115   — HTTP probing
cadvisor            :8088   — Container resource metrics
sql-exporter        :9399   — MSSQL deep metrics
loki                :3100   — Log storage
promtail            (no port) — Log collector
tempo               :3200   — Trace storage
otel-collector      :4317   — OTEL receiver
```

---

*แผนนี้ครอบคลุม Observability ระดับ Production ครบ 3 pillars: Metrics + Logs + Traces พร้อม Data Quality Layer ที่เฉพาะสำหรับระบบนี้*
