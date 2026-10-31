using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace GSYNC.App.Infrastructure.Wizard;

/// <summary>Extracts a program's Windows icon without running the program.</summary>
public static class ExecutableIconReader
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<byte[]?>>> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Task<byte[]?> ReadAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !OperatingSystem.IsWindowsVersionAtLeast(6, 1)) return Task.FromResult<byte[]?>(null);
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || (!file.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
                                 !file.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)))
                return Task.FromResult<byte[]?>(null);
            // File metadata makes a replaced executable invalidate the cached icon.
            var key = $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}";
            if (Cache.Count > 256) Cache.Clear();
            return Cache.GetOrAdd(key, _ => new Lazy<Task<byte[]?>>(() => Task.Run(() => Read(file.FullName)))).Value;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Task.FromResult<byte[]?>(null);
        }
    }

    private static byte[]? Read(string path)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1)) return null;
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon is null) return null;
            using var bitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or ExternalException or InvalidOperationException)
        {
            return null;
        }
    }
}
