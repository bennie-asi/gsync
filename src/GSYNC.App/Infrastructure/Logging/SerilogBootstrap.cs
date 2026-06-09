using Serilog;

namespace GSYNC.App.Infrastructure.Logging;

public static class SerilogBootstrap
{
    public static ILogger CreateLogger(string logsDirectory)
    {
        Directory.CreateDirectory(logsDirectory);

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logsDirectory, "gsync-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();
    }
}
