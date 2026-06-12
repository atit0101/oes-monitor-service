using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MonitorApi.Data;

public class MonitorDbContextFactory : IDesignTimeDbContextFactory<MonitorDbContext>
{
    public MonitorDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<MonitorDbContext>()
            .UseSqlite("Data Source=monitor-design.db")
            .Options;
        return new MonitorDbContext(opts);
    }
}
