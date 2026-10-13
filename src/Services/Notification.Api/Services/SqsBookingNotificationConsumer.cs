using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace SalonBooking.NotificationApi;

public class SqsBookingNotificationConsumer(
    IAmazonSQS sqs,
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<SqsBookingNotificationConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueUrl = configuration["Messaging:Sqs:QueueUrl"]!;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20
                }, stoppingToken);

                foreach (var sqsMessage in response.Messages ?? [])
                {
                    await ProcessAsync(queueUrl, sqsMessage, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error receiving booking-created messages");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
            }
        }
    }

    private async Task ProcessAsync(string queueUrl, Message sqsMessage, CancellationToken stoppingToken)
    {
        BookingCreatedMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<BookingCreatedMessage>(sqsMessage.Body);
        }
        catch (JsonException)
        {
            message = null;
        }

        if (message is null)
        {
            logger.LogWarning("Received an unparseable booking-created message, skipping.");
            await sqs.DeleteMessageAsync(queueUrl, sqsMessage.ReceiptHandle, stoppingToken);
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<BookingNotificationHandler>().HandleAsync(message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing booking-created message");
            return;
        }

        await sqs.DeleteMessageAsync(queueUrl, sqsMessage.ReceiptHandle, stoppingToken);
    }
}
