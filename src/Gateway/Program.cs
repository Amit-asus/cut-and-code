using Serilog;
using Yarp.ReverseProxy.Configuration;


var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddSingleton<SalonBooking.Logging.LoggerFactory.ILoggerFactory,
                              SalonBooking.Logging.LoggerFactory.LoggerFactory>();

builder.AddServiceDefaults();

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClient", policy =>
    {
       
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var routes = new List<RouteConfig>
{
    new RouteConfig
    {
        RouteId = "identity-route",
        ClusterId = "identity-cluster",
        Match = new RouteMatch { Path = "/identity/{**catch-all}" },
        Transforms = new[]
        {
            new Dictionary<string, string> { { "PathRemovePrefix", "/identity" } }
        }
    },
    new RouteConfig
    {
        RouteId = "salon-route",
        ClusterId = "salon-cluster",
        Match = new RouteMatch { Path = "/salons/{**catch-all}" }
    },
    new RouteConfig
    {
        RouteId = "booking-route",
        ClusterId = "booking-cluster",
        Match = new RouteMatch { Path = "/bookings/{**catch-all}" }
    }
};

var clusters = new List<ClusterConfig>
{
    new ClusterConfig
    {
        ClusterId = "identity-cluster",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            { "destination1", new DestinationConfig { Address = "https://identity-api" } }
        }
    },
    new ClusterConfig
    {
        ClusterId = "salon-cluster",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            { "destination1", new DestinationConfig { Address = "https://salon-api" } }
        }
    },
    new ClusterConfig
    {
        ClusterId = "booking-cluster",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            { "destination1", new DestinationConfig { Address = "https://booking-api" } }
        }
    }
};


builder.Services.AddReverseProxy()
    .LoadFromMemory(routes, clusters)
    .AddServiceDiscoveryDestinationResolver();



var app = builder.Build();

app.UseCors("WebClient");

app.MapDefaultEndpoints();

app.MapReverseProxy();

app.Run();
