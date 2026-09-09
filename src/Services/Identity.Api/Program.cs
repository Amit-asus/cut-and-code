using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSingleton<PasswordHasherService>();
builder.Services.AddSingleton<TokenService>();
builder.AddJwtAuth();

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<IdentityDbContext>("identitydb");
builder.AddRedisClient("cache");


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    db.Database.Migrate();

    var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasherService>();
    var adminExists = db.Users.Any(u => u.Role == UserRole.Admin);
    if (!adminExists)
    {
        var adminEmail = app.Configuration["Seed:AdminEmail"];
        var adminPassword = app.Configuration["Seed:AdminPassword"];

        if (!string.IsNullOrEmpty(adminEmail) && !string.IsNullOrEmpty(adminPassword))
        {
            var admin = new User
            {
                Id = Guid.NewGuid(),
                Email = adminEmail,
                Role = UserRole.Admin,
                CreatedAt = DateTime.UtcNow
            };
            admin.PasswordHash = hasher.Hash(admin, adminPassword);

            db.Users.Add(admin);
            db.SaveChanges();
        }
    }
}


app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();

app.Run();

