using System.Reflection;
using Microsoft.Win32;

namespace GSYNC.App.Infrastructure;

internal static class DesktopFeatures
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string EntryName = "GSYNC";
    internal const string ReleasesUrl = "https://github.com/bennie-asi/gsync/releases";
    internal static string CurrentVersion => typeof(DesktopFeatures).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    internal static string? StartupCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(EntryName) as string;
    }

    internal static string ExpectedStartupCommand()
    {
        var path = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine the application path.");
        if (string.Equals(Path.GetFileNameWithoutExtension(path), "dotnet", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Startup cannot be registered when running through dotnet.");
        return $"\"{path}\" {StartupLaunchPolicy.StartupArgument}";
    }

    internal static bool IsCurrentStartupCommand(string command) =>
        string.Equals(command, ExpectedStartupCommand(), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(command, LegacyStartupCommand(), StringComparison.OrdinalIgnoreCase);

    internal static string LegacyStartupCommand() => ExpectedStartupCommand()[..^(StartupLaunchPolicy.StartupArgument.Length + 1)];

    internal static void UpgradeLegacyStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(EntryName) is string current && string.Equals(current, LegacyStartupCommand(), StringComparison.OrdinalIgnoreCase))
            key.SetValue(EntryName, ExpectedStartupCommand(), RegistryValueKind.String);
    }

    internal static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true) ?? throw new InvalidOperationException("Cannot open startup settings.");
        if (enabled) key.SetValue(EntryName, ExpectedStartupCommand(), RegistryValueKind.String);
        else key.DeleteValue(EntryName, false);
    }

}
