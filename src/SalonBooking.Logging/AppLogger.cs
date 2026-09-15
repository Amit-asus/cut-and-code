namespace SalonBooking.Logging;

public abstract class AppLogger : IAppLogger
{
    public abstract void LogInformation(string message);
    public abstract void LogWarning(string message);
    public abstract void LogError(string message, Exception? ex = null);
}
