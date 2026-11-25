using System.Threading.RateLimiting;
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

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 } configured
    ? configured
    : ["http://localhost:5173", "http://localhost:5174"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("assistant", context =>
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
        var clientKey = !string.IsNullOrWhiteSpace(forwardedFor)
            ? forwardedFor.Split(',')[0].Trim()
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:Assistant:PermitLimit", 10),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

var routes = new List<RouteConfig>
{
    new RouteConfig
    {
        RouteId = "assistant-route",
        ClusterId = "assistant-cluster",
        Match = new RouteMatch { Path = "/assistant/{**catch-all}" },
        RateLimiterPolicy = "assistant"
    },
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
        ClusterId = "assistant-cluster",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            { "destination1", new DestinationConfig { Address = "https+http://assistant-api" } }
        }
    },
    new ClusterConfig
    {
        ClusterId = "identity-cluster",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            { "destination1", new DestinationConfig { Address = "https+http://identity-api" } }
        }
    },
    new ClusterConfig
    {
        ClusterId = "salon-cluster",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            { "destination1", new DestinationConfig { Address = "https+http://salon-api" } }
        }
    },
    new ClusterConfig
    {
        ClusterId = "booking-cluster",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            { "destination1", new DestinationConfig { Address = "https+http://booking-api" } }
        }
    }
};


builder.Services.AddReverseProxy()
    .LoadFromMemory(routes, clusters)
    .AddServiceDiscoveryDestinationResolver();



var app = builder.Build();

app.UseCors("WebClient");

app.UseRateLimiter();

app.MapDefaultEndpoints();

app.MapReverseProxy();

app.Run();
