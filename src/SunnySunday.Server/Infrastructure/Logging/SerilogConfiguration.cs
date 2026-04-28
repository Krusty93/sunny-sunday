using Serilog;

namespace SunnySunday.Server.Infrastructure.Logging;

internal static class SerilogConfiguration
{
    private const string LogDirectory = ".data/logs";
    private const string LogFilePath = ".data/logs/sunny-.log";

    internal static void ConfigureLogging(WebApplicationBuilder builder)
    {
        Directory.CreateDirectory(LogDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                LogFilePath,
                rollingInterval: RollingInterval.Day,
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
            .CreateLogger();

        builder.Host.UseSerilog();
    }
}
