namespace SalonBooking.NotificationApi;

public class BookingNotificationHandler(
    BookingEnrichmentService enrichment,
    EmailSender emailSender,
    ILogger<BookingNotificationHandler> logger)
{
    public async Task HandleAsync(BookingCreatedMessage message)
    {
        logger.LogInformation("Received booking-created event for booking {BookingId}", message.BookingId);

        var enriched = await enrichment.EnrichAsync(message);
        if (enriched is not null)
        {
            await emailSender.SendBookingConfirmationAsync(enriched, message);
        }
    }
}
