using GSYNC.Data.Services;
using Xunit;

namespace GSYNC.Core.Tests;

public sealed class AppPathServiceTests
{
    [Fact]
    public void GetDatabasePath_UsesAppDataGsyncDataDb()
    {
        var service = new AppPathService();

        var path = service.GetDatabasePath();

        Assert.EndsWith(Path.Combine("GSYNC", "data.db"), path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExplicitDataRoot_IsUsedForAllAppState_AndMustBeAbsolute()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsync-isolated", Guid.NewGuid().ToString("N"));
        var service = new AppPathService(root);
        Assert.Equal(root, service.GetAppDataRoot());
        Assert.Equal(Path.Combine(root, "data.db"), service.GetDatabasePath());
        Assert.Equal(Path.Combine(root, "definitions"), service.GetDefinitionsDirectory());
        Assert.Equal(Path.Combine(root, "snapshots"), service.GetSnapshotsDirectory());
        Assert.Throws<ArgumentException>(() => new AppPathService("relative-data"));
    }
}
