using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace GSYNC.App.Infrastructure.Wizard;

public sealed record GameProgramSelection(string DisplayName, string SourcePath, string? ExecutablePath, string? InstallDirectory, string? SteamId);

/// <summary>Reads a program or shortcut without launching it.</summary>
public static class GameProgramSelectionReader
{
    public const string ProgramPathVariable = "GSYNC_PROGRAM_PATH";
    public const string ArtworkPathVariable = "GSYNC_ARTWORK_PATH";

    public static GameProgramSelection Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("The selected program or shortcut does not exist.", fullPath);
        var extension = Path.GetExtension(fullPath);
        string? executable = null;
        string? steamId = null;
        if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            executable = fullPath;
        else if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            executable = ReadLinkTarget(fullPath);
        else if (extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
            steamId = ReadSteamShortcut(fullPath);
        else
            throw new InvalidDataException("Select an .exe, .lnk, or Steam .url shortcut.");

        var name = Path.GetFileNameWithoutExtension(fullPath);
        var directory = executable is not null && Path.IsPathFullyQualified(executable)
            ? Path.GetDirectoryName(executable) : null;
        return new GameProgramSelection(name, fullPath, executable, directory, steamId);
    }

    private static string? ReadLinkTarget(string path)
    {
        if (!OperatingSystem.IsWindows()) return null;
        object? shellLink = null;
        try
        {
            shellLink = new ShellLink();
            ((IPersistFile)shellLink).Load(path, 0);
            var target = new StringBuilder(32768);
            ((IShellLinkW)shellLink).GetPath(target, target.Capacity, IntPtr.Zero, 0);
            var value = Environment.ExpandEnvironmentVariables(target.ToString());
            return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && Path.IsPathFullyQualified(value) ? value : null;
        }
        catch (COMException) { return null; }
        finally
        {
            if (shellLink is not null) Marshal.FinalReleaseComObject(shellLink);
        }
    }

    private static string? ReadSteamShortcut(string path)
    {
        foreach (var line in File.ReadLines(path).Take(100))
        {
            if (!line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase) ||
                !Uri.TryCreate(line[4..].Trim(), UriKind.Absolute, out var uri) ||
                !uri.Scheme.Equals("steam", StringComparison.OrdinalIgnoreCase) ||
                !(uri.Host.Equals("rungameid", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("run", StringComparison.OrdinalIgnoreCase)))
                continue;
            var id = uri.AbsolutePath.Trim('/').Split('/')[0];
            return id.Length > 0 && id.All(char.IsAsciiDigit) ? id : null;
        }
        return null;
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, IntPtr findData, uint flags);
    }
}
