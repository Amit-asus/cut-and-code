using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace SalonBooking.NotificationApi;

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

            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<BookingNotificationHandler>().HandleAsync(message);

            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        };

        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Error processing booking-created message");
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            //Todo
        }

        await processor.StopProcessingAsync();
    }
}
