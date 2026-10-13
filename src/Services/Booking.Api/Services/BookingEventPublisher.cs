using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace SalonBooking.BookingApi;

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
