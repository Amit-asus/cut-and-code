#:sdk Aspire.AppHost.Sdk@13.5.3
#:package Aspire.Hosting.AppHost@13.5.3
#:package Aspire.Hosting.PostgreSQL@13.5.3
#:property AspireUseCliBundle=true
#:package Aspire.Hosting.Redis@13.5.3
#:package Aspire.Hosting.Azure.Storage@13.5.3



using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);
var jwtSigningKey = "a-very-long-random-secret-key-change-this-later-32chars-min";


var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent);
var identityDb = postgres.AddDatabase("identitydb");
var salonDb = postgres.AddDatabase("salondb");

var bookingDb = postgres.AddDatabase("bookingdb");


//adding redis
var cache = builder.AddRedis("cache")
    .WithLifetime(ContainerLifetime.Persistent);

// Blob storage for salon images. RunAsEmulator() runs Azurite (a local fake
// Azure Storage) in a container instead of hitting a real Azure account —
// same idea as AddPostgres/AddRedis above, just for file storage.
// WithDataVolume persists the emulator's actual files across restarts,
// same reasoning as .WithLifetime(ContainerLifetime.Persistent) on postgres/cache,
// but for the data itself rather than the container.
// WithBlobPort pins Azurite's blob endpoint to a fixed host port. Without it, Aspire
// assigns a random port each run — fine for services that resolve each other via Aspire's
// service discovery, but salon-api bakes the *browser-facing* blob URL straight into
// ImageUrl (the browser hits blob storage directly, not through salon-api), so that URL
// must stay valid across restarts or every previously-uploaded image breaks.
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(x =>
    {
        x.WithDataVolume("storage-data");
        x.WithBlobPort(10000);
    });

// One container ("bucket") named salon-images — salon-api gets a client
// scoped directly to this container, it never needs to name/select it itself.
var salonImages = storage.AddBlobContainer("salon-images");



//adding projects
var identityApi = builder.AddProject("identity-api", "src/Services/Identity.Api/Identity.Api.csproj")
    .WithReference(identityDb)
    .WaitFor(identityDb)
    .WithReference(cache)
    .WaitFor(cache)
    .WithEnvironment("Seed__AdminEmail", "admin@salonbooking.local")
    .WithEnvironment("Seed__AdminPassword", "ChangeThisAdminPassword123")
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey);




var bookingApi = builder.AddProject("booking-api", "src/Services/Booking.Api/Booking.Api.csproj")
    .WithReference(bookingDb)
    .WaitFor(bookingDb)
    .WithReference(cache)
    .WaitFor(cache)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey);


var notificationApi = builder.AddProject("notification-api", "src/Services/Notification.Api/Notification.Api.csproj");

var salonApi = builder.AddProject("salon-api", "src/Services/Salon.Api/Salon.Api.csproj")
    .WithReference(salonDb)
    .WaitFor(salonDb)
    // grants salon-api the connection info for the salon-images blob container
    // (Aspire injects it the same way it injects the salondb connection string)
    .WithReference(salonImages)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey);

builder.AddProject("gateway", "src/Gateway/Gateway.csproj")
    .WithReference(identityApi)
    .WithReference(bookingApi)
    .WithReference(notificationApi)
    .WithReference(salonApi);

builder.Build().Run();