using System.Net.Http.Json;

namespace SalonBooking.NotificationApi;

public record EnrichedBooking(string CustomerEmail, string SalonName, string ServiceName);

// Calls Identity.Api's and Salon.Api's internal-only endpoints to turn the opaque Guids
// on a booking-created message into human-readable values for the notification email.
public class BookingEnrichmentService(HttpClient httpClient, ILogger<BookingEnrichmentService> logger)
{
    public async Task<EnrichedBooking?> EnrichAsync(BookingCreatedMessage message)
    {
        var userResponse = await httpClient.GetAsync($"https://identity-api/internal/users/{message.CustomerId}");
        if (!userResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Could not resolve user {CustomerId} for booking {BookingId}: {StatusCode}",
                message.CustomerId, message.BookingId, userResponse.StatusCode);
            return null;
        }

        var user = await userResponse.Content.ReadFromJsonAsync<UserResponse>();

        var salonResponse = await httpClient.GetAsync(
            $"https://salon-api/internal/salons/{message.SalonId}/services/{message.ServiceId}");
        if (!salonResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Could not resolve salon/service {SalonId}/{ServiceId} for booking {BookingId}: {StatusCode}",
                message.SalonId, message.ServiceId, message.BookingId, salonResponse.StatusCode);
            return null;
        }

        var salon = await salonResponse.Content.ReadFromJsonAsync<SalonServiceResponse>();

        if (user is null || salon is null)
        {
            return null;
        }

        return new EnrichedBooking(user.Email, salon.SalonName, salon.ServiceName);
    }

    private record UserResponse(Guid Id, string Email);
    private record SalonServiceResponse(string SalonName, string ServiceName);
}
