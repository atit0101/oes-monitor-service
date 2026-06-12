using Microsoft.EntityFrameworkCore;
using MonitorApi.Data.Entities;

namespace MonitorApi.Data;

public class MonitorDbContext(DbContextOptions<MonitorDbContext> options) : DbContext(options)
{
    public DbSet<ServiceHealthRecord> ServiceHealthRecords => Set<ServiceHealthRecord>();
    public DbSet<DataQualityRecord> DataQualityRecords => Set<DataQualityRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServiceHealthRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ServiceName, x.Environment, x.CheckedAt });
            e.Property(x => x.RawJson).HasColumnType("TEXT");
        });

        modelBuilder.Entity<DataQualityRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RuleName, x.Environment, x.CheckedAt });
        });
    }
}
