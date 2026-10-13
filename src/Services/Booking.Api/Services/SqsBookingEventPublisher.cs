using Amazon.SQS;

namespace SalonBooking.BookingApi;

public class SqsBookingEventPublisher(IAmazonSQS sqs, IConfiguration configuration, ILogger<SqsBookingEventPublisher> logger) : IBookingEventPublisher
{
    public async Task PublishBookingCreatedAsync(Booking booking)
    {
        try
        {
            await sqs.SendMessageAsync(configuration["Messaging:Sqs:QueueUrl"], IBookingEventPublisher.ToJson(booking));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish booking-created event for booking {BookingId}", booking.Id);
        }
    }
}
