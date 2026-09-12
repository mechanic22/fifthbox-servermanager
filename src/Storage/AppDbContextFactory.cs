using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FifthBox.ServerManager.Storage;

/// <summary>
/// Lets `dotnet ef` build the context at design time without the Host. The connection string here is
/// tooling-only — the running app supplies its own via AddStorage(connectionString).
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=demoapp-design.db")
            .Options;
        return new AppDbContext(options);
    }
}
