using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace SalonBooking.NotificationApi;

// Long-running background worker: listens on the booking-created queue for the life of
// the app, independent of any HTTP request. IServiceScopeFactory is used (rather than
// injecting BookingEnrichmentService/EmailSender directly) because this class is a
// singleton but HttpClient-backed services are typically scoped — each message gets its
// own scope, same lifetime rules as a normal per-request DI scope.
public class BookingNotificationConsumer(
    ServiceBusClient client,
    IServiceScopeFactory scopeFactory,
    ILogger<BookingNotificationConsumer> logger) : BackgroundService
{
    private const string QueueName = "booking-created";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var processor = client.CreateProcessor(QueueName, new ServiceBusProcessorOptions());

        processor.ProcessMessageAsync += async args =>
        {
            var message = args.Message.Body.ToObjectFromJson<BookingCreatedMessage>();
            if (message is null)
            {
                logger.LogWarning("Received an unparseable booking-created message, skipping.");
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                return;
            }

            logger.LogInformation("Received booking-created event for booking {BookingId}", message.BookingId);

            using var scope = scopeFactory.CreateScope();
            var enrichment = scope.ServiceProvider.GetRequiredService<BookingEnrichmentService>();
            var emailSender = scope.ServiceProvider.GetRequiredService<EmailSender>();

            var enriched = await enrichment.EnrichAsync(message);
            if (enriched is not null)
            {
                await emailSender.SendBookingConfirmationAsync(enriched, message);
            }

            // Only remove the message from the queue once we've fully processed it — if
            // something above throws, ProcessErrorAsync below logs it and the message stays
            // in the queue to be retried instead of silently vanishing.
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        };

        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Error processing booking-created message");
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);

        // Keep the background service alive for as long as the app runs; StopAsync (called
        // automatically on shutdown) stops the processor via the token below.
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }

        await processor.StopProcessingAsync();
    }
}
