using Amazon.SQS;

namespace SalonBooking.NotificationApi;

public static class MessagingExtensions
{
    public static void AddBookingNotificationConsumer(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<BookingNotificationHandler>();

        if (builder.Configuration["Messaging:Provider"] == "Sqs")
        {
            if (string.IsNullOrWhiteSpace(builder.Configuration["Messaging:Sqs:QueueUrl"]))
            {
                throw new InvalidOperationException("Messaging:Sqs:QueueUrl is not configured.");
            }

            builder.Services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient());
            builder.Services.AddHostedService<SqsBookingNotificationConsumer>();
        }
        else
        {
            builder.AddAzureServiceBusClient("servicebus");
            builder.Services.AddHostedService<BookingNotificationConsumer>();
        }
    }
}
