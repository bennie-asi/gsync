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
}
