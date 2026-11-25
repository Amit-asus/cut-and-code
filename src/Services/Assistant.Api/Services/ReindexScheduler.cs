using Cronos;

namespace SalonBooking.AssistantApi;

public class ReindexScheduler(KnowledgeIndexer indexer, IConfiguration configuration, ILogger<ReindexScheduler> logger)
    : BackgroundService
{
    private static readonly TimeSpan StartupRetryDelay = TimeSpan.FromMinutes(1);
    private const int StartupAttempts = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        try
        {
            for (var attempt = 1; attempt <= StartupAttempts; attempt++)
            {
                var result = await indexer.ReindexAsync("startup", stoppingToken);
                if (result.Ran || result.Message?.Contains("not configured") == true)
                {
                    break;
                }

                logger.LogWarning("Startup reindex attempt {Attempt}/{Max} did not complete; retrying in {Delay}.",
                    attempt, StartupAttempts, StartupRetryDelay);
                await Task.Delay(StartupRetryDelay, stoppingToken);
            }

            var cronText = configuration["Assistant:ReindexCron"] ?? "0 * * * *";
            var cron = CronExpression.Parse(cronText);
            logger.LogInformation("Knowledge reindex scheduled with cron '{Cron}' (UTC).", cronText);

            while (!stoppingToken.IsCancellationRequested)
            {
                var next = cron.GetNextOccurrence(DateTime.UtcNow);
                if (next is null)
                {
                    return;
                }

                var delay = next.Value - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, stoppingToken);
                }

                await indexer.ReindexAsync("cron", stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
