using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace SalonBooking.BookingApi;

public static class BookingEndpoints
{
    private static readonly JsonSerializerOptions CacheJsonOptions = new(JsonSerializerDefaults.Web);

    private static string AvailabilityCacheKey(Guid serviceId, DateOnly date) =>
        $"availability:{serviceId}:{date:yyyy-MM-dd}";

    private static async Task InvalidateAvailabilityAsync(IConnectionMultiplexer redis, Guid serviceId, DateTime startTimeUtc)
    {
        try
        {
            var date = DateOnly.FromDateTime(startTimeUtc);
            await redis.GetDatabase().KeyDeleteAsync(AvailabilityCacheKey(serviceId, date));
        }
        catch (RedisException)
        {
        }
    }

    public static void MapBookingEndpoints(this WebApplication app)
    {
        app.MapPost("/bookings", async (CreateBookingRequest request, ClaimsPrincipal user, BookingDbContext db, IConnectionMultiplexer redis, IBookingEventPublisher publisher) =>
        {
            if (request.EndTime <= request.StartTime)
            {
                return Results.BadRequest("EndTime must be after StartTime.");
            }

            Guid customerId = Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

            var hasConflict = await db.Bookings.AnyAsync(b =>
                b.ServiceId == request.ServiceId &&
                b.Status == BookingStatus.Confirmed &&
                b.StartTime < request.EndTime &&
                b.EndTime > request.StartTime);

            if (hasConflict)
            {
                return Results.Conflict("This service is already booked for the requested time.");
            }

            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                SalonId = request.SalonId,
                ServiceId = request.ServiceId,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTime.UtcNow
            };

            db.Bookings.Add(booking);
            await db.SaveChangesAsync();

            await InvalidateAvailabilityAsync(redis, booking.ServiceId, booking.StartTime);
            await publisher.PublishBookingCreatedAsync(booking);

            return Results.Created($"/bookings/{booking.Id}", booking);
        }).RequireAuthorization();

        app.MapGet("/bookings/availability", async (Guid serviceId, DateOnly date, BookingDbContext db, IConnectionMultiplexer redis) =>
        {
            var cacheKey = AvailabilityCacheKey(serviceId, date);
            var redisDb = redis.GetDatabase();

            try
            {
                var cached = await redisDb.StringGetAsync(cacheKey);
                if (cached.HasValue)
                {
                    return Results.Text((string)cached!, "application/json");
                }
            }
            catch (RedisException)
            {
            }

            var istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var localMidnight = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            var dayStart = TimeZoneInfo.ConvertTimeToUtc(localMidnight, istZone);

            var bookedRanges = await db.Bookings
                .Where(b =>
                    b.ServiceId == serviceId &&
                    b.Status == BookingStatus.Confirmed &&
                    b.StartTime >= dayStart &&
                    b.StartTime < dayStart.AddDays(1))
                .Select(b => new { b.StartTime, b.EndTime })
                .ToListAsync();

            var slots = Enumerable.Range(9, 12).Select(hour =>
            {
                var start = dayStart.AddHours(hour);
                var end = start.AddHours(1);
                var booked = bookedRanges.Any(b => b.StartTime < end && b.EndTime > start);
                return new { StartTime = start, EndTime = end, Booked = booked };
            }).ToList();

            try
            {
                var json = JsonSerializer.Serialize(slots, CacheJsonOptions);
                await redisDb.StringSetAsync(cacheKey, json, TimeSpan.FromDays(1));
            }
            catch (RedisException)
            {
            }

            return Results.Ok(slots);
        }).RequireAuthorization();

        app.MapGet("/bookings", async (ClaimsPrincipal user, BookingDbContext db, int page = 1, int pageSize = 10) =>
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var customerId = Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            var query = db.Bookings.Where(b => b.CustomerId == customerId);

            var totalCount = await query.CountAsync();
            var bookings = await query
                .OrderByDescending(b => b.CreatedAt)
                .ThenByDescending(b => b.StartTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            return Results.Ok(new PagedResult<Booking>(bookings, page, pageSize, totalCount, totalPages));
        }).RequireAuthorization();

        app.MapDelete("/bookings/{id:guid}", async (Guid id, ClaimsPrincipal user, BookingDbContext db, IConnectionMultiplexer redis) =>
        {
            var customerId = Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            var booking = await db.Bookings.FindAsync(id);

            if (booking is null)
            {
                return Results.NotFound();
            }

            if (booking.CustomerId != customerId)
            {
                return Results.Forbid();
            }

            booking.Status = BookingStatus.Cancelled;
            await db.SaveChangesAsync();

            await InvalidateAvailabilityAsync(redis, booking.ServiceId, booking.StartTime);

            return Results.NoContent();
        }).RequireAuthorization();
    }
}
