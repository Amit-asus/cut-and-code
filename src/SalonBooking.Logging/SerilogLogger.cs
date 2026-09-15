using Serilog;

namespace SalonBooking.Logging;

public class SerilogLogger : AppLogger
{
    public override void LogInformation(string message) => Log.Information(message);

    public override void LogWarning(string message) => Log.Warning(message);

    public override void LogError(string message, Exception? ex = null)
    {
        if (ex is not null)
        {
            Log.Error(ex, message);
        }
        else
        {
            Log.Error(message);
        }
    }
}
