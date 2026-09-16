using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FifthBox.ServerManager.Storage;

/// dotnet ef only, the real connection string comes from AddStorage
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
