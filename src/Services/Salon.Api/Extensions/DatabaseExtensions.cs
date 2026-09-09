using Microsoft.EntityFrameworkCore;

namespace SalonBooking.SalonApi;

public static class DatabaseExtensions
{
    public static void MigrateDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SalonDbContext>();
        db.Database.Migrate();
    }
}
