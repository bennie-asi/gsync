using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace GSYNC.App.Infrastructure.Logging;

public static class SerilogBootstrap
{
    private static readonly LoggingLevelSwitch LevelSwitch = new(LogEventLevel.Information);

    public static ILogger CreateLogger(string logsDirectory, string minimumLevel)
    {
        Directory.CreateDirectory(logsDirectory);
        LevelSwitch.MinimumLevel = ParseLevel(minimumLevel);

        return new LoggerConfiguration()
            .MinimumLevel.ControlledBy(LevelSwitch)
            .WriteTo.File(
                Path.Combine(logsDirectory, "gsync-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();
    }

    public static void SetMinimumLevel(string minimumLevel)
    {
        LevelSwitch.MinimumLevel = ParseLevel(minimumLevel);
    }

    private static LogEventLevel ParseLevel(string? minimumLevel)
    {
        return Enum.TryParse<LogEventLevel>(minimumLevel, ignoreCase: true, out var level)
            ? level
            : LogEventLevel.Information;
    }
}
