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
    }
}
