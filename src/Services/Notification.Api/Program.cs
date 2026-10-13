using SalonBooking.NotificationApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddOpenApi();

builder.Services.AddSingleton<SalonBooking.Logging.LoggerFactory.ILoggerFactory,
                              SalonBooking.Logging.LoggerFactory.LoggerFactory>();

builder.AddServiceDefaults();

builder.Services.AddHttpClient<BookingEnrichmentService>();
#pragma warning disable EXTEXP0001
builder.Services.AddHttpClient<AiEmailWriter>()
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
        options.Retry.MaxRetryAttempts = 1;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
    });
#pragma warning restore EXTEXP0001
builder.Services.AddScoped<EmailSender>();
builder.AddBookingNotificationConsumer();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
