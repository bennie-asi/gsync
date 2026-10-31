using System.IO.Compression;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Services.Sync;
using GSYNC.Core.Services;
using GSYNC.Core.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GSYNC.Core.Tests;

public sealed class SyncEngineExecutionTests
{
    [Fact]
    public async Task UploadJob_UploadsFiles_AndWritesSyncRecord()
    {
        var fixture = new SyncEngineFixture();
        var localFile = Path.Combine(fixture.RootPath, "game-a", "save1.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(localFile)!);
        await File.WriteAllTextAsync(localFile, "save-data");

        var job = new SyncJob
        {
            GameInstanceId = fixture.GameInstance.Id,
            Direction = SyncDirection.Upload,
        };

        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, job, cts);

        Assert.Contains(fixture.StorageProvider.UploadedPaths, path => path.EndsWith("default/save1.sav", StringComparison.OrdinalIgnoreCase));
        Assert.Single(fixture.SyncHistoryRepository.Records);
        Assert.Equal(SyncDirection.Upload, fixture.SyncHistoryRepository.Records[0].Direction);
        Assert.Equal(SyncJobStatus.Completed, fixture.SyncHistoryRepository.Records[0].Status);
    }

    [Fact]
    public async Task DownloadJob_CreatesSnapshot_DownloadsFiles_AndWritesSyncRecord()
    {
        var fixture = new SyncEngineFixture();
        var existingLocalFile = Path.Combine(fixture.RootPath, "game-a", "save1.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(existingLocalFile)!);
        await File.WriteAllTextAsync(existingLocalFile, "old-save");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/save1.sav"] = "new-save"u8.ToArray();

        var job = new SyncJob
        {
            GameInstanceId = fixture.GameInstance.Id,
            Direction = SyncDirection.Download,
        };

        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, job, cts);

        var downloaded = await File.ReadAllTextAsync(existingLocalFile);
        Assert.Equal("new-save", downloaded);
        Assert.Single(fixture.SyncHistoryRepository.Snapshots);
        Assert.Single(fixture.SyncHistoryRepository.Records);
        Assert.Equal(SyncDirection.Download, fixture.SyncHistoryRepository.Records[0].Direction);
        Assert.Equal(SyncJobStatus.Completed, fixture.SyncHistoryRepository.Records[0].Status);
    }

    [Fact]
    public async Task DownloadJob_WithAutoSnapshotDisabled_DoesNotCreateSnapshot_ButStillWritesRecord()
    {
        var fixture = new SyncEngineFixture(autoSnapshotBeforeDownloadEnabled: false);
        var existingLocalFile = Path.Combine(fixture.RootPath, "game-a", "save1.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(existingLocalFile)!);
        await File.WriteAllTextAsync(existingLocalFile, "old-save");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/save1.sav"] = "new-save"u8.ToArray();

        var job = new SyncJob
        {
            GameInstanceId = fixture.GameInstance.Id,
            Direction = SyncDirection.Download,
        };

        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, job, cts);

        var downloaded = await File.ReadAllTextAsync(existingLocalFile);
        Assert.Equal("new-save", downloaded);
        Assert.Empty(fixture.SyncHistoryRepository.Snapshots);
        Assert.Single(fixture.SyncHistoryRepository.Records);
        Assert.Null(fixture.SyncHistoryRepository.Records[0].SnapshotId);
    }

    [Fact]
    public async Task ResolveConflictJob_AppliesOnlyDecidedFiles_AndWritesSyncRecord()
    {
        var fixture = new SyncEngineFixture();
        var localFile = Path.Combine(fixture.RootPath, "game-a", "save1.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(localFile)!);
        await File.WriteAllTextAsync(localFile, "local-save");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/save1.sav"] = "remote-save"u8.ToArray();
        fixture.StorageProvider.RemoteFiles["games/game-a/default/save2.sav"] = "remote-save-2"u8.ToArray();

        var job = new SyncJob
        {
            GameInstanceId = fixture.GameInstance.Id,
            Direction = SyncDirection.ResolveConflict,
            ConflictResolutionPlan = new ConflictResolutionPlan
            {
                GameInstanceId = fixture.GameInstance.Id,
                Decisions =
                [
                    new ConflictResolutionDecision { RelativePath = "games/game-a/default/save1.sav", Action = ConflictResolutionAction.KeepRemote },
                    new ConflictResolutionDecision { RelativePath = "games/game-a/default/save2.sav", Action = ConflictResolutionAction.Undecided },
                ],
            },
        };

        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, job, cts);

        Assert.Equal("remote-save", await File.ReadAllTextAsync(localFile));
        Assert.Single(fixture.SyncHistoryRepository.Snapshots);
        Assert.Single(fixture.SyncHistoryRepository.Records);
        Assert.Equal(SyncDirection.ResolveConflict, fixture.SyncHistoryRepository.Records[0].Direction);
        Assert.Equal(SyncJobStatus.Completed, fixture.SyncHistoryRepository.Records[0].Status);
        Assert.Equal(1, fixture.SyncHistoryRepository.Records[0].ProcessedFiles);
    }

    [Fact]
    public async Task CancelActiveJob_StopsDownload_AndWritesCancelledRecord()
    {
        var fixture = new SyncEngineFixture(delayDownloads: true);
        var existingLocalFile = Path.Combine(fixture.RootPath, "game-a", "save1.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(existingLocalFile)!);
        await File.WriteAllTextAsync(existingLocalFile, "old-save");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/save1.sav"] = "new-save"u8.ToArray();
        fixture.StorageProvider.RemoteFiles["games/game-a/default/save2.sav"] = "new-save-2"u8.ToArray();

        var job = new SyncJob
        {
            GameInstanceId = fixture.GameInstance.Id,
            Direction = SyncDirection.Download,
        };

        using var cts = new CancellationTokenSource();
        var worker = fixture.RunQueueAsync(cts.Token);
        await fixture.Queue.QueueAsync(job, CancellationToken.None);

        await fixture.WaitForActiveJobAsync();
        Assert.True(fixture.Queue.CancelActive(job.Id));

        await fixture.WaitForRecordsAsync(1);
        var record = Assert.Single(fixture.SyncHistoryRepository.Records);
        Assert.Equal(SyncDirection.Download, record.Direction);
        Assert.Equal(SyncJobStatus.Cancelled, record.Status);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await worker);
    }

    [Fact]
    public async Task RemovedJob_IsSkipped_AndWritesNoRecord()
    {
        var fixture = new SyncEngineFixture();
        var localFile = Path.Combine(fixture.RootPath, "game-a", "save1.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(localFile)!);
        await File.WriteAllTextAsync(localFile, "save-data");

        var removed = new SyncJob
        {
            GameInstanceId = fixture.GameInstance.Id,
            Direction = SyncDirection.Upload,
        };
        var kept = new SyncJob
        {
            GameInstanceId = fixture.GameInstance.Id,
            Direction = SyncDirection.Compare,
        };

        // Remove the first job before the worker starts so it can never run.
        await fixture.Queue.QueueAsync(removed, CancellationToken.None);
        await fixture.Queue.QueueAsync(kept, CancellationToken.None);
        Assert.True(fixture.Queue.Remove(removed.Id));

        using var cts = new CancellationTokenSource();
        var worker = fixture.RunQueueAsync(cts.Token);
        await fixture.WaitForRecordsAsync(1);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await worker);

        var record = Assert.Single(fixture.SyncHistoryRepository.Records);
        Assert.Equal(SyncDirection.Compare, record.Direction);
    }

    [Theory]
    [InlineData("AAAA", SyncChangeKind.Conflict)]
    [InlineData("BBBB", SyncChangeKind.Unchanged)]
    public async Task Compare_WithoutRemoteHash_VerifiesContentEvenWhenSizesMatch(string remoteContent, SyncChangeKind expected)
    {
        var fixture = new SyncEngineFixture();
        var path = Path.Combine(fixture.RootPath, "game-a", "slot.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "BBBB");
        fixture.StorageProvider.OmitHashes = true;
        fixture.StorageProvider.RemoteFiles["games/game-a/default/slot.sav"] = System.Text.Encoding.UTF8.GetBytes(remoteContent);

        var comparison = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Compare }, CancellationToken.None);

        var entry = Assert.Single(comparison.Entries);
        Assert.Equal(expected, entry.ChangeKind);
        Assert.NotNull(entry.RemoteHash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MultipleRoots_RoundTripResolveAndRestore_KeepFilesSeparateWhenTemplateOrderChanges(bool singleFileRoots)
    {
        var templates = new List<string>
        {
            "%GAME_INSTALL_DIR%/one" + (singleFileRoots ? "/slot.sav" : "/"),
            "%GAME_INSTALL_DIR%/two" + (singleFileRoots ? "/slot.sav" : "/"),
        };
        var fixture = new SyncEngineFixture(pathTemplates: templates);
        var first = Path.Combine(fixture.RootPath, "game-a", "one", "slot.sav");
        var second = Path.Combine(fixture.RootPath, "game-a", "two", "slot.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        Directory.CreateDirectory(Path.GetDirectoryName(second)!);
        await File.WriteAllTextAsync(first, "first-save");
        await File.WriteAllTextAsync(second, "second-save");
        using (var cts = new CancellationTokenSource())
        {
            await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Upload }, cts);
        }

        Assert.Equal(2, fixture.StorageProvider.RemoteFiles.Count);
        var comparison = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Compare }, CancellationToken.None);
        Assert.Equal(2, comparison.Entries.Count);
        Assert.All(comparison.Entries, entry => Assert.Equal(SyncChangeKind.Unchanged, entry.ChangeKind));

        await File.WriteAllTextAsync(first, "before-download-first");
        await File.WriteAllTextAsync(second, "before-download-second");
        templates.Reverse();
        using (var cts = new CancellationTokenSource())
        {
            await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Download }, cts);
        }

        Assert.Equal("first-save", await File.ReadAllTextAsync(first));
        Assert.Equal("second-save", await File.ReadAllTextAsync(second));
        await fixture.Engine.RestoreSnapshotAsync(Assert.Single(fixture.SyncHistoryRepository.Snapshots).Id, CancellationToken.None);
        Assert.Equal("before-download-first", await File.ReadAllTextAsync(first));
        Assert.Equal("before-download-second", await File.ReadAllTextAsync(second));

        var firstRemotePath = fixture.StorageProvider.RemoteFiles.Single(pair => System.Text.Encoding.UTF8.GetString(pair.Value) == "first-save").Key;
        await fixture.Engine.ResolveConflictsAsync(new ConflictResolutionPlan
        {
            GameInstanceId = fixture.GameInstance.Id,
            Decisions = new[] { new ConflictResolutionDecision { RelativePath = firstRemotePath, Action = ConflictResolutionAction.KeepRemote } },
        }, CancellationToken.None);
        Assert.Equal("first-save", await File.ReadAllTextAsync(first));
        Assert.Equal("before-download-second", await File.ReadAllTextAsync(second));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedDownload_RegistersSnapshotBeforeOverwrite_AndCanRestore(bool cancel)
    {
        var fixture = new SyncEngineFixture();
        var first = Path.Combine(fixture.RootPath, "game-a", "one.sav");
        var second = Path.Combine(fixture.RootPath, "game-a", "two.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        await File.WriteAllTextAsync(first, "old-one");
        await File.WriteAllTextAsync(second, "old-two");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/one.sav"] = "new-one"u8.ToArray();
        fixture.StorageProvider.RemoteFiles["games/game-a/default/two.sav"] = "new-two"u8.ToArray();
        fixture.StorageProvider.FailDownloadNumber = 2;
        fixture.StorageProvider.CancelInsteadOfFail = cancel;
        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Download }, cts);

        var record = Assert.Single(fixture.SyncHistoryRepository.Records);
        Assert.Equal(cancel ? SyncJobStatus.Cancelled : SyncJobStatus.Failed, record.Status);
        Assert.Equal("old-one", await File.ReadAllTextAsync(first));
        Assert.Equal(Assert.Single(fixture.SyncHistoryRepository.Snapshots).Id, record.SnapshotId);
        await fixture.Engine.RestoreSnapshotAsync(record.SnapshotId!.Value, CancellationToken.None);
        Assert.Equal("old-one", await File.ReadAllTextAsync(first));
        Assert.Equal("old-two", await File.ReadAllTextAsync(second));
    }

    [Fact]
    public async Task Download_WhenSnapshotCannotBeRegistered_DoesNotOverwriteFiles()
    {
        var fixture = new SyncEngineFixture();
        var path = Path.Combine(fixture.RootPath, "game-a", "slot.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "old");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/slot.sav"] = "new"u8.ToArray();
        fixture.SyncHistoryRepository.FailSnapshotWrites = true;
        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Download }, cts);

        Assert.Equal("old", await File.ReadAllTextAsync(path));
        Assert.Equal(SyncJobStatus.Failed, Assert.Single(fixture.SyncHistoryRepository.Records).Status);
        Assert.Empty(fixture.SyncHistoryRepository.Snapshots);
    }

    [Fact]
    public async Task MultipleRoots_LegacyMixedRemoteLayout_FailsBeforeWriting()
    {
        var fixture = new SyncEngineFixture(pathTemplates: new[] { "%GAME_INSTALL_DIR%/one/", "%GAME_INSTALL_DIR%/two/" });
        fixture.StorageProvider.RemoteFiles["games/game-a/default/slot.sav"] = "ambiguous"u8.ToArray();
        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Download }, cts);

        var record = Assert.Single(fixture.SyncHistoryRepository.Records);
        Assert.Equal(SyncJobStatus.Failed, record.Status);
        Assert.Contains("old mixed-path layout", record.ErrorMessage);
        Assert.False(Directory.Exists(Path.Combine(fixture.RootPath, "game-a")));
    }

    [Fact]
    public async Task Compare_PassesBindingConfiguration_AndResolvesTargetChangesOnEachJob()
    {
        var settings = new Dictionary<string, string> { ["rootPath"] = "selected-B" };
        var fixture = new SyncEngineFixture(bindingSettings: settings);
        await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Compare }, CancellationToken.None);
        Assert.Equal("selected-B", fixture.StorageProvider.LastConfiguration!["rootPath"]);

        var currentTarget = new Dictionary<string, string> { ["rootPath"] = "updated-B" };
        var dynamicFixture = new SyncEngineFixture(storageConfigurationResolver: _ => currentTarget);
        await dynamicFixture.Engine.CompareAsync(new SyncJob { GameInstanceId = dynamicFixture.GameInstance.Id, Direction = SyncDirection.Compare }, CancellationToken.None);
        Assert.Equal("updated-B", dynamicFixture.StorageProvider.LastConfiguration!["rootPath"]);
        currentTarget["rootPath"] = "moved-B";
        await dynamicFixture.Engine.CompareAsync(new SyncJob { GameInstanceId = dynamicFixture.GameInstance.Id, Direction = SyncDirection.Compare }, CancellationToken.None);
        Assert.Equal("moved-B", dynamicFixture.StorageProvider.LastConfiguration!["rootPath"]);
    }

    [Theory]
    [InlineData("../outside.sav")]
    [InlineData("..\\outside.sav")]
    [InlineData("/outside.sav")]
    [InlineData("C:/outside.sav")]
    public async Task Download_RejectsPathsThatEscapeTheContentRoot(string relativePath)
    {
        var fixture = new SyncEngineFixture();
        fixture.StorageProvider.RemoteFiles["games/game-a/default/" + relativePath] = "unsafe"u8.ToArray();
        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Download }, cts);

        var record = Assert.Single(fixture.SyncHistoryRepository.Records);
        Assert.Equal(SyncJobStatus.Failed, record.Status);
        Assert.Contains("safe relative file path", record.ErrorMessage);
        Assert.False(Directory.Exists(Path.Combine(fixture.RootPath, "game-a")));
    }

    [Theory]
    [InlineData("*.sav", "slot.sav", "slot.sav")]
    [InlineData("**/*.sav", "slot.sav,nested/slot.sav,profiles/p1/slot1.sav,profiles/p2/slot2.sav", "slot.sav,nested/slot.sav,profiles/p1/slot1.sav,profiles/p2/slot2.sav")]
    [InlineData("profiles/p?/slot[0-9].sav", "profiles/p1/slot1.sav,profiles/p2/slot2.sav", "p1/slot1.sav,p2/slot2.sav")]
    [InlineData("profiles/*", "profiles/p1/slot1.sav,profiles/p2/slot2.sav,profiles/p2/config.ini", "p1/slot1.sav,p2/slot2.sav,p2/config.ini")]
    public async Task GlobTemplates_RoundTripMatchingFilesWithoutFlatteningPaths(string pattern, string expectedPaths, string expectedRemotePaths)
    {
        var fixture = new SyncEngineFixture(pathTemplates: new[] { "%GAME_INSTALL_DIR%/" + pattern });
        var files = new[] { "slot.sav", "nested/slot.sav", "profiles/p1/slot1.sav", "profiles/p2/slot2.sav", "profiles/p2/config.ini", "other.txt" };
        foreach (var relative in files)
        {
            var path = Path.Combine(fixture.GameInstance.InstallDirectory!, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, relative);
        }

        using (var cts = new CancellationTokenSource())
            await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Upload }, cts);
        var expected = expectedPaths.Split(',').Order().ToArray();
        var prefix = "games/game-a/default/";
        Assert.Equal(expectedRemotePaths.Split(',').Order(), fixture.StorageProvider.RemoteFiles.Keys.Select(path => path[prefix.Length..]).Order());
        foreach (var relative in expected) File.Delete(Path.Combine(fixture.GameInstance.InstallDirectory!, relative));
        using (var cts = new CancellationTokenSource())
            await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Download }, cts);
        foreach (var relative in expected) Assert.Equal(relative, await File.ReadAllTextAsync(Path.Combine(fixture.GameInstance.InstallDirectory!, relative)));
    }

    [Fact]
    public async Task IncludeExcludeRules_AreAppliedToCompareAndDownload_AndProtectExistingFiles()
    {
        var fixture = new SyncEngineFixture(includePatterns: new[] { "**/*.sav" }, excludePatterns: new[] { "private*" });
        var root = fixture.GameInstance.InstallDirectory!;
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        await File.WriteAllTextAsync(Path.Combine(root, "settings.cfg"), "keep-settings");
        await File.WriteAllTextAsync(Path.Combine(root, "nested/private.sav"), "keep-private");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/slot.sav"] = "new-save"u8.ToArray();
        fixture.StorageProvider.RemoteFiles["games/game-a/default/settings.cfg"] = "wrong-settings"u8.ToArray();
        fixture.StorageProvider.RemoteFiles["games/game-a/default/nested/private.sav"] = "wrong-private"u8.ToArray();
        var comparison = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Compare }, CancellationToken.None);
        Assert.Equal("games/game-a/default/slot.sav", Assert.Single(comparison.Entries).RelativePath);
        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Download }, cts);
        Assert.Equal("new-save", await File.ReadAllTextAsync(Path.Combine(root, "slot.sav")));
        Assert.Equal("keep-settings", await File.ReadAllTextAsync(Path.Combine(root, "settings.cfg")));
        Assert.Equal("keep-private", await File.ReadAllTextAsync(Path.Combine(root, "nested/private.sav")));
    }

    [Fact]
    public async Task DottedDirectory_IsDownloadedAsDirectory()
    {
        var fixture = new SyncEngineFixture(pathTemplates: new[] { "%GAME_INSTALL_DIR%/saves.v2" });
        var path = Path.Combine(fixture.GameInstance.InstallDirectory!, "saves.v2/slot.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "old");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/slot.sav"] = "new"u8.ToArray();
        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Download }, cts);
        Assert.Equal(SyncJobStatus.Completed, Assert.Single(fixture.SyncHistoryRepository.Records).Status);
        Assert.Equal("new", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task CustomVariables_AreResolvedFromCurrentUserSettingsOnEveryJob()
    {
        var variables = new Dictionary<string, string>();
        var fixture = new SyncEngineFixture(pathTemplates: new[] { "%USER_SAVE_ROOT%" }, userVariablesProvider: () => variables);
        var one = Path.Combine(fixture.RootPath, "one");
        var two = Path.Combine(fixture.RootPath, "two");
        Directory.CreateDirectory(one);
        Directory.CreateDirectory(two);
        await File.WriteAllTextAsync(Path.Combine(one, "one.sav"), "one");
        await File.WriteAllTextAsync(Path.Combine(two, "two.sav"), "two");
        variables["USER_SAVE_ROOT"] = one;
        using (var cts = new CancellationTokenSource())
            await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Upload }, cts);
        variables["USER_SAVE_ROOT"] = two;
        using (var cts = new CancellationTokenSource())
            await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Upload }, cts);
        Assert.Equal(2, fixture.StorageProvider.RemoteFiles.Count);
        Assert.All(fixture.SyncHistoryRepository.Records, record => Assert.Equal(SyncJobStatus.Completed, record.Status));
    }

    [Theory]
    [InlineData("%UNKNOWN_SAVE_ROOT%")]
    [InlineData("%GAME_INSTALL_DIR%/*.missing")]
    [InlineData("%GAME_INSTALL_DIR%/missing")]
    public async Task InvalidOrUnmatchedContentPaths_NeverReportSuccessfulBackup(string template)
    {
        var fixture = new SyncEngineFixture(pathTemplates: new[] { template });
        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Upload }, cts);
        Assert.Equal(SyncJobStatus.Failed, Assert.Single(fixture.SyncHistoryRepository.Records).Status);
        Assert.Empty(fixture.StorageProvider.RemoteFiles);
    }

    [Fact]
    public async Task RestoreSnapshot_RestoresAbsentPaths_AndCreatesAnUndoSnapshot()
    {
        var fixture = new SyncEngineFixture();
        var first = Path.Combine(fixture.GameInstance.InstallDirectory!, "one.sav");
        var added = Path.Combine(fixture.GameInstance.InstallDirectory!, "new.sav");
        Directory.CreateDirectory(fixture.GameInstance.InstallDirectory!);
        await File.WriteAllTextAsync(first, "original");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/one.sav"] = "downloaded"u8.ToArray();
        fixture.StorageProvider.RemoteFiles["games/game-a/default/new.sav"] = "added"u8.ToArray();
        using (var cts = new CancellationTokenSource())
            await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Download }, cts);
        var original = Assert.Single(fixture.SyncHistoryRepository.Snapshots);
        await fixture.Engine.RestoreSnapshotAsync(original.Id, CancellationToken.None);
        Assert.Equal("original", await File.ReadAllTextAsync(first));
        Assert.False(File.Exists(added));
        var undo = fixture.SyncHistoryRepository.Snapshots.Last();
        Assert.NotEqual(original.Id, undo.Id);
        await fixture.Engine.RestoreSnapshotAsync(undo.Id, CancellationToken.None);
        Assert.Equal("downloaded", await File.ReadAllTextAsync(first));
        Assert.Equal("added", await File.ReadAllTextAsync(added));
    }

    [Fact]
    public async Task CorruptSnapshot_FailsBeforeAnySaveIsReplaced()
    {
        var fixture = new SyncEngineFixture();
        var path = Path.Combine(fixture.GameInstance.InstallDirectory!, "one.sav");
        Directory.CreateDirectory(fixture.GameInstance.InstallDirectory!);
        await File.WriteAllTextAsync(path, "original");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/one.sav"] = "downloaded"u8.ToArray();
        using (var cts = new CancellationTokenSource())
            await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Download }, cts);
        var snapshot = Assert.Single(fixture.SyncHistoryRepository.Snapshots);
        using (var archive = ZipFile.Open(snapshot.ArchivePath, ZipArchiveMode.Update))
        {
            archive.GetEntry("default/one.sav")!.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("default/one.sav").Open());
            writer.Write("corrupt backup");
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Engine.RestoreSnapshotAsync(snapshot.Id, CancellationToken.None));
        Assert.Equal("downloaded", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ConflictDownload_InterruptedStream_DoesNotTruncateCurrentSave()
    {
        var fixture = new SyncEngineFixture();
        var path = Path.Combine(fixture.GameInstance.InstallDirectory!, "one.sav");
        Directory.CreateDirectory(fixture.GameInstance.InstallDirectory!);
        await File.WriteAllTextAsync(path, "original");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/one.sav"] = "replacement"u8.ToArray();
        fixture.StorageProvider.DownloadContentFactory = _ => new FailingReadStream("replacement"u8.ToArray());
        await Assert.ThrowsAsync<IOException>(() => fixture.Engine.ResolveConflictsAsync(new ConflictResolutionPlan
        {
            GameInstanceId = fixture.GameInstance.Id,
            Decisions = new[] { new ConflictResolutionDecision { RelativePath = "games/game-a/default/one.sav", Action = ConflictResolutionAction.KeepRemote } },
        }, CancellationToken.None));
        Assert.Equal("original", await File.ReadAllTextAsync(path));
        Assert.Single(fixture.SyncHistoryRepository.Snapshots);
        Assert.Equal(SyncJobStatus.Failed, Assert.Single(fixture.SyncHistoryRepository.Records).Status);
    }

    [Fact]
    public async Task LocalRestore_DoesNotRequireTheRemoteTargetToStillExist()
    {
        var deleted = false;
        var fixture = new SyncEngineFixture(storageConfigurationResolver: binding => deleted ? throw new InvalidOperationException("Target removed") : binding.Settings);
        var path = Path.Combine(fixture.GameInstance.InstallDirectory!, "one.sav");
        Directory.CreateDirectory(fixture.GameInstance.InstallDirectory!);
        await File.WriteAllTextAsync(path, "original");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/one.sav"] = "downloaded"u8.ToArray();
        using (var cts = new CancellationTokenSource())
            await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Download }, cts);
        deleted = true;
        await fixture.Engine.RestoreSnapshotAsync(Assert.Single(fixture.SyncHistoryRepository.Snapshots).Id, CancellationToken.None);
        Assert.Equal("original", await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProtectedUpload_NeverOverwritesConflictsOrIgnoresRemoteOnlyFiles(bool remoteOnly)
    {
        var fixture = new SyncEngineFixture();
        Directory.CreateDirectory(fixture.GameInstance.InstallDirectory!);
        await File.WriteAllTextAsync(Path.Combine(fixture.GameInstance.InstallDirectory!, "one.sav"), "local");
        var remote = "games/game-a/default/" + (remoteOnly ? "other.sav" : "one.sav");
        fixture.StorageProvider.RemoteFiles[remote] = "remote"u8.ToArray();
        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Upload, ProtectRemoteChanges = true }, cts);
        Assert.Equal(SyncJobStatus.Failed, Assert.Single(fixture.SyncHistoryRepository.Records).Status);
        Assert.Equal("remote", System.Text.Encoding.UTF8.GetString(fixture.StorageProvider.RemoteFiles[remote]));
        Assert.Empty(fixture.StorageProvider.UploadedPaths);
    }

    [Fact]
    public async Task ExplicitUpload_BacksUpOldRemoteVersionBeforeReplacement()
    {
        var fixture = new SyncEngineFixture();
        var path = Path.Combine(fixture.GameInstance.InstallDirectory!, "one.sav");
        Directory.CreateDirectory(fixture.GameInstance.InstallDirectory!);
        await File.WriteAllTextAsync(path, "local");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/one.sav"] = "old-remote"u8.ToArray();
        using (var cts = new CancellationTokenSource())
            await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Upload }, cts);
        Assert.Equal("local", System.Text.Encoding.UTF8.GetString(fixture.StorageProvider.RemoteFiles["games/game-a/default/one.sav"]));
        var snapshot = Assert.Single(fixture.SyncHistoryRepository.Snapshots);
        Assert.Equal(snapshot.Id, Assert.Single(fixture.SyncHistoryRepository.Records).SnapshotId);
        await fixture.Engine.RestoreSnapshotAsync(snapshot.Id, CancellationToken.None);
        Assert.Equal("old-remote", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ConflictPlan_IsFullyValidatedBeforeAnyDecisionIsApplied()
    {
        var fixture = new SyncEngineFixture();
        var path = Path.Combine(fixture.GameInstance.InstallDirectory!, "one.sav");
        Directory.CreateDirectory(fixture.GameInstance.InstallDirectory!);
        await File.WriteAllTextAsync(path, "local");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/one.sav"] = "remote"u8.ToArray();
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Engine.ResolveConflictsAsync(new ConflictResolutionPlan
        {
            GameInstanceId = fixture.GameInstance.Id,
            Decisions = new[]
            {
                new ConflictResolutionDecision { RelativePath = "games/game-a/default/one.sav", Action = ConflictResolutionAction.KeepLocal },
                new ConflictResolutionDecision { RelativePath = "games/game-a/unknown/two.sav", Action = ConflictResolutionAction.KeepRemote },
            },
        }, CancellationToken.None));
        Assert.Empty(fixture.StorageProvider.UploadedPaths);
        Assert.Equal("remote", System.Text.Encoding.UTF8.GetString(fixture.StorageProvider.RemoteFiles["games/game-a/default/one.sav"]));
    }

    [Fact]
    public async Task Restore_WaitsForAnActiveDownloadForTheSameGame()
    {
        var fixture = new SyncEngineFixture();
        var path = Path.Combine(fixture.GameInstance.InstallDirectory!, "one.sav");
        Directory.CreateDirectory(fixture.GameInstance.InstallDirectory!);
        await File.WriteAllTextAsync(path, "original");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/one.sav"] = "downloaded"u8.ToArray();
        fixture.StorageProvider.DownloadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.StorageProvider.DownloadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        var worker = fixture.RunQueueAsync(cts.Token);
        var job = new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Download };
        await fixture.Queue.QueueAsync(job, CancellationToken.None);
        await fixture.StorageProvider.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var restore = fixture.Engine.RestoreSnapshotAsync(Assert.Single(fixture.SyncHistoryRepository.Snapshots).Id, CancellationToken.None);
        Assert.False(restore.IsCompleted);
        fixture.StorageProvider.DownloadGate.SetResult();
        await restore.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("original", await File.ReadAllTextAsync(path));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FreshDevice_DownloadsAndRestoresMissingDottedDirectoriesAndExtensionlessFiles(bool directory)
    {
        var template = "%GAME_INSTALL_DIR%/" + (directory ? "saves.v2" : "save");
        var source = new SyncEngineFixture(pathTemplates: new[] { template });
        var relative = directory ? "saves.v2/slot.sav" : "save";
        var original = Path.Combine(source.GameInstance.InstallDirectory!, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(original)!);
        await File.WriteAllTextAsync(original, "source-save");
        using (var cts = new CancellationTokenSource())
            await RunQueuedJobAsync(source, new SyncJob { GameInstanceId = source.GameInstance.Id, Direction = SyncDirection.Upload }, cts);

        var target = new SyncEngineFixture(pathTemplates: new[] { template });
        foreach (var pair in source.StorageProvider.RemoteFiles) target.StorageProvider.RemoteFiles[pair.Key] = pair.Value;
        foreach (var pair in source.StorageProvider.MetadataFiles) target.StorageProvider.MetadataFiles[pair.Key] = pair.Value;
        foreach (var pair in source.StorageProvider.ImmutableFiles) target.StorageProvider.ImmutableFiles[pair.Key] = pair.Value;
        using (var cts = new CancellationTokenSource())
            await RunQueuedJobAsync(target, new SyncJob { GameInstanceId = target.GameInstance.Id, Direction = SyncDirection.Download }, cts);
        var path = Path.Combine(target.GameInstance.InstallDirectory!, relative);
        Assert.Equal("source-save", await File.ReadAllTextAsync(path));
        var snapshot = Assert.Single(target.SyncHistoryRepository.Snapshots);
        await target.Engine.RestoreSnapshotAsync(snapshot.Id, CancellationToken.None);
        Assert.False(File.Exists(path));
        await target.Engine.RestoreSnapshotAsync(target.SyncHistoryRepository.Snapshots.Last().Id, CancellationToken.None);
        Assert.Equal("source-save", await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData("same")]
    [InlineData("inside")]
    [InlineData("parent")]
    public async Task OverlappingSaveAndStoragePaths_AreRejectedBeforeUploading(string overlap)
    {
        var fixture = new SyncEngineFixture();
        Directory.CreateDirectory(fixture.GameInstance.InstallDirectory!);
        await File.WriteAllTextAsync(Path.Combine(fixture.GameInstance.InstallDirectory!, "slot.sav"), "save");
        fixture.StorageProvider.LocalRootPath = overlap switch
        {
            "same" => fixture.GameInstance.InstallDirectory,
            "inside" => Path.Combine(fixture.GameInstance.InstallDirectory!, "backup"),
            _ => fixture.RootPath,
        };
        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Upload }, cts);
        Assert.Equal(SyncJobStatus.Failed, Assert.Single(fixture.SyncHistoryRepository.Records).Status);
        Assert.Empty(fixture.StorageProvider.UploadedPaths);
    }

    [Fact]
    public async Task OverlappingSaveAndStoragePaths_ThroughDirectoryAlias_AreRejectedBeforeWriting()
    {
        var fixture = new SyncEngineFixture();
        Directory.CreateDirectory(fixture.GameInstance.InstallDirectory!);
        await File.WriteAllTextAsync(Path.Combine(fixture.GameInstance.InstallDirectory!, "slot.sav"), "save");
        var alias = Path.Combine(fixture.RootPath, "alias");
        if (OperatingSystem.IsWindows())
        {
            var info = new System.Diagnostics.ProcessStartInfo("cmd.exe") { CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "/c", "mklink", "/J", alias, fixture.GameInstance.InstallDirectory! }) info.ArgumentList.Add(argument);
            using var process = System.Diagnostics.Process.Start(info)!;
            await process.WaitForExitAsync();
            Assert.Equal(0, process.ExitCode);
        }
        else Directory.CreateSymbolicLink(alias, fixture.GameInstance.InstallDirectory!);
        try
        {
            // Include a missing descendant: the existing ancestor link must still be resolved.
            fixture.StorageProvider.LocalRootPath = Path.Combine(alias, "backup");
            using var cts = new CancellationTokenSource();
            await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Upload }, cts);
            var record = Assert.Single(fixture.SyncHistoryRepository.Records);
            Assert.Equal(SyncJobStatus.Failed, record.Status);
            Assert.Contains("must not overlap", record.ErrorMessage);
            Assert.Empty(fixture.StorageProvider.UploadedPaths);
            Assert.False(Directory.Exists(Path.Combine(alias, "backup")));
        }
        finally { Directory.Delete(alias); }
    }

    [Fact]
    public async Task Upload_WithOnlyConfigFilesAndMissingEnabledSaves_DoesNotClaimSuccessfulBackup()
    {
        var fixture = new SyncEngineFixture(contentItems: new[]
        {
            new ContentItem { ContentId = "saves", Category = ContentCategory.Save, PathTemplates = new[] { "%GAME_INSTALL_DIR%/saves" } },
            new ContentItem { ContentId = "config", Category = ContentCategory.Config, PathTemplates = new[] { "%GAME_INSTALL_DIR%/config" } },
        });
        Directory.CreateDirectory(Path.Combine(fixture.GameInstance.InstallDirectory!, "config"));
        await File.WriteAllTextAsync(Path.Combine(fixture.GameInstance.InstallDirectory!, "config/settings.ini"), "config");
        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Upload }, cts);
        var record = Assert.Single(fixture.SyncHistoryRepository.Records);
        Assert.Equal(SyncJobStatus.Failed, record.Status);
        Assert.Contains("No save files", record.ErrorMessage);
        Assert.Empty(fixture.StorageProvider.UploadedPaths);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictResolution_RejectsChangesMadeAfterComparison(bool remoteChanged)
    {
        var fixture = new SyncEngineFixture();
        var local = Path.Combine(fixture.RootPath, "game-a", "one.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(local)!);
        await File.WriteAllTextAsync(local, "local-version");
        const string remote = "games/game-a/default/one.sav";
        fixture.StorageProvider.RemoteFiles[remote] = "remote-version"u8.ToArray();
        var compared = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Compare }, CancellationToken.None);
        var entry = Assert.Single(compared.Entries);
        if (remoteChanged) fixture.StorageProvider.RemoteFiles[remote] = "new-remote-version"u8.ToArray();
        else await File.WriteAllTextAsync(local, "new-local-version");
        var plan = new ConflictResolutionPlan { GameInstanceId = fixture.GameInstance.Id, Decisions = new[]
        {
            new ConflictResolutionDecision { RelativePath = entry.RelativePath, Action = ConflictResolutionAction.KeepRemote,
                VerifyComparedVersions = true, ExpectedLocalHash = entry.LocalHash, ExpectedRemoteHash = entry.RemoteHash }
        } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Engine.ResolveConflictsAsync(plan, CancellationToken.None));
        Assert.Equal(remoteChanged ? "local-version" : "new-local-version", await File.ReadAllTextAsync(local));
        Assert.Empty(fixture.StorageProvider.UploadedPaths);
        Assert.Equal(SyncJobStatus.Failed, Assert.Single(fixture.SyncHistoryRepository.Records).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FullComparisonPrecondition_RejectsUnrelatedChangesDuringDownload(bool remoteChanged)
    {
        var fixture = new SyncEngineFixture(autoSnapshotBeforeDownloadEnabled: false);
        var folder = fixture.GameInstance.InstallDirectory!;
        Directory.CreateDirectory(folder);
        var first = Path.Combine(folder, "one.sav");
        var other = Path.Combine(folder, "two.sav");
        await File.WriteAllTextAsync(first, "old");
        await File.WriteAllTextAsync(other, "unchanged");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/one.sav"] = "new"u8.ToArray();
        fixture.StorageProvider.RemoteFiles["games/game-a/default/two.sav"] = "unchanged"u8.ToArray();
        var comparison = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Compare }, CancellationToken.None);
        var entry = comparison.Entries.Single(e => e.RelativePath.EndsWith("one.sav"));
        var plan = new ConflictResolutionPlan { GameInstanceId = fixture.GameInstance.Id, ExpectedComparison = comparison, Decisions = new[]
        {
            new ConflictResolutionDecision { RelativePath = entry.RelativePath, Action = ConflictResolutionAction.KeepRemote,
                VerifyComparedVersions = true, ExpectedLocalHash = entry.LocalHash, ExpectedRemoteHash = entry.RemoteHash }
        } };
        fixture.StorageProvider.DownloadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.StorageProvider.DownloadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = fixture.Engine.ResolveConflictsAsync(plan, CancellationToken.None);
        await fixture.StorageProvider.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        if (remoteChanged) fixture.StorageProvider.RemoteFiles["games/game-a/default/two.sav"] = "new-other"u8.ToArray();
        else await File.WriteAllTextAsync(other, "new-other");
        fixture.StorageProvider.DownloadGate.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
        Assert.Equal("old", await File.ReadAllTextAsync(first));
        Assert.Single(fixture.SyncHistoryRepository.Snapshots);
    }

    [Fact]
    public async Task FullComparisonPrecondition_RejectsTargetConfigurationChange()
    {
        IReadOnlyDictionary<string, string> configuration = new Dictionary<string, string> { ["rootPath"] = "first" };
        var fixture = new SyncEngineFixture(storageConfigurationResolver: _ => configuration);
        Directory.CreateDirectory(fixture.GameInstance.InstallDirectory!);
        var local = Path.Combine(fixture.GameInstance.InstallDirectory!, "one.sav");
        await File.WriteAllTextAsync(local, "old");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/one.sav"] = "new"u8.ToArray();
        var comparison = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Compare }, CancellationToken.None);
        var entry = Assert.Single(comparison.Entries);
        configuration = new Dictionary<string, string> { ["rootPath"] = "second" };
        var plan = new ConflictResolutionPlan { GameInstanceId = fixture.GameInstance.Id, ExpectedComparison = comparison, Decisions = new[]
        {
            new ConflictResolutionDecision { RelativePath = entry.RelativePath, Action = ConflictResolutionAction.KeepRemote,
                VerifyComparedVersions = true, ExpectedLocalHash = entry.LocalHash, ExpectedRemoteHash = entry.RemoteHash }
        } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Engine.ResolveConflictsAsync(plan, CancellationToken.None));
        Assert.Equal("old", await File.ReadAllTextAsync(local));
    }

    [Fact]
    public async Task AutomaticPull_RejectsOverlappingContentRoots()
    {
        var fixture = new SyncEngineFixture(contentItems: new[]
        {
            new ContentItem { ContentId = "first", Category = ContentCategory.Save, PathTemplates = new[] { "%GAME_INSTALL_DIR%" } },
            new ContentItem { ContentId = "second", Category = ContentCategory.Save, PathTemplates = new[] { "%GAME_INSTALL_DIR%" } },
        });
        Directory.CreateDirectory(fixture.GameInstance.InstallDirectory!);
        var local = Path.Combine(fixture.GameInstance.InstallDirectory!, "slot.sav");
        await File.WriteAllTextAsync(local, "common");
        fixture.StorageProvider.RemoteFiles["games/game-a/first/slot.sav"] = "common"u8.ToArray();
        fixture.StorageProvider.RemoteFiles["games/game-a/second/slot.sav"] = "common"u8.ToArray();
        var initial = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Compare }, CancellationToken.None);
        var baseline = AutoPullPlanner.ConfirmBaseline(fixture.GameInstance.Id, initial);
        fixture.StorageProvider.RemoteFiles["games/game-a/first/slot.sav"] = "changed"u8.ToArray();
        var current = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Compare }, CancellationToken.None);
        var evaluation = AutoPullPlanner.Evaluate(fixture.GameInstance.Id, current, baseline);
        Assert.Equal(AutoPullDisposition.Pull, evaluation.Disposition);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Engine.ResolveConflictsAsync(evaluation.Plan!, CancellationToken.None));
        Assert.Equal("common", await File.ReadAllTextAsync(local));
    }

    [Fact]
    public async Task AutomaticPull_RequiresBaselineAvoidsDuplicatesSnapshotsAndStopsForLocalEdits()
    {
        var fixture = new SyncEngineFixture(autoSnapshotBeforeDownloadEnabled: false);
        var game = fixture.GameInstance;
        Directory.CreateDirectory(game.InstallDirectory!);
        var local = Path.Combine(game.InstallDirectory!, "one.sav");
        const string remote = "games/game-a/default/one.sav";
        await File.WriteAllTextAsync(local, "common");
        fixture.StorageProvider.RemoteFiles[remote] = "common"u8.ToArray();
        var store = new AutoPullBaselineStore(new FakeAppPathService(fixture.RootPath));
        var coordinator = new AutoPullCoordinator(new FakeGameInstanceRepository(game), fixture.Engine, fixture.Queue, store, NullLogger<AutoPullCoordinator>.Instance);
        await coordinator.ScanAsync(CancellationToken.None);
        Assert.Empty(fixture.Queue.PendingJobs);
        coordinator.SetEnabled(true);
        await coordinator.ScanAsync(CancellationToken.None);
        Assert.Equal("baseline-required", Assert.Single(coordinator.Statuses).State);
        await coordinator.ConfirmBaselineAsync(game.Id, CancellationToken.None);
        fixture.StorageProvider.RemoteFiles[remote] = "remote-new"u8.ToArray();
        await coordinator.ScanAsync(CancellationToken.None);
        var job = Assert.Single(fixture.Queue.PendingJobs);
        await coordinator.ScanAsync(CancellationToken.None);
        Assert.Single(fixture.Queue.PendingJobs);
        using var cts = new CancellationTokenSource();
        var worker = fixture.RunQueueAsync(cts.Token);
        for (var i = 0; i < 150 && job.Status is SyncJobStatus.Queued or SyncJobStatus.Running; i++) await Task.Delay(20);
        Assert.Equal(SyncJobStatus.Completed, job.Status);
        Assert.Equal("remote-new", await File.ReadAllTextAsync(local));
        Assert.Single(fixture.SyncHistoryRepository.Snapshots);
        Assert.Empty(fixture.StorageProvider.UploadedPaths);
        await coordinator.ScanAsync(CancellationToken.None);
        Assert.Equal("ready", Assert.Single(coordinator.Statuses).State);
        await File.WriteAllTextAsync(local, "local-new");
        fixture.StorageProvider.RemoteFiles[remote] = "remote-newer"u8.ToArray();
        await coordinator.ScanAsync(CancellationToken.None);
        Assert.Empty(fixture.Queue.PendingJobs);
        Assert.Equal("review", Assert.Single(coordinator.Statuses).State);
        Assert.Equal("local-new", await File.ReadAllTextAsync(local));
        coordinator.SetEnabled(false);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
    }

    [Fact]
    public async Task DisablingAutomaticPullRemovesPendingWork()
    {
        var fixture = new SyncEngineFixture();
        var game = fixture.GameInstance;
        Directory.CreateDirectory(game.InstallDirectory!);
        var local = Path.Combine(game.InstallDirectory!, "one.sav");
        await File.WriteAllTextAsync(local, "common");
        const string remote = "games/game-a/default/one.sav";
        fixture.StorageProvider.RemoteFiles[remote] = "common"u8.ToArray();
        var coordinator = new AutoPullCoordinator(new FakeGameInstanceRepository(game), fixture.Engine, fixture.Queue,
            new AutoPullBaselineStore(new FakeAppPathService(fixture.RootPath)), NullLogger<AutoPullCoordinator>.Instance);
        coordinator.SetEnabled(true);
        await coordinator.ConfirmBaselineAsync(game.Id, CancellationToken.None);
        fixture.StorageProvider.RemoteFiles[remote] = "changed"u8.ToArray();
        await coordinator.ScanAsync(CancellationToken.None);
        var job = Assert.Single(fixture.Queue.PendingJobs);
        coordinator.SetEnabled(false);
        Assert.Empty(fixture.Queue.PendingJobs);
        Assert.Equal(SyncJobStatus.Cancelled, job.Status);
        Assert.Equal("common", await File.ReadAllTextAsync(local));
    }

    [Fact]
    public async Task DisablingAutomaticPullCancelsActiveDownloadBeforeApplyingIt()
    {
        var fixture = new SyncEngineFixture();
        var game = fixture.GameInstance;
        Directory.CreateDirectory(game.InstallDirectory!);
        var local = Path.Combine(game.InstallDirectory!, "one.sav");
        await File.WriteAllTextAsync(local, "common");
        const string remote = "games/game-a/default/one.sav";
        fixture.StorageProvider.RemoteFiles[remote] = "common"u8.ToArray();
        var coordinator = new AutoPullCoordinator(new FakeGameInstanceRepository(game), fixture.Engine, fixture.Queue,
            new AutoPullBaselineStore(new FakeAppPathService(fixture.RootPath)), NullLogger<AutoPullCoordinator>.Instance);
        coordinator.SetEnabled(true);
        await coordinator.ConfirmBaselineAsync(game.Id, CancellationToken.None);
        fixture.StorageProvider.RemoteFiles[remote] = "changed"u8.ToArray();
        await coordinator.ScanAsync(CancellationToken.None);
        var job = Assert.Single(fixture.Queue.PendingJobs);
        fixture.StorageProvider.DownloadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.StorageProvider.DownloadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        var worker = fixture.RunQueueAsync(cts.Token);
        await fixture.StorageProvider.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        coordinator.SetEnabled(false);
        for (var i = 0; i < 150 && job.Status is SyncJobStatus.Queued or SyncJobStatus.Running; i++) await Task.Delay(20);
        Assert.Equal(SyncJobStatus.Cancelled, job.Status);
        Assert.Equal("common", await File.ReadAllTextAsync(local));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
    }

    [Fact]
    public async Task UploadCapturesAllLocalFilesBeforeNetworkWritesCanObserveLaterGameProgress()
    {
        var fixture = new SyncEngineFixture();
        var root = Path.Combine(fixture.RootPath, "game-a");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "one.sav"), "old-one");
        await File.WriteAllTextAsync(Path.Combine(root, "two.sav"), "old-two");
        var changed = false;
        fixture.StorageProvider.AfterUpload = path =>
        {
            if (!path.EndsWith("one.sav", StringComparison.Ordinal)) return;
            File.WriteAllText(Path.Combine(root, "two.sav"), "later-game-progress");
            changed = true;
        };
        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Upload }, cts);
        Assert.True(changed);
        Assert.Equal(SyncJobStatus.Completed, Assert.Single(fixture.SyncHistoryRepository.Records).Status);
        Assert.Equal("old-two", System.Text.Encoding.UTF8.GetString(fixture.StorageProvider.RemoteFiles["games/game-a/default/two.sav"]));
        Assert.Equal("later-game-progress", await File.ReadAllTextAsync(Path.Combine(root, "two.sav")));
    }

    [Fact]
    public async Task ApplicationShutdownWaitsForTheSyncCancellationRecordAndCleanup()
    {
        using var host = new ApplicationTaskHost();
        var fixture = new SyncEngineFixture(applicationTasks: host);
        var local = Path.Combine(fixture.RootPath, "game-a", "one.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(local)!);
        await File.WriteAllTextAsync(local, "old");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/one.sav"] = "remote"u8.ToArray();
        fixture.StorageProvider.DownloadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.StorageProvider.DownloadGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = host.RunAsync(token => Task.Run(() => fixture.RunQueueAsync(token), token));
        var job = new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Download };
        await fixture.Engine.QueueAsync(job, CancellationToken.None);
        await fixture.StorageProvider.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();
        Assert.True(worker.IsCanceled);
        Assert.Equal(SyncJobStatus.Cancelled, job.Status);
        Assert.Equal(SyncJobStatus.Cancelled, Assert.Single(fixture.SyncHistoryRepository.Records).Status);
        Assert.Equal("old", await File.ReadAllTextAsync(local));
        Assert.DoesNotContain(Directory.GetFiles(Path.GetDirectoryName(local)!), GSYNC.Core.Utilities.AtomicFile.IsTemporary);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoteDescriptorsAreBounded_AndCheckedAgainstTheCommittedHash(bool oversized)
    {
        var fixture = new SyncEngineFixture();
        var local = Path.Combine(fixture.RootPath, "game-a", "one.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(local)!);
        await File.WriteAllTextAsync(local, "local");
        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Upload }, cts);
        var metadata = fixture.StorageProvider.ImmutableFiles.Keys.Single(path => path.Contains("/__gsync_meta/", StringComparison.Ordinal));
        fixture.StorageProvider.ImmutableFiles[metadata] = oversized ? new byte[65537] : "{\"IsDirectory\":true,\"Version\":1}"u8.ToArray();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Engine.CompareAsync(
            new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Compare }, CancellationToken.None));
        Assert.Contains(oversized ? "too large" : "integrity", error.Message);
        Assert.Equal("local", await File.ReadAllTextAsync(local));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DownloadNeverClaimsSuccessWhenEnabledSavesAreMissingRemotely(bool hasRemoteConfig)
    {
        var fixture = new SyncEngineFixture(contentItems: new[]
        {
            new ContentItem { ContentId = "saves", Category = ContentCategory.Save, PathTemplates = new[] { "%GAME_INSTALL_DIR%/saves" } },
            new ContentItem { ContentId = "config", Category = ContentCategory.Config, PathTemplates = new[] { "%GAME_INSTALL_DIR%/settings.cfg" } },
        });
        var save = Path.Combine(fixture.RootPath, "game-a", "saves", "one.sav");
        var config = Path.Combine(fixture.RootPath, "game-a", "settings.cfg");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        await File.WriteAllTextAsync(save, "current-save");
        await File.WriteAllTextAsync(config, "current-config");
        if (hasRemoteConfig) fixture.StorageProvider.RemoteFiles["games/game-a/config/settings.cfg"] = "remote-config"u8.ToArray();
        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, new SyncJob { GameInstanceId = fixture.GameInstance.Id, Direction = SyncDirection.Download }, cts);
        Assert.Equal(SyncJobStatus.Failed, Assert.Single(fixture.SyncHistoryRepository.Records).Status);
        Assert.Empty(fixture.SyncHistoryRepository.Snapshots);
        Assert.Equal("current-save", await File.ReadAllTextAsync(save));
        Assert.Equal("current-config", await File.ReadAllTextAsync(config));
    }

    private static async Task RunQueuedJobAsync(SyncEngineFixture fixture, SyncJob job, CancellationTokenSource queueCts)
    {
        var worker = fixture.RunQueueAsync(queueCts.Token);
        await fixture.Queue.QueueAsync(job, CancellationToken.None);
        var limit = DateTime.UtcNow.AddSeconds(5);
        while (job.Status is SyncJobStatus.Queued or SyncJobStatus.Running)
        {
            if (DateTime.UtcNow > limit) throw new TimeoutException("Queued job did not finish.");
            await Task.Delay(10);
        }
        queueCts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await worker);
    }

    private sealed class SyncEngineFixture
    {
        public SyncEngineFixture(bool delayDownloads = false, bool autoSnapshotBeforeDownloadEnabled = true,
            IReadOnlyList<string>? pathTemplates = null, IReadOnlyDictionary<string, string>? bindingSettings = null,
            Func<StorageBinding, IReadOnlyDictionary<string, string>>? storageConfigurationResolver = null,
            IReadOnlyList<string>? includePatterns = null, IReadOnlyList<string>? excludePatterns = null,
            Func<IReadOnlyDictionary<string, string>>? userVariablesProvider = null, IReadOnlyList<ContentItem>? contentItems = null,
            ApplicationTaskHost? applicationTasks = null)
        {
            RootPath = Path.Combine(Path.GetTempPath(), "gsync-sync-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);

            GameInstance = new GameInstance
            {
                GameId = "game-a",
                DisplayName = "Game A",
                SourceProviderId = "custom",
                InstallDirectory = Path.Combine(RootPath, "game-a"),
            };

            Binding = new StorageBinding
            {
                GameInstanceId = GameInstance.Id,
                StorageProviderId = "fake-storage",
                RemoteNamespace = "games/game-a",
                Settings = bindingSettings ?? new Dictionary<string, string>(),
            };

            Queue = new SyncQueue();
            StorageProvider = new FakeStorageProvider(delayDownloads);
            SyncHistoryRepository = new FakeSyncHistoryRepository();
            Engine = new SyncEngine(
                Queue,
                new FakeGameInstanceRepository(GameInstance),
                new FakeStorageBindingRepository(Binding),
                SyncHistoryRepository,
                new FakeManifestService(pathTemplates, includePatterns, excludePatterns, contentItems),
                new ISourceProvider[] { new FakeSourceProvider() },
                new IStorageProvider[] { StorageProvider },
                new FakeAppPathService(RootPath),
                new PathResolver(),
                new SystemVariableProvider(),
                NullLogger<SyncEngine>.Instance,
                () => autoSnapshotBeforeDownloadEnabled,
                storageConfigurationResolver, userVariablesProvider, applicationTasks);
        }

        public string RootPath { get; }

        public GameInstance GameInstance { get; }

        public StorageBinding Binding { get; }

        public SyncQueue Queue { get; }

        public FakeStorageProvider StorageProvider { get; }

        public FakeSyncHistoryRepository SyncHistoryRepository { get; }

        public SyncEngine Engine { get; }

        public Task RunQueueAsync(CancellationToken cancellationToken) => Engine.ProcessQueuedJobsAsync(cancellationToken);

        public async Task WaitForRecordsAsync(int count)
        {
            for (var i = 0; i < 100; i++)
            {
                if (SyncHistoryRepository.Records.Count >= count)
                {
                    return;
                }

                await Task.Delay(20);
            }

            throw new TimeoutException("Sync record was not written in time.");
        }

        public async Task WaitForActiveJobAsync()
        {
            for (var i = 0; i < 100; i++)
            {
                if (Queue.ActiveJob is not null)
                {
                    return;
                }

                await Task.Delay(20);
            }

            throw new TimeoutException("No active job started in time.");
        }
    }

    private sealed class FakeManifestService : IManifestService
    {
        private readonly GameContentDefinition _definition;

        public FakeManifestService(IReadOnlyList<string>? pathTemplates = null, IReadOnlyList<string>? includePatterns = null,
            IReadOnlyList<string>? excludePatterns = null, IReadOnlyList<ContentItem>? contentItems = null)
        {
            _definition = new()
            {
                GameId = "game-a",
                DisplayName = "Game A",
                ContentItems = contentItems ?? new[]
                {
                    new ContentItem
                    {
                        ContentId = "default",
                        Category = ContentCategory.Save,
                        PathTemplates = pathTemplates ?? new[] { "%GAME_INSTALL_DIR%" },
                        IncludePatterns = includePatterns ?? Array.Empty<string>(),
                        ExcludePatterns = excludePatterns ?? Array.Empty<string>(),
                        DefaultEnabled = true,
                    },
                },
            };
        }

        public Task<IReadOnlyList<GameContentDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<GameContentDefinition>>(new[] { _definition });

        public Task<GameContentDefinition?> GetDefinitionAsync(string gameId, CancellationToken cancellationToken)
            => Task.FromResult<GameContentDefinition?>(_definition);

        public Task RefreshCommunityDefinitionsAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class FakeGameInstanceRepository : IGameInstanceRepository
    {
        private readonly GameInstance _instance;

        public FakeGameInstanceRepository(GameInstance instance)
        {
            _instance = instance;
        }

        public Task<IReadOnlyList<GameInstance>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<GameInstance>>(new[] { _instance });

        public Task<GameInstance?> GetAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult<GameInstance?>(_instance.Id == id ? _instance : null);

        public Task UpsertAsync(GameInstance instance, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeStorageBindingRepository : IStorageBindingRepository
    {
        private readonly StorageBinding _binding;

        public FakeStorageBindingRepository(StorageBinding binding)
        {
            _binding = binding;
        }

        public Task<IReadOnlyList<StorageBinding>> ListByGameInstanceAsync(Guid gameInstanceId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<StorageBinding>>(new[] { _binding });

        public Task<StorageBinding?> GetAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult<StorageBinding?>(_binding.Id == id ? _binding : null);

        public Task UpsertAsync(StorageBinding binding, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSyncHistoryRepository : ISyncHistoryRepository
    {
        public List<SyncRecord> Records { get; } = new();

        public List<Snapshot> Snapshots { get; } = new();

        public bool FailSnapshotWrites { get; set; }

        public Task AddRecordAsync(SyncRecord record, CancellationToken cancellationToken)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SyncRecord>> ListRecordsAsync(Guid gameInstanceId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SyncRecord>>(Records.Where(r => r.GameInstanceId == gameInstanceId).ToArray());

        public Task<IReadOnlyList<SyncRecord>> ListRecentRecordsAsync(int limit, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SyncRecord>>(Records.OrderByDescending(r => r.StartedAtUtc).Take(limit).ToArray());

        public Task AddSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken)
        {
            if (FailSnapshotWrites) throw new IOException("Snapshot database write failed.");
            Snapshots.Add(snapshot);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Snapshot>> ListSnapshotsAsync(Guid gameInstanceId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Snapshot>>(Snapshots.Where(s => s.GameInstanceId == gameInstanceId).ToArray());

        public Task<Snapshot?> GetSnapshotAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Snapshots.FirstOrDefault(s => s.Id == id));
    }

    private sealed class FakeSourceProvider : ISourceProvider
    {
        public string ProviderId => "custom";

        public string DisplayName => "Custom";

        public Task<IReadOnlyList<DiscoveredGame>> ScanAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DiscoveredGame>>(Array.Empty<DiscoveredGame>());

        public IReadOnlyDictionary<string, string> ResolveVariables(GameInstance instance)
            => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["GAME_INSTALL_DIR"] = instance.InstallDirectory ?? string.Empty,
            };
    }

    private sealed class FakeStorageProvider : IStorageProvider
    {
        private readonly bool _delayDownloads;

        public FakeStorageProvider(bool delayDownloads)
        {
            _delayDownloads = delayDownloads;
        }

        public string ProviderId => "fake-storage";

        public string DisplayName => "Fake Storage";
        public string? LocalRootPath { get; set; }

        public Dictionary<string, byte[]> RemoteFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, byte[]> MetadataFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, byte[]> ImmutableFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> UploadedPaths { get; } = new();
        public Action<string>? AfterUpload { get; set; }

        public bool OmitHashes { get; set; }
        public int? FailDownloadNumber { get; set; }
        public bool CancelInsteadOfFail { get; set; }
        public Func<string, Stream>? DownloadContentFactory { get; set; }
        public TaskCompletionSource? DownloadStarted { get; set; }
        public TaskCompletionSource? DownloadGate { get; set; }
        private int _downloads;
        public IReadOnlyDictionary<string, string>? LastConfiguration { get; private set; }

        public IStorageProvider WithConfiguration(IReadOnlyDictionary<string, string> configuration)
        {
            LastConfiguration = new Dictionary<string, string>(configuration);
            return this;
        }

        public Task<ConnectionResult> TestConnectionAsync(IReadOnlyDictionary<string, string> configuration, CancellationToken cancellationToken)
            => Task.FromResult(ConnectionResult.Success());

        public async Task UploadAsync(string remotePath, Stream content, CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream();
            await content.CopyToAsync(memory, cancellationToken);
            UploadedPaths.Add(remotePath);
            (remotePath.Contains("/__gsync_versions/", StringComparison.Ordinal) ? ImmutableFiles :
                remotePath.Contains("/__gsync_meta/", StringComparison.Ordinal) ? MetadataFiles : RemoteFiles)[remotePath] = memory.ToArray();
            AfterUpload?.Invoke(remotePath);
        }

        public async Task<StorageDownloadResult> DownloadVersionedAsync(string path, CancellationToken cancellationToken)
        {
            var result = await DownloadAsync(path, cancellationToken);
            return new StorageDownloadResult { Content = result.Content, Length = result.Length,
                Revision = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(MetadataFiles[path])) };
        }

        public async Task<bool> TryUploadAsync(string path, Stream content, string? expectedRevision, CancellationToken cancellationToken)
        {
            using var bytes = new MemoryStream();
            await content.CopyToAsync(bytes, cancellationToken);
            var revision = MetadataFiles.TryGetValue(path, out var current) ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(current)) : null;
            if (revision != expectedRevision) return false;
            MetadataFiles[path] = bytes.ToArray();
            var prefix = path[..path.IndexOf("/__gsync_meta/current.json", StringComparison.Ordinal)];
            using var manifest = System.Text.Json.JsonDocument.Parse(bytes.ToArray());
            foreach (var file in manifest.RootElement.GetProperty("Files").EnumerateArray())
            {
                var logical = prefix + "/" + file.GetProperty("Path").GetString();
                var blob = file.GetProperty("BlobPath").GetString()!;
                (logical.Contains("/__gsync_meta/", StringComparison.Ordinal) ? MetadataFiles : RemoteFiles)[logical] = ImmutableFiles[blob];
            }
            return true;
        }

        public async Task<StorageDownloadResult> DownloadAsync(string remotePath, CancellationToken cancellationToken)
        {
            if (remotePath.Contains("/__gsync_meta/", StringComparison.Ordinal))
            {
                if (!MetadataFiles.TryGetValue(remotePath, out var metadata) && !ImmutableFiles.TryGetValue(remotePath, out metadata)) throw new FileNotFoundException();
                return new StorageDownloadResult { Content = new MemoryStream(metadata), Length = metadata.Length };
            }
            DownloadStarted?.TrySetResult();
            if (DownloadGate is not null) await DownloadGate.Task.WaitAsync(cancellationToken);
            if (++_downloads == FailDownloadNumber)
            {
                if (CancelInsteadOfFail) throw new OperationCanceledException();
                throw new IOException("Simulated connection failure.");
            }
            if (_delayDownloads)
            {
                await Task.Delay(100, cancellationToken);
            }

            var bytes = ImmutableFiles.TryGetValue(remotePath, out var immutable) ? immutable : RemoteFiles[remotePath];
            return new StorageDownloadResult
            {
                Content = DownloadContentFactory?.Invoke(remotePath) ?? new MemoryStream(bytes),
                Length = bytes.LongLength,
            };
        }

        public Task<IReadOnlyList<RemoteEntry>> ListAsync(string pathNamespace, CancellationToken cancellationToken)
        {
            var prefix = pathNamespace.Trim('/');
            var entries = RemoteFiles
                .Where(pair => pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(pair => new RemoteEntry
                {
                    Path = pair.Key,
                    IsDirectory = false,
                    Size = pair.Value.LongLength,
                    LastModifiedUtc = DateTimeOffset.UtcNow,
                    Hash = OmitHashes ? null : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pair.Value)),
                })
                .ToArray();
            return Task.FromResult<IReadOnlyList<RemoteEntry>>(entries);
        }

        public Task DeleteAsync(string remotePath, CancellationToken cancellationToken)
        {
            RemoteFiles.Remove(remotePath);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAppPathService : IAppPathService
    {
        private readonly string _rootPath;

        public FakeAppPathService(string rootPath)
        {
            _rootPath = rootPath;
        }

        public string GetAppDataRoot() => _rootPath;

        public string GetDatabasePath() => Path.Combine(_rootPath, "data.db");

        public string GetDefinitionsDirectory() => Path.Combine(_rootPath, "definitions");

        public string GetLogsDirectory() => Path.Combine(_rootPath, "logs");

        public string GetManifestCachePath() => Path.Combine(_rootPath, "community-manifest.yaml");

        public string GetSnapshotsDirectory() => Path.Combine(_rootPath, "snapshots");

        public string GetUiSettingsPath() => Path.Combine(_rootPath, "ui-settings.json");
    }
}
