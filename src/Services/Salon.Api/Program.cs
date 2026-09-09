using SalonBooking.SalonApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<SalonDbContext>("salondb");
builder.AddAzureBlobContainerClient("salon-images");

builder.AddJwtAuth();



var app = builder.Build();
app.MigrateDatabase();


app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();


app.MapSalonEndpoints();

app.Run();
