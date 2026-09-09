using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;

namespace SalonBooking.SalonApi;

public static class SalonEndpoints
{
    public static void MapSalonEndpoints(this WebApplication app)
    {
        // Create
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

        // Read all
        app.MapGet("/salons", async (SalonDbContext db) =>
        {
            var salons = await db.Salons.ToListAsync();
            return Results.Ok(salons);
        });

        // Read one
        app.MapGet("/salons/{id}", async (Guid id, SalonDbContext db) =>
        {
            // FindAsync doesn't load navigation properties — Include is required or
            // salon.Services always comes back empty regardless of what's in the DB.
            var salon = await db.Salons.Include(s => s.Services).FirstOrDefaultAsync(s => s.Id == id);
            return salon is not null ? Results.Ok(salon) : Results.NotFound();
        });

        // Update
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

        // Delete
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

        // Upload image
        app.MapPost("/salons/{id}/image", async (Guid id, IFormFile file, SalonDbContext db, BlobContainerClient blobContainerClient) =>
        {
            var salon = await db.Salons.FindAsync(id);
            if (salon is null)
            {
                return Results.NotFound();
            }

            // Blob containers are private by default — without this, the ImageUrl we hand back
            // to the browser 403s when used directly as an <img src>, since there's no SAS token.
            // PublicAccessType.Blob allows anonymous read of blobs (not container listing).
            // NOTE: CreateIfNotExistsAsync only applies the access type at creation time — it's a
            // no-op (access unchanged) if the container already exists, which it always will after
            // the first upload. SetAccessPolicyAsync applies unconditionally on every request, so a
            // container created before this fix existed still gets flipped to public.
            await blobContainerClient.CreateIfNotExistsAsync();
            await blobContainerClient.SetAccessPolicyAsync(Azure.Storage.Blobs.Models.PublicAccessType.Blob);

            // give the blob a unique name so re-uploads don't collide/overwrite each other's cache
            var blobName = $"{id}/{Guid.NewGuid()}-{file.FileName}";
            var blobClient = blobContainerClient.GetBlobClient(blobName);

            await using (var stream = file.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, overwrite: true);
            }

            salon.ImageUrl = blobClient.Uri.ToString();
            await db.SaveChangesAsync();

            return Results.Ok(salon);
        }).RequireAuthorization(policy => policy.RequireRole("Staff", "Admin"))
          .DisableAntiforgery();

        // Create a service under a salon
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

            // Return a plain shape, not the tracked entity — service.Salon and salon.Services
            // point at each other (EF's navigation fixup), which crashes JSON serialization
            // with "possible object cycle" if the raw entity is returned instead.
            return Results.Created($"/salons/{salonId}/services/{service.Id}", new
            {
                service.Id,
                service.SalonId,
                service.Name,
                service.Price
            });
        }).RequireAuthorization(policy => policy.RequireRole("Staff", "Admin"));

        // Update a service
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

            // Same plain-shape reasoning as Create — avoid serializing the Salon nav property.
            return Results.Ok(new
            {
                service.Id,
                service.SalonId,
                service.Name,
                service.Price
            });
        }).RequireAuthorization(policy => policy.RequireRole("Staff", "Admin"));

        // Delete a service
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
    }
}
