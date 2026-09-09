using Microsoft.EntityFrameworkCore;

namespace SalonBooking.BookingApi;

public static class DatabaseExtensions
{
    public static void MigrateDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        db.Database.Migrate();
    }
}
