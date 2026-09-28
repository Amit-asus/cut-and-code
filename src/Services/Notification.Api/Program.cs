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

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<SalonBooking.Logging.LoggerFactory.ILoggerFactory,
                              SalonBooking.Logging.LoggerFactory.LoggerFactory>();

builder.AddServiceDefaults();

builder.AddAzureServiceBusClient("servicebus");

// Named HttpClient the enrichment service uses to call Identity.Api's and Salon.Api's
// internal endpoints. AddServiceDefaults() wires Aspire service discovery into
// HttpClientFactory-created clients, so "https://identity-api"/"https://salon-api" resolve
// the same way they do in the Gateway's YARP config — no hardcoded ports.
builder.Services.AddHttpClient<BookingEnrichmentService>();
// Typed client for the Gemini call. ServiceDefaults' standard resilience handler caps each
// attempt at 10s — fine for our internal services, too short for an LLM that "thinks"
// before answering. Swap it for a copy with longer timeouts on this client only.
// RemoveAllResilienceHandlers is marked experimental (EXTEXP0001) — opted in here only.
#pragma warning disable EXTEXP0001
builder.Services.AddHttpClient<AiEmailWriter>()
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
        options.Retry.MaxRetryAttempts = 1;
        // Polly validation requires the circuit breaker's sampling window >= 2x attempt timeout.
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
    });
#pragma warning restore EXTEXP0001
builder.Services.AddScoped<EmailSender>();
builder.Services.AddHostedService<BookingNotificationConsumer>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
