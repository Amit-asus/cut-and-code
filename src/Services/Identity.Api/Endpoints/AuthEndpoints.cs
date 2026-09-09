using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // Public sign-up. Always creates a Customer — never lets a caller self-assign Staff/Admin.
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

        // Verifies credentials and issues a JWT. Same generic 401 for "no such user" and "wrong password" to avoid user-enumeration.
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

        // Returns the caller's own identity from their token's claims. Requires any valid JWT.
        app.MapGet("/me", (ClaimsPrincipal user) =>
        {
            var id = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var email = user.FindFirstValue(JwtRegisteredClaimNames.Email);
            var role = user.FindFirstValue(ClaimTypes.Role);

            return Results.Ok(new { id, email, role });
        }).RequireAuthorization();

        // Admin-only: creates a user with any role (Customer/Staff/Admin). Requires the "Admin" role, not just any valid token.
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
    }
}
