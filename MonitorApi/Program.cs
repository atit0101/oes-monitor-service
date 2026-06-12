using Microsoft.EntityFrameworkCore;
using MonitorApi.Data;
using MonitorApi.Services;
using MonitorApi.Workers;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────────────────
// Database — SQLite (ไม่พึ่ง MSSQL production)
// ──────────────────────────────────────────────────────────
var dbPath = Path.Combine(
    builder.Configuration["DataPath"] ?? "/app/data",
    "monitor.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContext<MonitorDbContext>(opts =>
    opts.UseSqlite($"Data Source={dbPath}"));

// ──────────────────────────────────────────────────────────
// HTTP Client สำหรับ polling health endpoints
// ──────────────────────────────────────────────────────────
builder.Services.AddHttpClient("health", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
    c.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ──────────────────────────────────────────────────────────
// Services
// ──────────────────────────────────────────────────────────
builder.Services.AddScoped<HealthPollerService>();
builder.Services.AddSingleton<TopologyBuilderService>();

// ──────────────────────────────────────────────────────────
// Background Workers
// ──────────────────────────────────────────────────────────
builder.Services.AddHostedService<HealthAggregatorWorker>();
builder.Services.AddHostedService<DataQualityWorker>();

// ──────────────────────────────────────────────────────────
// API
// ──────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "OES Monitor API", Version = "v1" });
});

builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(p => p
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
});

var app = builder.Build();

// ──────────────────────────────────────────────────────────
// Auto migrate SQLite on startup
// ──────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MonitorDbContext>();
    await db.Database.MigrateAsync();
}

// ──────────────────────────────────────────────────────────
// Middleware
// ──────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRouting();

// Prometheus metrics endpoint (เพื่อให้ Prometheus scrape ได้)
app.UseHttpMetrics();
app.MapMetrics("/metrics");

app.MapControllers();

// Health endpoint ของ MonitorApi เอง
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "monitor-api",
    timestamp = DateTime.UtcNow,
}));

app.Run();
