using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Listo.Notification.Infrastructure.Data;

/// <summary>
/// Design-time factory for NotificationDbContext to support EF Core migrations.
/// </summary>
public class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NotificationDbContext>();
        
        // Use a connection string for design-time migrations
        // This will be replaced with actual connection string at runtime
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=ListoNotification;Trusted_Connection=True;MultipleActiveResultSets=true",
            b => b.MigrationsAssembly("Listo.Notification.Infrastructure"));

        return new NotificationDbContext(optionsBuilder.Options);
    }
}
