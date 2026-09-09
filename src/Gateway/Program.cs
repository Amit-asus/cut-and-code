var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClient", policy =>
    {
        policy.WithOrigins("http://localhost:5174") // your Vite dev server origin
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
