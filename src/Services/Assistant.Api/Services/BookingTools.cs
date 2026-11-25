using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SalonBooking.AssistantApi;

public class BookingTools(HttpClient httpClient, SalonCatalogSource salonCatalog, ILogger<BookingTools> logger)
{
    private const int MaxSalonsReturned = 20;
    private static readonly TimeZoneInfo Ist = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    private static readonly object LoginRequired = new
    {
        error = "login_required",
        message = "The customer must log in to check times or book."
    };

    public static DateOnly TodayInIst => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Ist));

    public static object Declarations { get; } = new[]
    {
        new
        {
            functionDeclarations = new object[]
            {
                new
                {
                    name = "search_salons",
                    description = "Live list of salons with their services, prices and ids. Use it to find which salon offers a service and to get the salonId/serviceId needed by the other tools.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new { type = "string", description = "Optional salon or service name to filter by, e.g. 'haircut' or 'Aura Luxe'." }
                        }
                    }
                },
                new
                {
                    name = "get_availability",
                    description = "Free and booked 1-hour slots (IST) for one service on one date. Requires the customer to be logged in.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            serviceId = new { type = "string", description = "serviceId from search_salons." },
                            date = new { type = "string", description = "Date in yyyy-MM-dd format." }
                        },
                        required = new[] { "serviceId", "date" }
                    }
                },
                new
                {
                    name = "propose_booking",
                    description = "Shows the customer a booking card with a Confirm button. It does NOT book: the booking is only made when the customer presses Confirm. Call it only when salon, service, date and time are all known.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            salonId = new { type = "string", description = "salonId from search_salons." },
                            serviceId = new { type = "string", description = "serviceId from search_salons." },
                            date = new { type = "string", description = "Date in yyyy-MM-dd format." },
                            time = new { type = "string", description = "Start time in 24-hour HH:mm format (IST), e.g. 15:00." }
                        },
                        required = new[] { "salonId", "serviceId", "date", "time" }
                    }
                }
            }
        }
    };

    public async Task<object> RunAsync(string name, JsonElement args, ToolContext context, CancellationToken ct)
    {
        try
        {
            return name switch
            {
                "search_salons" => await SearchSalonsAsync(args, ct),
                "get_availability" => await GetAvailabilityAsync(args, context, ct),
                "propose_booking" => await ProposeBookingAsync(args, context, ct),
                _ => new { error = $"Unknown tool '{name}'." }
            };
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Assistant tool {Tool} failed.", name);
            return new { error = "The lookup failed. Tell the customer something went wrong and suggest the booking page." };
        }
    }

    private async Task<object> SearchSalonsAsync(JsonElement args, CancellationToken ct)
    {
        var query = GetString(args, "query")?.Trim();
        var salons = await salonCatalog.GetSalonsAsync(ct);

        var matches = salons;
        var matchedQuery = false;
        if (!string.IsNullOrEmpty(query))
        {
            var filtered = salons
                .Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || s.Services.Any(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (filtered.Count > 0)
            {
                matches = filtered;
                matchedQuery = true;
            }
        }

        return new
        {
            matchedQuery,
            salons = matches.Take(MaxSalonsReturned).Select(s => new
            {
                salonId = s.Id,
                name = s.Name,
                address = s.Address,
                services = s.Services.Select(x => new { serviceId = x.Id, name = x.Name, priceInRupees = x.Price })
            })
        };
    }

    private async Task<object> GetAvailabilityAsync(JsonElement args, ToolContext context, CancellationToken ct)
    {
        if (!context.IsLoggedIn)
        {
            return LoginRequired;
        }

        if (!TryGetGuid(args, "serviceId", out var serviceId))
        {
            return new { error = "serviceId must be a serviceId from search_salons." };
        }

        if (!TryGetDate(args, "date", out var date))
        {
            return new { error = "date must be in yyyy-MM-dd format." };
        }

        if (date < TodayInIst)
        {
            return new { error = "That date is in the past." };
        }

        var (slots, error) = await FetchSlotsAsync(serviceId, date, context, ct);
        if (error is not null)
        {
            return error;
        }

        var upcoming = slots!.Where(s => s.StartTime > DateTime.UtcNow).ToList();
        return new
        {
            date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            timeZone = "IST",
            freeSlots = upcoming.Where(s => !s.Booked).Select(s => ToIstTime(s.StartTime)),
            bookedSlots = upcoming.Where(s => s.Booked).Select(s => ToIstTime(s.StartTime))
        };
    }

    private async Task<object> ProposeBookingAsync(JsonElement args, ToolContext context, CancellationToken ct)
    {
        if (!context.IsLoggedIn)
        {
            return LoginRequired;
        }

        if (!TryGetGuid(args, "salonId", out var salonId) || !TryGetGuid(args, "serviceId", out var serviceId))
        {
            return new { error = "salonId and serviceId must come from search_salons." };
        }

        if (!TryGetDate(args, "date", out var date))
        {
            return new { error = "date must be in yyyy-MM-dd format." };
        }

        if (!TimeOnly.TryParseExact(GetString(args, "time"), ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return new { error = "time must be in 24-hour HH:mm format, e.g. 15:00." };
        }

        var salons = await salonCatalog.GetSalonsAsync(ct);
        var salon = salons.FirstOrDefault(s => s.Id == salonId);
        var service = salon?.Services.FirstOrDefault(x => x.Id == serviceId);
        if (salon is null || service is null)
        {
            return new { error = "That salon and service combination was not found. Use ids from search_salons." };
        }

        var (slots, error) = await FetchSlotsAsync(serviceId, date, context, ct);
        if (error is not null)
        {
            return error;
        }

        var wanted = time.ToString("HH:mm", CultureInfo.InvariantCulture);
        var slot = slots!.FirstOrDefault(s => ToIstTime(s.StartTime) == wanted);
        if (slot is null)
        {
            return new { error = "There is no slot at that time. Slots start on the hour from 09:00 to 20:00 IST." };
        }

        if (slot.StartTime <= DateTime.UtcNow)
        {
            return new { error = "That time has already passed." };
        }

        if (slot.Booked)
        {
            return new
            {
                error = "That slot is already booked.",
                freeSlots = slots!.Where(s => !s.Booked && s.StartTime > DateTime.UtcNow).Select(s => ToIstTime(s.StartTime))
            };
        }

        context.Proposal = new BookingProposal(salon.Id, service.Id, salon.Name, service.Name, service.Price, slot.StartTime, slot.EndTime);
        logger.LogInformation("Assistant proposed booking: service {ServiceId} at {StartTime:o}", service.Id, slot.StartTime);

        return new
        {
            status = "confirmation_card_shown",
            salon = salon.Name,
            service = service.Name,
            date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            time = wanted,
            priceInRupees = service.Price,
            instruction = "NOT booked yet. Tell the customer to check the card and press Confirm to book."
        };
    }

    private async Task<(List<Slot>? Slots, object? Error)> FetchSlotsAsync(Guid serviceId, DateOnly date, ToolContext context, CancellationToken ct)
    {
        var url = $"https+http://booking-api/bookings/availability?serviceId={serviceId}&date={date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", context.Authorization);

        using var response = await httpClient.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return (null, LoginRequired);
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Availability lookup failed: {StatusCode}", response.StatusCode);
            return (null, new { error = "Could not load availability right now." });
        }

        var slots = await response.Content.ReadFromJsonAsync<List<Slot>>(JsonSerializerOptions.Web, ct) ?? [];
        return (slots.Select(s => s with { StartTime = AsUtc(s.StartTime), EndTime = AsUtc(s.EndTime) }).ToList(), null);
    }

    private static string ToIstTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, Ist).ToString("HH:mm", CultureInfo.InvariantCulture);

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string? GetString(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object
        && args.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool TryGetGuid(JsonElement args, string name, out Guid value) =>
        Guid.TryParse(GetString(args, name), out value);

    private static bool TryGetDate(JsonElement args, string name, out DateOnly value) =>
        DateOnly.TryParseExact(GetString(args, name), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    private record Slot(DateTime StartTime, DateTime EndTime, bool Booked);
}
