#:sdk Aspire.AppHost.Sdk@13.5.3
#:package Aspire.Hosting.AppHost@13.5.3
#:package Aspire.Hosting.PostgreSQL@13.5.3
#:property AspireUseCliBundle=true
#:package Aspire.Hosting.Redis@13.5.3
#:package Aspire.Hosting.Azure.Storage@13.5.3
#:package Aspire.Hosting.Azure.ServiceBus@13.5.3



using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);
var jwtSigningKey = "a-very-long-random-secret-key-change-this-later-32chars-min";


var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("postgres-data")
    .WithPgAdmin(pgAdmin => pgAdmin.WithLifetime(ContainerLifetime.Persistent));
var identityDb = postgres.AddDatabase("identitydb");
var salonDb = postgres.AddDatabase("salondb");

var bookingDb = postgres.AddDatabase("bookingdb");


var cache = builder.AddRedis("cache")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("cache-data")
    .WithRedisCommander(commander => commander.WithLifetime(ContainerLifetime.Persistent));

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(x =>
    {
        x.WithDataVolume("storage-data");
        x.WithBlobPort(10000);
    });

var salonImages = storage.AddBlobContainer("salon-images");

var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator();

var bookingCreatedQueue = serviceBus.AddServiceBusQueue("booking-created");



var identityApi = builder.AddProject("identity-api", "src/Services/Identity.Api/Identity.Api.csproj")
    .WithReference(identityDb)
    .WaitFor(identityDb)
    .WithEnvironment("Seed__AdminEmail", "admin@salonbooking.local")
    .WithEnvironment("Seed__AdminPassword", "ChangeThisAdminPassword123")
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey);




var bookingApi = builder.AddProject("booking-api", "src/Services/Booking.Api/Booking.Api.csproj")
    .WithReference(bookingDb)
    .WaitFor(bookingDb)
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(serviceBus)
    .WaitFor(serviceBus)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey);


var salonApi = builder.AddProject("salon-api", "src/Services/Salon.Api/Salon.Api.csproj")
    .WithReference(salonDb)
    .WaitFor(salonDb)
    .WithReference(salonImages)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey);

var notificationApi = builder.AddProject("notification-api", "src/Services/Notification.Api/Notification.Api.csproj")
    .WithReference(serviceBus)
    .WaitFor(serviceBus)
    .WithReference(identityApi)
    .WithReference(salonApi);

builder.AddProject("gateway", "src/Gateway/Gateway.csproj")
    .WithReference(identityApi)
    .WithReference(bookingApi)
    .WithReference(notificationApi)
    .WithReference(salonApi);

builder.Build().Run();