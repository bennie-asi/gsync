using System.Reflection;
using GSYNC.Core.Models;
using GSYNC.Core.Services.Sync;
using Xunit;

namespace GSYNC.Core.Tests;

public sealed class SyncEngineBehaviorTests
{
    [Fact]
    public void DetermineChangeKind_ReturnsUnchanged_WhenHashesMatch()
    {
        var method = typeof(SyncEngine).GetMethod("DetermineChangeKind", BindingFlags.NonPublic | BindingFlags.Static)!;
        var localFileType = typeof(SyncEngine).GetNestedType("ResolvedLocalFile", BindingFlags.NonPublic)!;
        var remoteFileType = typeof(SyncEngine).GetNestedType("RemoteFileEntry", BindingFlags.NonPublic)!;

        var local = Activator.CreateInstance(localFileType, new object?[]
        {
            null,
            "C:/save.sav",
            "save.sav",
            DateTime.UtcNow,
        });

        var remote = Activator.CreateInstance(remoteFileType, new object?[]
        {
            "save.sav",
            "games/game-a/save.sav",
            "ABC",
            DateTimeOffset.UtcNow,
            10L,
        });

        var result = (SyncChangeKind)method.Invoke(null, new[] { local, remote, "ABC", (object)10L })!;

        Assert.Equal(SyncChangeKind.Unchanged, result);
    }

    [Fact]
    public void DetermineChangeKind_ReturnsConflict_WhenBothExistAndHashesDiffer()
    {
        var method = typeof(SyncEngine).GetMethod("DetermineChangeKind", BindingFlags.NonPublic | BindingFlags.Static)!;
        var localFileType = typeof(SyncEngine).GetNestedType("ResolvedLocalFile", BindingFlags.NonPublic)!;
        var remoteFileType = typeof(SyncEngine).GetNestedType("RemoteFileEntry", BindingFlags.NonPublic)!;

        var local = Activator.CreateInstance(localFileType, new object?[]
        {
            null,
            "C:/save.sav",
            "save.sav",
            DateTime.UtcNow,
        });

        var remote = Activator.CreateInstance(remoteFileType, new object?[]
        {
            "save.sav",
            "games/game-a/save.sav",
            "XYZ",
            DateTimeOffset.UtcNow,
            10L,
        });

        var result = (SyncChangeKind)method.Invoke(null, new[] { local, remote, "ABC", (object)10L })!;

        Assert.Equal(SyncChangeKind.Conflict, result);
    }

    [Fact]
    public void DetermineChangeKind_ReturnsUnchanged_WhenNoRemoteHashAndSizesMatch()
    {
        var method = typeof(SyncEngine).GetMethod("DetermineChangeKind", BindingFlags.NonPublic | BindingFlags.Static)!;
        var localFileType = typeof(SyncEngine).GetNestedType("ResolvedLocalFile", BindingFlags.NonPublic)!;
        var remoteFileType = typeof(SyncEngine).GetNestedType("RemoteFileEntry", BindingFlags.NonPublic)!;

        var local = Activator.CreateInstance(localFileType, new object?[]
        {
            null,
            "C:/save.sav",
            "save.sav",
            DateTime.UtcNow.AddHours(-2),
        });

        // No hash, and the remote "last modified" is well after the local mtime (it reflects the
        // upload time). Equal size must still read as in-sync rather than a conflict.
        var remote = Activator.CreateInstance(remoteFileType, new object?[]
        {
            "save.sav",
            "games/game-a/save.sav",
            null,
            DateTimeOffset.UtcNow,
            10L,
        });

        var result = (SyncChangeKind)method.Invoke(null, new[] { local, remote, "ABC", (object)10L })!;

        Assert.Equal(SyncChangeKind.Unchanged, result);
    }

    [Fact]
    public void DetermineChangeKind_ReturnsConflict_WhenNoRemoteHashAndSizesDiffer()
    {
        var method = typeof(SyncEngine).GetMethod("DetermineChangeKind", BindingFlags.NonPublic | BindingFlags.Static)!;
        var localFileType = typeof(SyncEngine).GetNestedType("ResolvedLocalFile", BindingFlags.NonPublic)!;
        var remoteFileType = typeof(SyncEngine).GetNestedType("RemoteFileEntry", BindingFlags.NonPublic)!;

        var local = Activator.CreateInstance(localFileType, new object?[]
        {
            null,
            "C:/save.sav",
            "save.sav",
            DateTime.UtcNow,
        });

        var remote = Activator.CreateInstance(remoteFileType, new object?[]
        {
            "save.sav",
            "games/game-a/save.sav",
            null,
            DateTimeOffset.UtcNow,
            20L,
        });

        var result = (SyncChangeKind)method.Invoke(null, new[] { local, remote, "ABC", (object)10L })!;

        Assert.Equal(SyncChangeKind.Conflict, result);
    }
}
