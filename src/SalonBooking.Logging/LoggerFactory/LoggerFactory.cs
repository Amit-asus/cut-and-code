using SalonBooking.Logging;

namespace SalonBooking.Logging.LoggerFactory;

public class LoggerFactory : ILoggerFactory
{
    public IAppLogger CreateLogger() => new SerilogLogger();
}
