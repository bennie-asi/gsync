using GSYNC.Core.Utilities;
using Xunit;

namespace GSYNC.Core.Tests;

public sealed class SafePathTests
{
    [Fact]
    public async Task PhysicalPath_ResolvesLinksInsideAnotherLinksTarget_IncludingMissingDescendants()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsync-links-tests", Guid.NewGuid().ToString("N"));
        var actual = Path.Combine(root, "actual");
        Directory.CreateDirectory(Path.Combine(actual, "inner"));
        var first = Path.Combine(root, "first");
        var second = Path.Combine(root, "second");
        try
        {
            await CreateAliasAsync(first, actual);
            await CreateAliasAsync(second, Path.Combine(first, "inner"));
            Assert.Equal(Path.Combine(actual, "inner/missing/slot.sav").Replace('/', Path.DirectorySeparatorChar),
                SafePath.GetPhysicalPath(Path.Combine(second, "missing/slot.sav")));
        }
        finally
        {
            if (Directory.Exists(second)) Directory.Delete(second);
            if (Directory.Exists(first)) Directory.Delete(first);
            Directory.Delete(root, true);
        }
    }

    private static async Task CreateAliasAsync(string link, string target)
    {
        if (!OperatingSystem.IsWindows()) { Directory.CreateSymbolicLink(link, target); return; }
        var info = new System.Diagnostics.ProcessStartInfo("cmd.exe") { CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(info)!;
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
    }
}
