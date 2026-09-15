using Serilog;

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
        // Vite's default port is 5173, but it auto-increments if that port is taken,
        // so both 5173 and the earlier-hardcoded 5174 are allowed here to survive either case.
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
            .AllowAnyHeader()
            .AllowAnyMethod();
        // add .AllowCredentials() only if you ever send cookies; you're using
        // a Bearer token in localStorage, so it's not needed here
    });
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();


var app = builder.Build();

app.UseCors("WebClient");

app.MapDefaultEndpoints();

app.MapReverseProxy();

app.Run();
