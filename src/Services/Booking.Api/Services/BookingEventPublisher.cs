using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace SalonBooking.BookingApi;

// Publishes a "booking created" message to Service Bus after a booking is saved.
// Fire-and-forget from the caller's perspective: a booking has already succeeded in
// Postgres by the time this runs, so a Service Bus outage should degrade to "no
// notification sent" rather than fail the booking response — same philosophy as the
// Redis cache invalidation above (best-effort, swallow the failure, log and move on).
public class BookingEventPublisher(ServiceBusClient client, ILogger<BookingEventPublisher> logger)
{
    private const string QueueName = "booking-created";

    public async Task PublishBookingCreatedAsync(Booking booking)
    {
        try
        {
            var sender = client.CreateSender(QueueName);
            var payload = new
            {
                BookingId = booking.Id,
                CustomerId = booking.CustomerId,
                SalonId = booking.SalonId,
                ServiceId = booking.ServiceId,
                StartTime = booking.StartTime,
                EndTime = booking.EndTime
            };

            var message = new ServiceBusMessage(JsonSerializer.Serialize(payload));
            await sender.SendMessageAsync(message);
        }
        catch (ServiceBusException ex)
        {
            logger.LogWarning(ex, "Failed to publish booking-created event for booking {BookingId}", booking.Id);
        }
    }
}
