using Microsoft.EntityFrameworkCore;

namespace SalonBooking.SalonApi;

public static class SalonEndpoints
{
    public static void MapSalonEndpoints(this WebApplication app)
    {
        app.MapPost("/salons", async (CreateSalonRequest request, SalonDbContext db) =>
        {
            var salon = new Salon
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Address = request.Address,
                OwnerId = request.OwnerId,
                CreatedAt = DateTime.UtcNow
            };

            db.Salons.Add(salon);
            await db.SaveChangesAsync();

            return Results.Created($"/salons/{salon.Id}", salon);
        }).RequireAuthorization(policy => policy.RequireRole("Staff", "Admin"));

        app.MapGet("/salons", async (SalonDbContext db) =>
        {
            var salons = await db.Salons.ToListAsync();
            return Results.Ok(salons);
        });

        app.MapGet("/salons/{id}", async (Guid id, SalonDbContext db) =>
        {
            var salon = await db.Salons.Include(s => s.Services).FirstOrDefaultAsync(s => s.Id == id);
            return salon is not null ? Results.Ok(salon) : Results.NotFound();
        });

        app.MapPut("/salons/{id}", async (Guid id, UpdateSalonRequest request, SalonDbContext db) =>
        {
            var salon = await db.Salons.FindAsync(id);
            if (salon is null)
            {
                return Results.NotFound();
            }

            salon.Name = request.Name;
            salon.Address = request.Address;
            await db.SaveChangesAsync();

            return Results.Ok(salon);
        }).RequireAuthorization(policy => policy.RequireRole("Staff", "Admin"));

        app.MapDelete("/salons/{id}", async (Guid id, SalonDbContext db) =>
        {
            var salon = await db.Salons.FindAsync(id);
            if (salon is null)
            {
                return Results.NotFound();
            }

            db.Salons.Remove(salon);
            await db.SaveChangesAsync();

            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole("Staff", "Admin"));

        app.MapPost("/salons/{id}/image", async (Guid id, IFormFile file, SalonDbContext db, IImageStorage storage) =>
        {
            var salon = await db.Salons.FindAsync(id);
            if (salon is null)
            {
                return Results.NotFound();
            }

            var name = $"{id}/{Guid.NewGuid()}-{file.FileName}";

            await using (var stream = file.OpenReadStream())
            {
                salon.ImageUrl = await storage.UploadAsync(name, stream, file.ContentType);
            }

            await db.SaveChangesAsync();

            return Results.Ok(salon);
        }).RequireAuthorization(policy => policy.RequireRole("Staff", "Admin"))
          .DisableAntiforgery();

        app.MapPost("/salons/{salonId}/services", async (Guid salonId, CreateServiceRequest request, SalonDbContext db) =>
        {
            var salon = await db.Salons.FindAsync(salonId);
            if (salon is null)
            {
                return Results.NotFound();
            }

            var service = new Service
            {
                Id = Guid.NewGuid(),
                SalonId = salonId,
                Name = request.Name,
                Price = request.Price
            };

            db.Services.Add(service);
            await db.SaveChangesAsync();

            return Results.Created($"/salons/{salonId}/services/{service.Id}", new
            {
                service.Id,
                service.SalonId,
                service.Name,
                service.Price
            });
        }).RequireAuthorization(policy => policy.RequireRole("Staff", "Admin"));

        app.MapPut("/salons/{salonId}/services/{serviceId}", async (Guid salonId, Guid serviceId, UpdateServiceRequest request, SalonDbContext db) =>
        {
            var service = await db.Services.FindAsync(serviceId);
            if (service is null || service.SalonId != salonId)
            {
                return Results.NotFound();
            }

            service.Name = request.Name;
            service.Price = request.Price;
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                service.Id,
                service.SalonId,
                service.Name,
                service.Price
            });
        }).RequireAuthorization(policy => policy.RequireRole("Staff", "Admin"));

        app.MapDelete("/salons/{salonId}/services/{serviceId}", async (Guid salonId, Guid serviceId, SalonDbContext db) =>
        {
            var service = await db.Services.FindAsync(serviceId);
            if (service is null || service.SalonId != salonId)
            {
                return Results.NotFound();
            }

            db.Services.Remove(service);
            await db.SaveChangesAsync();

            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole("Staff", "Admin"));

        app.MapGet("/internal/salons/{salonId}/services/{serviceId}", async (Guid salonId, Guid serviceId, SalonDbContext db) =>
        {
            var salon = await db.Salons.FindAsync(salonId);
            if (salon is null)
            {
                return Results.NotFound();
            }

            var service = await db.Services.FindAsync(serviceId);
            if (service is null || service.SalonId != salonId)
            {
                return Results.NotFound();
            }

            return Results.Ok(new { SalonName = salon.Name, ServiceName = service.Name });
        });
    }
}
