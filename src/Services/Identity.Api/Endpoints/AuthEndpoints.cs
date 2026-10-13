using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/register", async (RegisterRequest request, IdentityDbContext db, PasswordHasherService hasher) =>
        {
            var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (existingUser is not null)
            {
                return Results.Conflict("Email is already registered.");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                Role = UserRole.Customer,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = hasher.Hash(user, request.Password);

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return Results.Created($"/users/{user.Id}", new { user.Id, user.Email, user.Role });
        });

        app.MapPost("/login", async (LoginRequest request, IdentityDbContext db, PasswordHasherService hasher, TokenService tokenService) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user is null || !hasher.Verify(user, user.PasswordHash, request.Password))
            {
                return Results.Unauthorized();
            }

            var token = tokenService.GenerateToken(user);
            return Results.Ok(new { token });
        });

        app.MapGet("/me", (ClaimsPrincipal user) =>
        {
            var id = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var email = user.FindFirstValue(JwtRegisteredClaimNames.Email);
            var role = user.FindFirstValue(ClaimTypes.Role);

            return Results.Ok(new { id, email, role });
        }).RequireAuthorization();

        app.MapGet("/profile", async (ClaimsPrincipal user, IdentityDbContext db) =>
        {
            var id = Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            var profile = await db.Users
                .Where(u => u.Id == id)
                .Select(u => new { u.Id, u.Email, Role = u.Role.ToString(), u.CreatedAt })
                .FirstOrDefaultAsync();

            return profile is null ? Results.NotFound() : Results.Ok(profile);
        }).RequireAuthorization();

        app.MapPost("/admin/users", async (CreateUserRequest  request, IdentityDbContext db, PasswordHasherService hasher) =>
        {
            var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (existingUser is not null)
            {
                return Results.Conflict("Email is already registered.");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                Role = request.Role,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = hasher.Hash(user, request.Password);

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return Results.Created($"/users/{user.Id}", new { user.Id, user.Email, user.Role });
        }).RequireAuthorization(policy => policy.RequireRole("Admin"));

        app.MapGet("/internal/users/{id}", async (Guid id, IdentityDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            return user is not null ? Results.Ok(new { user.Id, user.Email }) : Results.NotFound();
        });
    }
}
