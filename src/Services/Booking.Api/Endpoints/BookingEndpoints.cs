using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace SalonBooking.BookingApi;

public static class BookingEndpoints
{
    // Matches ASP.NET Core's default Results.Ok(...) serialization (camelCase property
    // names), so a cache hit and a cache miss return byte-for-byte-equivalent JSON shape.
    private static readonly JsonSerializerOptions CacheJsonOptions = new(JsonSerializerDefaults.Web);

    private static string AvailabilityCacheKey(Guid serviceId, DateOnly date) =>
        $"availability:{serviceId}:{date:yyyy-MM-dd}";

    // Best-effort cache invalidation: a booking write already succeeded in Postgres (the
    // source of truth) by the time this runs. If Redis is briefly unavailable, we swallow
    // the error rather than fail the booking — worst case, the picker shows a stale slot
    // for up to the cache's TTL, but the /bookings POST conflict check still prevents an
    // actual double-booking. Never let a caching concern block the core feature.
    private static async Task InvalidateAvailabilityAsync(IConnectionMultiplexer redis, Guid serviceId, DateTime startTimeUtc)
    {
        try
        {
            var date = DateOnly.FromDateTime(startTimeUtc);
            await redis.GetDatabase().KeyDeleteAsync(AvailabilityCacheKey(serviceId, date));
        }
        catch (RedisException)
        {
            // Ignored deliberately — see comment above.
        }
    }

    public static void MapBookingEndpoints(this WebApplication app)
    {
        // Create a booking. Rejects if the same Service already has an overlapping Confirmed booking.
        app.MapPost("/bookings", async (CreateBookingRequest request, ClaimsPrincipal user, BookingDbContext db, IConnectionMultiplexer redis) =>
        {
            if (request.EndTime <= request.StartTime)
            {
                return Results.BadRequest("EndTime must be after StartTime.");
            }

            var customerId = Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

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

            return Results.Created($"/bookings/{booking.Id}", booking);
        }).RequireAuthorization();

        // Fixed 1-hour slots for a service on a given day (9am-9pm IST), each flagged booked/available.
        // Any authenticated user can call this (needed to render the slot picker before booking) —
        // it only reveals whether a slot is taken, never which customer holds it.
        app.MapGet("/bookings/availability", async (Guid serviceId, DateOnly date, BookingDbContext db, IConnectionMultiplexer redis) =>
        {
            var cacheKey = AvailabilityCacheKey(serviceId, date);
            var redisDb = redis.GetDatabase();

            // Cache read is also best-effort: if Redis is unreachable, fall through and
            // compute from Postgres like normal — a cache outage should degrade to "slower",
            // never to "broken".
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
                // Ignored deliberately — fall through to computing from Postgres.
            }

            // "9am-9pm" means salon-local (IST) hours, not UTC — build midnight in IST, then
            // convert to UTC, since bookings are stored/compared in UTC (see CreateBooking above).
            // Without this, slots were literally 9:00-20:00 UTC, which the frontend then displayed
            // shifted by the browser's own local offset (e.g. 2:30 PM-1:30 AM for an IST viewer).
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
                // Ignored deliberately — the response below is still correct, we just won't cache it this time.
            }

            return Results.Ok(slots);
        }).RequireAuthorization();

        // List the current user's own bookings.
        app.MapGet("/bookings", async (ClaimsPrincipal user, BookingDbContext db) =>
        {
            var customerId = Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            var bookings = await db.Bookings.Where(b => b.CustomerId == customerId).ToListAsync();
            return Results.Ok(bookings);
        }).RequireAuthorization();

        // Cancel a booking (soft-delete: sets Status, doesn't remove the row). Only the owning customer can cancel it.
        // {id:guid} (not just {id}) so this doesn't swallow /bookings/availability — without the
        // constraint, "availability" matches {id} as a literal string and ASP.NET Core routes
        // GET /bookings/availability here too, then 405s since this route is DELETE-only.
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
