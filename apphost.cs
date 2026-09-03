#:sdk Aspire.AppHost.Sdk@13.5.3
#:package Aspire.Hosting.AppHost@13.5.3
#:package Aspire.Hosting.PostgreSQL@13.5.3
#:property AspireUseCliBundle=true

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var identityDb = postgres.AddDatabase("identitydb");

//adding projects
var identityApi = builder.AddProject("identity-api", "src/Services/Identity.Api/Identity.Api.csproj")
    .WithReference(identityDb)
    .WaitFor(identityDb);

var bookingApi = builder.AddProject("booking-api", "src/Services/Booking.Api/Booking.Api.csproj");

var notificationApi = builder.AddProject("notification-api", "src/Services/Notification.Api/Notification.Api.csproj");

var salonApi = builder.AddProject("salon-api", "src/Services/Salon.Api/Salon.Api.csproj");

builder.AddProject("gateway", "src/Gateway/Gateway.csproj")
    .WithReference(identityApi)
    .WithReference(bookingApi)
    .WithReference(notificationApi)
    .WithReference(salonApi);

builder.Build().Run();