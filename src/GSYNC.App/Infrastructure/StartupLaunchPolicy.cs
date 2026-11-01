namespace GSYNC.App.Infrastructure;

public static class StartupLaunchPolicy
{
    public const string StartupArgument = "--windows-startup";

    public static bool IsStartup(IEnumerable<string> arguments) =>
        arguments.Any(value => string.Equals(value, StartupArgument, StringComparison.OrdinalIgnoreCase));

    public static bool StartHidden(IEnumerable<string> arguments, bool startupInBackground, bool restartHidden) =>
        restartHidden || (startupInBackground && IsStartup(arguments));
}
