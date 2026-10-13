using Azure.Messaging.ServiceBus;

namespace SalonBooking.BookingApi;

public class BookingEventPublisher(ServiceBusClient client, ILogger<BookingEventPublisher> logger) : IBookingEventPublisher
{
    private const string QueueName = "booking-created";

    public async Task PublishBookingCreatedAsync(Booking booking)
    {
        try
        {
            var sender = client.CreateSender(QueueName);
            var message = new ServiceBusMessage(IBookingEventPublisher.ToJson(booking));
            await sender.SendMessageAsync(message);
        }
        catch (ServiceBusException ex)
        {
            logger.LogWarning(ex, "Failed to publish booking-created event for booking {BookingId}", booking.Id);
        }
    }
}
