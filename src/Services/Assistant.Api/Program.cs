using SalonBooking.AssistantApi;
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
builder.AddAssistantDatabase();
builder.AddJwtAuth();

#pragma warning disable EXTEXP0001
builder.Services.AddHttpClient<GeminiClient>()
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(8);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.Retry.ShouldHandle = _ => ValueTask.FromResult(false);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
    });
#pragma warning restore EXTEXP0001
builder.Services.AddHttpClient<SalonCatalogSource>();
builder.Services.AddHttpClient<BookingTools>();
builder.Services.AddScoped<SupportDocsSource>();
builder.Services.AddScoped<AssistantService>();
builder.Services.AddSingleton<KnowledgeIndexer>();
builder.Services.AddHostedService<ReindexScheduler>();

var app = builder.Build();
app.MigrateDatabase();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapAssistantEndpoints();

app.Run();
