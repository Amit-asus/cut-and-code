using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace SalonBooking.AssistantApi;

public static class DatabaseExtensions
{
    public static void AddAssistantDatabase(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("assistantdb");

        builder.Services.AddDbContext<AssistantDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

        builder.EnrichNpgsqlDbContext<AssistantDbContext>();
    }

    public static void MigrateDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssistantDbContext>();
        db.Database.Migrate();
    }
}
