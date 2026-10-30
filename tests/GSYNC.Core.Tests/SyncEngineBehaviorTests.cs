using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Services.Sync;
using Xunit;

namespace GSYNC.Core.Tests;

public sealed class SyncEngineBehaviorTests
{
    [Theory]
    [InlineData("ABC", 10L, SyncChangeKind.Unchanged)]
    [InlineData("XYZ", 10L, SyncChangeKind.Conflict)]
    [InlineData(null, 10L, SyncChangeKind.Conflict)]
    [InlineData(null, 20L, SyncChangeKind.Conflict)]
    public void CompareRequiresVerifiedMatchingHashes(string? remoteHash, long size, SyncChangeKind expected)
    {
        var local = new ResolvedLocalFile(new ContentItem { ContentId = "save", Category = ContentCategory.Save, PathTemplates = new[] { "%HOME%/save" } },
            "C:/save.sav", "save.sav", DateTime.UtcNow);
        var remote = new RemoteFileEntry("save.sav", "games/game-a/save.sav", remoteHash, DateTimeOffset.UtcNow, size);
        Assert.Equal(expected, SyncFileService.DetermineChangeKind(local, remote, "ABC", 10));
    }
}
