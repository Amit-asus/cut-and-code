using Amazon.SQS;

namespace SalonBooking.BookingApi;

public static class MessagingExtensions
{
    public static void AddBookingEventPublisher(this WebApplicationBuilder builder)
    {
        if (builder.Configuration["Messaging:Provider"] == "Sqs")
        {
            if (string.IsNullOrWhiteSpace(builder.Configuration["Messaging:Sqs:QueueUrl"]))
            {
                throw new InvalidOperationException("Messaging:Sqs:QueueUrl is not configured.");
            }

            builder.Services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient());
            builder.Services.AddSingleton<IBookingEventPublisher, SqsBookingEventPublisher>();
        }
        else
        {
            builder.AddAzureServiceBusClient("servicebus");
            builder.Services.AddSingleton<IBookingEventPublisher, BookingEventPublisher>();
        }
    }
}
