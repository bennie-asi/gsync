using GSYNC.App.Infrastructure.Configuration;
using GSYNC.App.Infrastructure;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.App.Infrastructure.Wizard;
using GSYNC.App.ViewModels;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Abstractions.Sync;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Services;
using GSYNC.Core.Services.Sync;
using GSYNC.Core.Utilities;
using GSYNC.Data.Repositories;
using GSYNC.Data.Services;
using GSYNC.Manifest.Ludusavi;
using GSYNC.Manifest.Services;
using GSYNC.Storage.Options;
using GSYNC.Storage.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GSYNC.AppLogic.Tests;

public sealed class ProfilePersistenceTests
{
    [Fact]
    public async Task RemovingGamePreservesSavesAndHistoryAndAllowsAddingItAgain()
    {
        using var fixture = await Fixture.CreateAsync();
        var saves = fixture.Folder("remove-game-saves");
        var localFile = Path.Combine(saves, "slot.sav");
        await File.WriteAllTextAsync(localFile, "local save");
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Removable Game", saves);
        Assert.Null(await wizard.CreateProfileAsync());
        var id = wizard.LastCreatedInstanceId!.Value;
        var binding = Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(id, CancellationToken.None));
        await fixture.UploadAsync(id);
        var snapshotPath = Path.Combine(fixture.Folder("retained-snapshots"), "backup.zip");
        await File.WriteAllTextAsync(snapshotPath, "snapshot");
        await fixture.History.AddSnapshotAsync(new Snapshot { GameInstanceId = id, ArchivePath = snapshotPath, FileCount = 1 }, CancellationToken.None);
        var details = fixture.Details();
        await details.LoadAsync(id);

        Assert.Null(await details.RemoveGameAsync());
        Assert.Null(await fixture.Games.GetAsync(id, CancellationToken.None));
        Assert.Empty(await fixture.Bindings.ListByGameInstanceAsync(id, CancellationToken.None));
        Assert.Equal("local save", await File.ReadAllTextAsync(localFile));
        Assert.True(File.Exists(snapshotPath));
        Assert.NotEmpty(await fixture.History.ListRecordsAsync(id, CancellationToken.None));
        Assert.Single(await fixture.History.ListSnapshotsAsync(id, CancellationToken.None));
        var remote = await fixture.OpenRemoteAsync(binding.RemoteNamespace);
        using var file = await remote.DownloadAsync(binding.RemoteNamespace + "/saves/slot.sav", CancellationToken.None);
        Assert.Equal("local save", await new StreamReader(file.Content).ReadToEndAsync());

        var history = new HistoryPageViewModel(new Localization(), fixture.History, fixture.Games, fixture.Bindings, fixture.Engine);
        await history.LoadAsync();
        Assert.False(history.CanRestore);
        Assert.Contains("removed", history.SnapshotsSubtitle, StringComparison.OrdinalIgnoreCase);

        var readd = fixture.Wizard();
        PrepareManual(readd, "Removable Game", saves);
        Assert.Null(await readd.CreateProfileAsync());
        Assert.NotEqual(id, readd.LastCreatedInstanceId);
    }

    [Fact]
    public async Task RemovingGameWaitsForQueuedTasksAndRejectsConcurrentNewJobs()
    {
        using var fixture = await Fixture.CreateAsync();
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Queued Removal", fixture.Folder("queued-removal"));
        Assert.Null(await wizard.CreateProfileAsync());
        var id = wizard.LastCreatedInstanceId!.Value;
        var details = fixture.Details();
        await details.LoadAsync(id);
        await details.StopStatusCheckAsync();
        var queued = new SyncJob { GameInstanceId = id, Direction = SyncDirection.Upload };
        await fixture.Engine.QueueAsync(queued, CancellationToken.None);
        Assert.Contains("queued or running", await details.RemoveGameAsync());
        Assert.NotNull(await fixture.Games.GetAsync(id, CancellationToken.None));
        Assert.True(fixture.Queue.Remove(queued.Id));

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var removal = fixture.Engine.RunGameRemovalAsync(id, async token =>
        {
            started.SetResult();
            await release.Task.WaitAsync(token);
            await fixture.Games.DeleteAsync(id, token);
        }, CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var newJob = fixture.Engine.QueueAsync(new SyncJob { GameInstanceId = id, Direction = SyncDirection.Upload }, CancellationToken.None);
        try { Assert.False(newJob.IsCompleted); }
        finally { release.TrySetResult(); }
        await removal.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<InvalidOperationException>(() => newJob.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Empty(fixture.Queue.PendingJobs);
        Assert.Null(await fixture.Games.GetAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task FirstSync_ReviewsExistingRemoteBeforeAnyNewJobIsQueued()
    {
        using var fixture = await Fixture.CreateAsync();
        var saves = fixture.Folder("first-sync-saves");
        var path = Path.Combine(saves, "slot.sav");
        await File.WriteAllTextAsync(path, "original");
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "First Sync Game", saves);
        wizard.StartFirstSync = true;
        Assert.Null(await wizard.CreateProfileAsync());
        Assert.Empty(fixture.Queue.PendingJobs);

        await fixture.UploadAsync(wizard.LastCreatedInstanceId!.Value);
        await File.WriteAllTextAsync(path, "changed locally");
        var (context, error) = await wizard.CompareCreatedProfileAsync();
        Assert.Null(error);
        Assert.NotNull(context);
        Assert.Contains(context!.Comparison.Entries, entry => entry.ChangeKind == SyncChangeKind.Conflict && entry.RemoteHash is not null);
        Assert.Empty(fixture.Queue.PendingJobs);
        Assert.Null(await wizard.QueueFirstUploadAsync());
        Assert.True(Assert.Single(fixture.Queue.PendingJobs).ProtectRemoteChanges);
    }

    [Fact]
    public async Task Wizard_TargetConfigurationReturnPreservesGameAndSelectsTarget()
    {
        using var fixture = await Fixture.CreateAsync();
        var saves = fixture.Folder("wizard-target-saves");
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Retained Game", saves);
        wizard.CurrentStep = 5;
        var target = new SyncTargetConfig { Name = "Other target", ProviderId = "local-folder", Settings = new() { ["rootPath"] = fixture.Folder("other-target") } };
        fixture.TargetStore.Upsert(target);

        wizard.RefreshTargets(target.Id);

        Assert.Equal(5, wizard.CurrentStep);
        Assert.Equal("Retained Game", wizard.ManualGameName);
        Assert.Equal(saves, wizard.ManualSavePath);
        Assert.Equal(target.Id, wizard.SelectedTargetId);
        Assert.Contains(wizard.Targets, option => option.Key == target.Id.ToString("D") && option.IsSelected);
    }

    [Fact]
    public async Task Wizard_ManualDraftSurvivesReturningToTheFirstPhase()
    {
        using var fixture = await Fixture.CreateAsync();
        var wizard = fixture.Wizard();
        var saves = fixture.Folder("manual-draft");
        wizard.SwitchToManualSetup();
        wizard.ManualGameName = "Draft Game";
        wizard.ManualSavePath = saves;
        wizard.SetManualGame(wizard.ManualGameName, wizard.ManualSavePath);
        wizard.CurrentStep = 3;

        wizard.GoBack();

        Assert.Equal(1, wizard.CurrentStep);
        Assert.True(wizard.IsManualSourceSelected);
        Assert.Equal("Draft Game", wizard.ManualGameName);
        Assert.Equal(saves, wizard.ManualSavePath);
    }

    [Fact]
    public async Task ConflictBulkActions_SkipFilesMissingTheChosenSide()
    {
        using var fixture = await Fixture.CreateAsync();
        var viewModel = fixture.Conflicts();
        viewModel.LoadFrom(new ConflictNavigationContext(Guid.NewGuid(), "Game", new SyncComparison
        {
            Entries =
            [
                new SyncDiffEntry { RelativePath = "local", ChangeKind = SyncChangeKind.AddedLocally, LocalHash = "local-hash" },
                new SyncDiffEntry { RelativePath = "remote", ChangeKind = SyncChangeKind.AddedRemotely, RemoteHash = "remote-hash" },
                new SyncDiffEntry { RelativePath = "both", ChangeKind = SyncChangeKind.Conflict, LocalHash = "local-hash", RemoteHash = "remote-hash" },
            ],
        }));

        viewModel.SetAllActions(ConflictResolutionAction.KeepRemote);
        Assert.Equal(ConflictResolutionAction.Skip, viewModel.ConflictFiles.Single(row => row.Filename == "local").Action);
        Assert.Equal(ConflictResolutionAction.KeepRemote, viewModel.ConflictFiles.Single(row => row.Filename == "remote").Action);
        viewModel.SetAllActions(ConflictResolutionAction.KeepLocal);
        Assert.Equal(ConflictResolutionAction.Skip, viewModel.ConflictFiles.Single(row => row.Filename == "remote").Action);
        Assert.Equal(ConflictResolutionAction.KeepLocal, viewModel.ConflictFiles.Single(row => row.Filename == "both").Action);
        Assert.False(viewModel.ConflictFiles.Single(row => row.Filename == "local").CanKeepRemote);
        Assert.False(viewModel.ConflictFiles.Single(row => row.Filename == "remote").CanKeepLocal);
        viewModel.SetAction("local", ConflictResolutionAction.KeepRemote);
        Assert.Equal(ConflictResolutionAction.Skip, viewModel.ConflictFiles.Single(row => row.Filename == "local").Action);
    }

    [Fact]
    public async Task ConflictView_ReportsOneSidedUpdatesAsDifferences()
    {
        using var fixture = await Fixture.CreateAsync();
        var viewModel = fixture.Conflicts();
        viewModel.LoadFrom(new ConflictNavigationContext(Guid.NewGuid(), "Game", new SyncComparison
        {
            Entries =
            [
                new SyncDiffEntry { RelativePath = "local-update", ChangeKind = SyncChangeKind.UpdatedLocally, LocalHash = "local", RemoteHash = "remote" },
                new SyncDiffEntry { RelativePath = "remote-update", ChangeKind = SyncChangeKind.UpdatedRemotely, LocalHash = "local", RemoteHash = "remote" },
            ],
        }));

        Assert.Equal("Save differences found", viewModel.WarningTitle);
        Assert.Contains("1 local changes, 1 remote changes", viewModel.WarningMessage);
        Assert.Equal("Updated locally", viewModel.ConflictFiles.Single(row => row.Filename == "local-update").LocalStatus);
        Assert.Equal("Updated remotely", viewModel.ConflictFiles.Single(row => row.Filename == "remote-update").RemoteStatus);
        Assert.All(viewModel.ConflictFiles, row => Assert.Equal(ConflictResolutionAction.Undecided, row.Action));
    }

    [Fact]
    public async Task ConflictView_OneSidedNewFilesRequireExplicitChoice()
    {
        using var fixture = await Fixture.CreateAsync();
        var viewModel = fixture.Conflicts();
        viewModel.LoadFrom(new ConflictNavigationContext(Guid.NewGuid(), "Game", new SyncComparison
        {
            Entries =
            [
                new SyncDiffEntry { RelativePath = "local", ChangeKind = SyncChangeKind.AddedLocally, LocalHash = "local" },
                new SyncDiffEntry { RelativePath = "remote", ChangeKind = SyncChangeKind.AddedRemotely, RemoteHash = "remote" },
            ],
        }));
        Assert.All(viewModel.ConflictFiles, row => Assert.Equal(ConflictResolutionAction.Undecided, row.Action));
        viewModel.SetAction("local", ConflictResolutionAction.KeepLocal);
        Assert.Equal(ConflictResolutionAction.KeepLocal, viewModel.ConflictFiles.Single(row => row.Filename == "local").Action);
        Assert.Equal(ConflictResolutionAction.Undecided, viewModel.ConflictFiles.Single(row => row.Filename == "remote").Action);
    }

    [Fact]
    public async Task Library_OffersFirstRunAndNoFilterResultsSeparately()
    {
        using var fixture = await Fixture.CreateAsync();
        var library = fixture.Library();
        await library.LoadAsync();
        Assert.True(library.IsEmptyState);
        Assert.False(library.ShowNoMatches);
        await AddHistoryGameAsync(fixture, "Visible Game", DateTimeOffset.UtcNow);
        await library.LoadAsync();
        library.SearchText = "not present";
        Assert.False(library.IsEmptyState);
        Assert.True(library.ShowNoMatches);
        library.SearchText = "Visible";
        Assert.False(library.ShowNoMatches);
        await library.StopStatusCheckAsync();
    }

    [Fact]
    public async Task Library_KeepsContentConflictsRegardlessOfModificationTimes()
    {
        using var fixture = await Fixture.CreateAsync();
        var saves = fixture.Folder("conflicting-saves");
        var file = Path.Combine(saves, "slot.sav");
        await File.WriteAllTextAsync(file, "first");
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Conflict Game", saves);
        Assert.Null(await wizard.CreateProfileAsync());
        await fixture.UploadAsync(wizard.LastCreatedInstanceId!.Value);
        await File.WriteAllTextAsync(file, "different local save");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(1));

        var library = fixture.Library();
        await library.LoadAsync();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (library.Games.Single().LocalStatus == "Checking…" && DateTime.UtcNow < deadline) await Task.Delay(10);
        var row = Assert.Single(library.Games);
        Assert.Equal("Conflict", row.LocalStatus);
        Assert.Equal("Conflict", row.RemoteStatus);
        Assert.True(library.HasConflicts);
        Assert.Equal(1, library.ConflictCount);
        library.SearchText = "not present";
        Assert.True(library.HasConflicts);
        await library.StopStatusCheckAsync();
    }

    [Fact]
    public async Task Details_RestoresTheSelectedSnapshotAndRejectsUnknownSnapshots()
    {
        using var fixture = await Fixture.CreateAsync();
        var gameId = await AddHistoryGameAsync(fixture, "Snapshot Game", DateTimeOffset.UtcNow);
        var older = new Snapshot { GameInstanceId = gameId, ArchivePath = "older.zip", CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1) };
        await fixture.History.AddSnapshotAsync(older, CancellationToken.None);
        var engine = new RecordingRestoreEngine();
        var details = fixture.Details(engine: engine);
        await details.LoadAsync(gameId);
        var selected = details.Snapshots.Single(row => row.SnapshotId == older.Id);

        Assert.Null(await details.RestoreSnapshotAsync(selected));
        Assert.Equal(older.Id, engine.RestoredId);
        var unknown = selected with { SnapshotId = Guid.NewGuid() };
        Assert.NotNull(await details.RestoreSnapshotAsync(unknown));
        Assert.Equal(older.Id, engine.RestoredId);
        await details.StopStatusCheckAsync();
    }

    [Fact]
    public async Task NormalUploadEntries_QueueJobsWithRemoteChangeProtection()
    {
        using var fixture = await Fixture.CreateAsync();
        var saves = fixture.Folder("protected-saves");
        await File.WriteAllTextAsync(Path.Combine(saves, "slot.sav"), "save");
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Protected Game", saves);
        Assert.Null(await wizard.CreateProfileAsync());
        var gameId = wizard.LastCreatedInstanceId!.Value;

        var library = fixture.Library();
        Assert.Null(await library.QueueSyncForGameAsync(gameId, SyncDirection.Upload, "Protected Game"));
        var details = fixture.Details();
        await details.LoadAsync(gameId);
        Assert.Null(await details.QueueSyncAsync(SyncDirection.Upload));
        var (count, error) = await library.SyncAllAsync();
        Assert.Equal(1, count);
        Assert.Null(error);
        Assert.Equal(3, fixture.Queue.PendingJobs.Count);
        Assert.All(fixture.Queue.PendingJobs, job => Assert.True(job.ProtectRemoteChanges));
        await details.StopStatusCheckAsync();
    }

    [Fact]
    public async Task BulkUpload_ReportsPartialSubmissionFailure()
    {
        using var fixture = await Fixture.CreateAsync();
        await AddHistoryGameAsync(fixture, "First Game", DateTimeOffset.UtcNow);
        await AddHistoryGameAsync(fixture, "Second Game", DateTimeOffset.UtcNow);
        var library = fixture.Library(new FailingQueueEngine());

        var (count, error) = await library.SyncAllAsync();

        Assert.Equal(1, count);
        Assert.Equal("queue unavailable", error);
    }

    [Fact]
    public async Task TargetChecks_AreSharedAndDiscardedAfterConfigurationChanges()
    {
        using var fixture = await Fixture.CreateAsync();
        var store = fixture.TargetStore;
        var target = store.GetDefault()!;
        var firstStart = DateTimeOffset.UtcNow;
        store.SetConnectionCheck(target, true, null, firstStart);
        var viewModel = fixture.Targets();
        await viewModel.LoadAsync();
        Assert.Equal("Connected", viewModel.SelectedTargetStatus);

        store.SetConnectionCheck(target, false, "unreachable", firstStart.AddSeconds(1));
        store.SetConnectionCheck(target, true, null, firstStart);
        await viewModel.LoadAsync();
        Assert.Equal("Failed", viewModel.SelectedTargetStatus);
        Assert.Equal("unreachable", viewModel.FailureMessage);

        target.Name = "Updated target";
        store.Upsert(target);
        store.SetConnectionCheck(target, true, null, firstStart.AddSeconds(2));
        await viewModel.LoadAsync();
        Assert.Equal("Untested", viewModel.SelectedTargetStatus);
    }

    [Fact]
    public async Task AddedContent_PersistsAcrossReload_StaysEnabled_AndIsActuallyUploaded()
    {
        using var fixture = await Fixture.CreateAsync();
        var savePath = fixture.Folder("saves");
        var extraPath = fixture.Folder("extra");
        await File.WriteAllTextAsync(Path.Combine(savePath, "slot.sav"), "save");
        await File.WriteAllTextAsync(Path.Combine(extraPath, "extra.sav"), "extra");
        var definition = new GameContentDefinition
        {
            GameId = "manual-game", DisplayName = "Manual Game", ContentItems = new[]
            {
                new ContentItem { ContentId = "saves", Category = ContentCategory.Save, PathTemplates = new[] { savePath } },
                new ContentItem { ContentId = "disabled", Category = ContentCategory.Config, PathTemplates = new[] { fixture.Folder("disabled") }, DefaultEnabled = false },
            },
        };
        await fixture.UserStore.SaveDefinitionAsync(definition, CancellationToken.None);
        var instance = new GameInstance { GameId = definition.GameId, DisplayName = definition.DisplayName, SourceProviderId = "custom" };
        await fixture.Games.UpsertAsync(instance, CancellationToken.None);
        var binding = new StorageBinding { GameInstanceId = instance.Id, StorageProviderId = "local-folder", RemoteNamespace = "legacy-namespace",
            Settings = new Dictionary<string, string> { ["rootPath"] = fixture.StoragePath }, ContentSelections = new[]
            {
                new ContentSelection { ContentId = "saves", IsEnabled = true },
                new ContentSelection { ContentId = "disabled", IsEnabled = false },
            } };
        await fixture.Bindings.UpsertAsync(binding, CancellationToken.None);
        var details = fixture.Details();
        await details.LoadAsync(instance.Id);
        Assert.Null(await details.AddCustomContentItemAsync("Saves", extraPath));

        var reloaded = fixture.Details();
        await reloaded.LoadAsync(instance.Id);
        Assert.Contains(reloaded.ContentItems, item => item.Name == "saves-2" && item.Enabled);
        Assert.Contains(reloaded.ContentItems, item => item.Name == "disabled" && !item.Enabled);
        Assert.Equal(3, (await fixture.Manifest.GetDefinitionAsync(instance.GameId, CancellationToken.None))!.ContentItems.Count);
        Assert.Equal("legacy-namespace", (await fixture.Bindings.GetAsync(binding.Id, CancellationToken.None))!.RemoteNamespace);
        await fixture.UploadAsync(instance.Id);
        var remote = await fixture.OpenRemoteAsync("legacy-namespace");
        using var save = await remote.DownloadAsync("legacy-namespace/saves/slot.sav", CancellationToken.None);
        using var extra = await remote.DownloadAsync("legacy-namespace/saves-2/extra.sav", CancellationToken.None);
        Assert.Equal("save", await new StreamReader(save.Content).ReadToEndAsync());
        Assert.Equal("extra", await new StreamReader(extra.Content).ReadToEndAsync());
        await details.StopStatusCheckAsync();
        await reloaded.StopStatusCheckAsync();
    }

    [Fact]
    public async Task AddingContent_WaitsForQueuedSyncWithoutChangingConfiguration()
    {
        using var fixture = await Fixture.CreateAsync();
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Queued Game", fixture.Folder("queued-saves"));
        Assert.Null(await wizard.CreateProfileAsync());
        var game = Assert.Single(await fixture.Games.ListAsync(CancellationToken.None));
        var details = fixture.Details();
        await details.LoadAsync(game.Id);
        await details.StopStatusCheckAsync();
        var original = Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(game.Id, CancellationToken.None));
        var definition = (await fixture.Manifest.GetDefinitionAsync(game.GameId, CancellationToken.None))!;
        var job = new SyncJob { GameInstanceId = game.Id, Direction = SyncDirection.Upload };
        await fixture.Engine.QueueAsync(job, CancellationToken.None);

        var error = await details.AddCustomContentItemAsync("Extra", fixture.Folder("queued-extra"));

        Assert.Contains("sync tasks", error);
        Assert.Equal(original.UpdatedAtUtc, (await fixture.Bindings.GetAsync(original.Id, CancellationToken.None))!.UpdatedAtUtc);
        Assert.Equal(definition.ContentItems.Count, (await fixture.Manifest.GetDefinitionAsync(game.GameId, CancellationToken.None))!.ContentItems.Count);
        Assert.True(fixture.Queue.Remove(job.Id));
        await details.StopStatusCheckAsync();
    }

    [Fact]
    public async Task AddingContent_RestoresBindingWhenDefinitionSaveFails()
    {
        using var fixture = await Fixture.CreateAsync();
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Rollback Game", fixture.Folder("rollback-saves"));
        Assert.Null(await wizard.CreateProfileAsync());
        var game = Assert.Single(await fixture.Games.ListAsync(CancellationToken.None));
        var details = fixture.Details(definitionStore: new FailingDefinitionStore(fixture.UserStore));
        await details.LoadAsync(game.Id);
        await details.StopStatusCheckAsync();
        var original = Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(game.Id, CancellationToken.None));
        var definition = (await fixture.Manifest.GetDefinitionAsync(game.GameId, CancellationToken.None))!;

        var error = await details.AddCustomContentItemAsync("Extra", fixture.Folder("rollback-extra"));

        Assert.Contains("definition save failed", error);
        var restored = (await fixture.Bindings.GetAsync(original.Id, CancellationToken.None))!;
        Assert.Equal(original.UpdatedAtUtc, restored.UpdatedAtUtc);
        Assert.Equal(original.ContentSelections.Select(x => x.ContentId), restored.ContentSelections.Select(x => x.ContentId));
        Assert.Equal(definition.ContentItems.Count, (await fixture.Manifest.GetDefinitionAsync(game.GameId, CancellationToken.None))!.ContentItems.Count);
        await details.StopStatusCheckAsync();
    }

    [Fact]
    public async Task AddingContent_CompletesBeforeAConcurrentQueuedUploadReadsIt()
    {
        using var fixture = await Fixture.CreateAsync();
        var wizard = fixture.Wizard();
        var savePath = fixture.Folder("concurrent-saves");
        await File.WriteAllTextAsync(Path.Combine(savePath, "slot.sav"), "save");
        PrepareManual(wizard, "Concurrent Game", savePath);
        Assert.Null(await wizard.CreateProfileAsync());
        var game = Assert.Single(await fixture.Games.ListAsync(CancellationToken.None));
        var extraPath = fixture.Folder("concurrent-extra");
        await File.WriteAllTextAsync(Path.Combine(extraPath, "extra.sav"), "extra");
        var delayedStore = new DelayedDefinitionStore(fixture.UserStore);
        var details = fixture.Details(definitionStore: delayedStore);
        await details.LoadAsync(game.Id);
        await details.StopStatusCheckAsync();
        var add = details.AddCustomContentItemAsync("Extra", extraPath);
        using var cts = new CancellationTokenSource();
        Task? worker = null;
        try
        {
            await delayedStore.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var job = new SyncJob { GameInstanceId = game.Id, Direction = SyncDirection.Upload };
            await fixture.Engine.QueueAsync(job, CancellationToken.None);
            worker = fixture.Engine.ProcessQueuedJobsAsync(cts.Token);
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (job.Status == SyncJobStatus.Queued && DateTime.UtcNow < deadline) await Task.Delay(10);
            Assert.Equal(SyncJobStatus.Running, job.Status);

            delayedStore.Release.TrySetResult();
            Assert.Null(await add);
            while (job.Status is SyncJobStatus.Queued or SyncJobStatus.Running && DateTime.UtcNow < deadline) await Task.Delay(10);
            var records = await fixture.History.ListRecordsAsync(game.Id, CancellationToken.None);
            Assert.True(job.Status == SyncJobStatus.Completed, string.Join("; ", records.Select(record => record.ErrorMessage)));
            var binding = Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(game.Id, CancellationToken.None));
            var remote = await fixture.OpenRemoteAsync(binding.RemoteNamespace);
            using var extra = await remote.DownloadAsync(binding.RemoteNamespace + "/extra/extra.sav", CancellationToken.None);
            Assert.Equal("extra", await new StreamReader(extra.Content).ReadToEndAsync());
        }
        finally
        {
            delayedStore.Release.TrySetResult();
            await add;
            cts.Cancel();
            if (worker is not null) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
            await details.StopStatusCheckAsync();
        }
    }

    [Fact]
    public async Task AddingContent_DoesNotOverwriteNewerBinding()
    {
        using var fixture = await Fixture.CreateAsync();
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Changed Game", fixture.Folder("changed-saves"));
        Assert.Null(await wizard.CreateProfileAsync());
        var game = Assert.Single(await fixture.Games.ListAsync(CancellationToken.None));
        var stale = fixture.Details();
        var current = fixture.Details();
        await stale.LoadAsync(game.Id);
        await current.LoadAsync(game.Id);
        Assert.Null(await current.UpdateContentSelectionAsync(Assert.Single(current.ContentItems), false));
        var changed = Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(game.Id, CancellationToken.None));

        var error = await stale.AddCustomContentItemAsync("Extra", fixture.Folder("changed-extra"));

        Assert.Contains("configuration changed", error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(changed.UpdatedAtUtc, (await fixture.Bindings.GetAsync(changed.Id, CancellationToken.None))!.UpdatedAtUtc);
        Assert.Single((await fixture.Manifest.GetDefinitionAsync(game.GameId, CancellationToken.None))!.ContentItems);
        await stale.StopStatusCheckAsync();
        await current.StopStatusCheckAsync();
    }

    [Fact]
    public async Task ContentPathsCanBeDisabledReenabledAndRemovedWithoutDeletingFiles()
    {
        using var fixture = await Fixture.CreateAsync();
        var first = fixture.Folder("first-content");
        var second = fixture.Folder("second-content");
        await File.WriteAllTextAsync(Path.Combine(first, "one.sav"), "one");
        await File.WriteAllTextAsync(Path.Combine(second, "two.sav"), "two");
        var definition = new GameContentDefinition { GameId = "content-test", DisplayName = "Content Test", ContentItems = new[]
        {
            new ContentItem { ContentId = "saves", Category = ContentCategory.Save, PathTemplates = new[] { first, second } },
        } };
        await fixture.UserStore.SaveDefinitionAsync(definition, CancellationToken.None);
        var instance = new GameInstance { GameId = definition.GameId, DisplayName = definition.DisplayName, SourceProviderId = "custom" };
        await fixture.Games.UpsertAsync(instance, CancellationToken.None);
        await fixture.Bindings.UpsertAsync(new StorageBinding { GameInstanceId = instance.Id, StorageProviderId = "local-folder",
            RemoteNamespace = "content-test", Settings = new Dictionary<string, string> { ["rootPath"] = fixture.StoragePath } }, CancellationToken.None);
        var details = fixture.Details();
        await details.LoadAsync(instance.Id);
        var firstRow = Assert.Single(details.ContentItems, row => row.PathTemplate == first);
        Assert.Null(await details.UpdateContentSelectionAsync(firstRow, false));
        Assert.False(Assert.Single(details.ContentItems, row => row.PathTemplate == first).Enabled);
        var disabledComparison = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = instance.Id, Direction = SyncDirection.Compare }, CancellationToken.None);
        Assert.Single(disabledComparison.Entries);
        Assert.Null(await details.UpdateContentSelectionAsync(Assert.Single(details.ContentItems, row => row.PathTemplate == first), true));
        Assert.True(Assert.Single(details.ContentItems, row => row.PathTemplate == first).Enabled);
        Assert.Null(await details.UpdateContentSelectionAsync(Assert.Single(details.ContentItems, row => row.PathTemplate == first), null));
        Assert.Single(details.ContentItems);
        Assert.True(File.Exists(Path.Combine(first, "one.sav")));
        var reloaded = fixture.Details();
        await reloaded.LoadAsync(instance.Id);
        Assert.Single(reloaded.ContentItems);
        var remaining = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = instance.Id, Direction = SyncDirection.Compare }, CancellationToken.None);
        Assert.Single(remaining.Entries);
        Assert.EndsWith("two.sav", remaining.Entries[0].RelativePath);
        await details.StopStatusCheckAsync();
        await reloaded.StopStatusCheckAsync();
    }

    [Fact]
    public async Task MultipleUploadTargetsQueueSeparateProtectedJobsWithoutChangingPrimaryBinding()
    {
        using var fixture = await Fixture.CreateAsync();
        var local = fixture.Folder("multi-target-save");
        await File.WriteAllTextAsync(Path.Combine(local, "slot.sav"), "save");
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Multi Target Game", local);
        Assert.Null(await wizard.CreateProfileAsync());
        var id = wizard.LastCreatedInstanceId!.Value;
        var details = fixture.Details();
        await details.LoadAsync(id);
        var primary = details.CurrentTargetId!.Value;
        var other = new SyncTargetConfig { Name = "Second", ProviderId = "local-folder", Settings = new() { ["rootPath"] = fixture.Folder("second-target") } };
        fixture.TargetStore.Upsert(other);
        var before = Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(id, CancellationToken.None));
        Assert.Null(await details.QueueSyncAsync(SyncDirection.Upload, targetIds: new[] { primary, other.Id }));
        var jobs = fixture.Queue.PendingJobs.ToArray();
        Assert.Equal(new[] { primary, other.Id }, jobs.Select(job => job.TargetId!.Value));
        Assert.All(jobs, job => Assert.True(job.ProtectRemoteChanges));
        using var cancellation = new CancellationTokenSource();
        var worker = fixture.Engine.ProcessQueuedJobsAsync(cancellation.Token);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (jobs.Any(job => job.Status is SyncJobStatus.Queued or SyncJobStatus.Running))
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(10);
        }
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
        Assert.All(jobs, job => Assert.Equal(SyncJobStatus.Completed, job.Status));
        var firstRemote = await fixture.OpenRemoteAsync(before.RemoteNamespace);
        var firstPath = Assert.Single(await firstRemote.ListAsync(before.RemoteNamespace, CancellationToken.None), entry => entry.Path.EndsWith("/slot.sav", StringComparison.Ordinal)).Path;
        using var firstSave = await firstRemote.DownloadAsync(firstPath, CancellationToken.None);
        var secondRemote = await RemoteSaveSession.OpenAsync(new LocalFolderStorageProvider(Microsoft.Extensions.Options.Options.Create(
            new LocalFolderOptions { RootPath = other.Settings["rootPath"] })), before.RemoteNamespace, CancellationToken.None);
        var secondPath = Assert.Single(await secondRemote.ListAsync(before.RemoteNamespace, CancellationToken.None), entry => entry.Path.EndsWith("/slot.sav", StringComparison.Ordinal)).Path;
        using var secondSave = await secondRemote.DownloadAsync(secondPath, CancellationToken.None);
        Assert.Equal("save", await new StreamReader(firstSave.Content).ReadToEndAsync());
        Assert.Equal("save", await new StreamReader(secondSave.Content).ReadToEndAsync());
        Assert.Equal(before.Id, Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(id, CancellationToken.None)).Id);
        await details.StopStatusCheckAsync();
    }

    [Fact]
    public async Task RenamedSingleFileShowsOldRemoteFileAndCanRemoveItSafely()
    {
        using var fixture = await Fixture.CreateAsync();
        var folder = fixture.Folder("renamed-file");
        var oldPath = Path.Combine(folder, "old.sav");
        var newPath = Path.Combine(folder, "new.sav");
        await File.WriteAllTextAsync(oldPath, "original");
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Renamed File", oldPath);
        Assert.Null(await wizard.CreateProfileAsync());
        var id = wizard.LastCreatedInstanceId!.Value;
        await fixture.UploadAsync(id);
        File.Move(oldPath, newPath);
        var details = fixture.Details();
        await details.LoadAsync(id);
        Assert.Null(await details.UpdateContentPathAsync(Assert.Single(details.ContentItems), newPath));
        var comparison = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = id, Direction = SyncDirection.Compare }, CancellationToken.None);
        var remoteOld = Assert.Single(comparison.Entries, entry => entry.RelativePath.EndsWith("/old.sav", StringComparison.Ordinal));
        Assert.Null(remoteOld.LocalHash);
        var localNew = Assert.Single(comparison.Entries, entry => entry.RelativePath.EndsWith("/new.sav", StringComparison.Ordinal));
        await fixture.Engine.ResolveConflictsAsync(new ConflictResolutionPlan { GameInstanceId = id, ExpectedConfigurationFingerprint = comparison.ConfigurationFingerprint,
            Decisions = new[]
            {
                new ConflictResolutionDecision { RelativePath = remoteOld.RelativePath, Action = ConflictResolutionAction.RemoveRemote, VerifyComparedVersions = true, ExpectedRemoteHash = remoteOld.RemoteHash },
                new ConflictResolutionDecision { RelativePath = localNew.RelativePath, Action = ConflictResolutionAction.KeepLocal, VerifyComparedVersions = true, ExpectedLocalHash = localNew.LocalHash },
            } }, CancellationToken.None);
        var after = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = id, Direction = SyncDirection.Compare }, CancellationToken.None);
        Assert.Equal(SyncChangeKind.Unchanged, Assert.Single(after.Entries).ChangeKind);
        Assert.Equal("original", await File.ReadAllTextAsync(newPath));
        await details.StopStatusCheckAsync();
    }

    [Fact]
    public async Task QueuedTargetChangeIsRejectedBeforeWritingAnyFiles()
    {
        using var fixture = await Fixture.CreateAsync();
        var local = fixture.Folder("target-change-save");
        await File.WriteAllTextAsync(Path.Combine(local, "slot.sav"), "save");
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Target Change", local);
        Assert.Null(await wizard.CreateProfileAsync());
        var id = wizard.LastCreatedInstanceId!.Value;
        var target = new SyncTargetConfig { Name = "Target", ProviderId = "local-folder", Settings = new() { ["rootPath"] = fixture.Folder("target-before") } };
        fixture.TargetStore.Upsert(target);
        var job = new SyncJob { GameInstanceId = id, Direction = SyncDirection.Upload, TargetId = target.Id, ProtectRemoteChanges = true };
        await fixture.Engine.QueueAsync(job, CancellationToken.None);
        target.Settings["rootPath"] = fixture.Folder("target-after");
        fixture.TargetStore.Upsert(target);
        using var cancellation = new CancellationTokenSource();
        var worker = fixture.Engine.ProcessQueuedJobsAsync(cancellation.Token);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (job.Status is SyncJobStatus.Queued or SyncJobStatus.Running)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(10);
        }
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
        Assert.Equal(SyncJobStatus.Failed, job.Status);
        Assert.Empty(Directory.EnumerateFiles(target.Settings["rootPath"], "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task SelectingDifferentTargetsComparesAndResolvesTheSelectedTargetOnly()
    {
        using var fixture = await Fixture.CreateAsync();
        var local = fixture.Folder("different-target-save");
        await File.WriteAllTextAsync(Path.Combine(local, "slot.sav"), "local");
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Different Targets", local);
        Assert.Null(await wizard.CreateProfileAsync());
        var id = wizard.LastCreatedInstanceId!.Value;
        var original = Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(id, CancellationToken.None));
        var second = new SyncTargetConfig { Name = "Second", ProviderId = "local-folder", Settings = new() { ["rootPath"] = fixture.Folder("different-target-remote") } };
        fixture.TargetStore.Upsert(second);
        var provider = new LocalFolderStorageProvider(Microsoft.Extensions.Options.Options.Create(new LocalFolderOptions { RootPath = second.Settings["rootPath"] }));
        var remote = await RemoteSaveSession.OpenAsync(provider, original.RemoteNamespace, CancellationToken.None);
        using (var payload = new MemoryStream("remote"u8.ToArray()))
            await remote.UploadAsync(original.RemoteNamespace + "/saves/slot.sav", payload, CancellationToken.None);
        await remote.CommitAsync(CancellationToken.None);
        var details = fixture.Details();
        await details.LoadAsync(id);
        var (context, error) = await details.CompareAsync(targetId: second.Id);
        Assert.Null(error);
        Assert.NotNull(context);
        var conflicts = fixture.Conflicts();
        conflicts.LoadFrom(context!);
        Assert.Contains(conflicts.ConflictFiles, row => row.RemoteStatus != "Missing remotely");
        conflicts.SetAllActions(ConflictResolutionAction.KeepRemote);
        Assert.Null(await conflicts.ResolveAsync());
        var job = Assert.Single(fixture.Queue.PendingJobs);
        Assert.Equal(second.Id, job.TargetId);
        using var cancellation = new CancellationTokenSource();
        var worker = fixture.Engine.ProcessQueuedJobsAsync(cancellation.Token);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (job.Status is SyncJobStatus.Queued or SyncJobStatus.Running)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(10);
        }
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
        Assert.Equal(SyncJobStatus.Completed, job.Status);
        Assert.Equal("remote", await File.ReadAllTextAsync(Path.Combine(local, "slot.sav")));
        Assert.Equal(original.Id, Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(id, CancellationToken.None)).Id);
        await details.StopStatusCheckAsync();
    }

    [Fact]
    public async Task DetailsEditing_PreservesGameIdentityAndChangesContentPathWithoutMovingFiles()
    {
        using var fixture = await Fixture.CreateAsync();
        var original = fixture.Folder("original-save");
        var replacement = fixture.Folder("replacement-save");
        await File.WriteAllTextAsync(Path.Combine(original, "slot.sav"), "old");
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Identity Game", original);
        Assert.Null(await wizard.CreateProfileAsync());
        var id = wizard.LastCreatedInstanceId!.Value;
        var before = (await fixture.Games.GetAsync(id, CancellationToken.None))!;
        var binding = Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(id, CancellationToken.None));
        var details = fixture.Details();
        await details.LoadAsync(id);
        var comparedBefore = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = id, Direction = SyncDirection.Compare }, CancellationToken.None);
        Assert.Null(await details.UpdateGameAsync("Renamed Game", replacement, new Dictionary<string, string> { ["SAVE_ROOT"] = replacement }));
        var updated = (await fixture.Games.GetAsync(id, CancellationToken.None))!;
        Assert.Equal(before.GameId, updated.GameId);
        Assert.Equal(before.SourceProviderId, updated.SourceProviderId);
        Assert.Equal(before.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.Equal("Renamed Game", updated.DisplayName);
        Assert.Equal(replacement, updated.Variables["SAVE_ROOT"]);
        var item = Assert.Single(details.ContentItems);
        Assert.Null(await details.UpdateContentPathAsync(item, replacement));
        var reloaded = fixture.Details();
        await reloaded.LoadAsync(id);
        Assert.Equal(original.Replace('\\', '/'), Assert.Single(reloaded.ContentItems).PathTemplate);
        Assert.Equal(replacement.Replace('\\', '/'), Assert.Single(reloaded.ContentItems).ResolvedPath);
        Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(original, "slot.sav")));
        Assert.Equal(binding.RemoteNamespace, (await fixture.Bindings.GetAsync(binding.Id, CancellationToken.None))!.RemoteNamespace);
        var comparedAfter = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = id, Direction = SyncDirection.Compare }, CancellationToken.None);
        Assert.NotEqual(comparedBefore.ConfigurationFingerprint, comparedAfter.ConfigurationFingerprint);
        await details.StopStatusCheckAsync(); await reloaded.StopStatusCheckAsync();
    }

    [Fact]
    public async Task ExistingGame_ChangesDestinationWithoutMovingFilesAndRequiresNewBaseline()
    {
        using var fixture = await Fixture.CreateAsync();
        var local = fixture.Folder("destination-save");
        await File.WriteAllTextAsync(Path.Combine(local, "slot.sav"), "common");
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Destination Game", local);
        Assert.Null(await wizard.CreateProfileAsync());
        var gameId = wizard.LastCreatedInstanceId!.Value;
        await fixture.UploadAsync(gameId);
        var details = fixture.Details();
        await details.LoadAsync(gameId);
        var original = Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(gameId, CancellationToken.None));
        var originalTarget = details.CurrentTargetId!.Value;
        var comparison = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = gameId, Direction = SyncDirection.Compare }, CancellationToken.None);
        var baseline = AutoPullPlanner.ConfirmBaseline(gameId, comparison);
        Assert.Null(await details.UpdateDestinationAsync(originalTarget, original.RemoteNamespace));
        Assert.Equal(original.UpdatedAtUtc, (await fixture.Bindings.GetAsync(original.Id, CancellationToken.None))!.UpdatedAtUtc);
        var otherRoot = fixture.Folder("other-remote");
        var other = new SyncTargetConfig { Name = "Other remote", ProviderId = "local-folder", Settings = new() { ["rootPath"] = otherRoot } };
        fixture.TargetStore.Upsert(other);
        Assert.Null(await details.UpdateDestinationAsync(other.Id, original.RemoteNamespace));
        var changed = Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(gameId, CancellationToken.None));
        Assert.Equal(original.Id, changed.Id);
        Assert.Equal(original.CreatedAtUtc, changed.CreatedAtUtc);
        Assert.Equal(original.RemoteNamespace, changed.RemoteNamespace);
        Assert.Equal(original.ContentSelections.Select(item => (item.ContentId, item.IsEnabled)), changed.ContentSelections.Select(item => (item.ContentId, item.IsEnabled)));
        Assert.Equal(other.Id.ToString("D"), changed.Settings["targetId"]);
        Assert.Equal(otherRoot, changed.Settings["rootPath"]);
        Assert.Equal("Other remote", details.ActiveTarget);
        Assert.Equal("common", await File.ReadAllTextAsync(Path.Combine(local, "slot.sav")));
        var oldRemote = await fixture.OpenRemoteAsync(original.RemoteNamespace);
        using var stored = await oldRemote.DownloadAsync(original.RemoteNamespace + "/saves/slot.sav", CancellationToken.None);
        Assert.Equal("common", await new StreamReader(stored.Content).ReadToEndAsync());
        Assert.Empty(Directory.EnumerateFileSystemEntries(otherRoot));
        var newComparison = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = gameId, Direction = SyncDirection.Compare }, CancellationToken.None);
        Assert.Equal(AutoPullDisposition.Review, AutoPullPlanner.Evaluate(gameId, newComparison, baseline).Disposition);
        Assert.Null(await details.UpdateDestinationAsync(originalTarget, original.RemoteNamespace));
        var returned = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = gameId, Direction = SyncDirection.Compare }, CancellationToken.None);
        Assert.Equal(AutoPullDisposition.Review, AutoPullPlanner.Evaluate(gameId, returned, baseline).Disposition);
        await WaitForStatusAsync(details, "Synced");
        await details.StopStatusCheckAsync();
    }

    [Fact]
    public async Task ExistingGame_DestinationValidationAndQueuedJobsProtectBinding()
    {
        using var fixture = await Fixture.CreateAsync();
        var local = fixture.Folder("queued-destination-save");
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Queued Destination", local);
        Assert.Null(await wizard.CreateProfileAsync());
        var id = wizard.LastCreatedInstanceId!.Value;
        var details = fixture.Details();
        await details.LoadAsync(id);
        var original = Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(id, CancellationToken.None));
        var other = new SyncTargetConfig { Name = "Other", ProviderId = "local-folder", Settings = new() { ["rootPath"] = fixture.Folder("queued-other") } };
        fixture.TargetStore.Upsert(other);
        foreach (var invalid in new[] { "", "../outside", "/absolute", "foo//bar", "foo/__gsync_meta" })
            Assert.NotNull(await details.UpdateDestinationAsync(other.Id, invalid));
        var queued = new SyncJob { GameInstanceId = id, Direction = SyncDirection.Upload };
        await fixture.Engine.QueueAsync(queued, CancellationToken.None);
        Assert.Contains("queued", (await details.UpdateDestinationAsync(other.Id, "custom/new"))!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(original.Settings["targetId"], (await fixture.Bindings.GetAsync(original.Id, CancellationToken.None))!.Settings["targetId"]);
        Assert.True(fixture.Queue.Remove(queued.Id));
        Assert.Null(await details.UpdateDestinationAsync(other.Id, "custom/new"));
        Assert.Equal("custom/new", (await fixture.Bindings.GetAsync(original.Id, CancellationToken.None))!.RemoteNamespace);
        await WaitForStatusAsync(details, "No files");
        await details.StopStatusCheckAsync();
    }

    [Fact]
    public async Task ExistingGame_WithoutBindingCanChooseItsFirstTarget()
    {
        using var fixture = await Fixture.CreateAsync();
        var local = fixture.Folder("unbound-destination-save");
        await File.WriteAllTextAsync(Path.Combine(local, "slot.sav"), "local");
        var game = new GameInstance { GameId = "unbound-game", DisplayName = "Unbound", SourceProviderId = "custom", InstallDirectory = local };
        await fixture.Games.UpsertAsync(game, CancellationToken.None);
        await fixture.UserStore.SaveDefinitionAsync(new GameContentDefinition { GameId = game.GameId, DisplayName = game.DisplayName, ContentItems = new[]
        {
            new ContentItem { ContentId = "saves", Category = ContentCategory.Save, PathTemplates = new[] { "%GAME_INSTALL_DIR%" } }
        } }, CancellationToken.None);
        var details = fixture.Details();
        await details.LoadAsync(game.Id);
        Assert.Null(details.CurrentTargetId);
        var target = Assert.Single(details.GetTargetOptions());
        var remoteNamespace = details.CurrentRemoteNamespace;
        Assert.Null(await details.UpdateDestinationAsync(target.Id, remoteNamespace));
        var binding = Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(game.Id, CancellationToken.None));
        Assert.Equal(target.Id.ToString("D"), binding.Settings["targetId"]);
        Assert.Equal(remoteNamespace, binding.RemoteNamespace);
        Assert.Empty(binding.ContentSelections);
        Assert.True(Assert.Single(details.ContentItems).Enabled);
        await WaitForStatusAsync(details, "Differences");
        await details.StopStatusCheckAsync();
    }

    [Fact]
    public async Task ExistingGame_CanBindWebDavWithoutEmbeddingPlaintextCredentials()
    {
        using var fixture = await Fixture.CreateAsync();
        var local = fixture.Folder("webdav-destination-save");
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "WebDav Destination", local);
        Assert.Null(await wizard.CreateProfileAsync());
        var id = wizard.LastCreatedInstanceId!.Value;
        var details = fixture.Details();
        await details.LoadAsync(id);
        var target = new SyncTargetConfig { Name = "Remote WebDAV", ProviderId = "webdav", Settings = new()
        {
            ["baseUrl"] = "https://example.test/dav", ["username"] = "user", ["password"] = "secret",
        } };
        fixture.TargetStore.Upsert(target);
        Assert.Null(await details.UpdateDestinationAsync(target.Id, details.CurrentRemoteNamespace));
        var binding = Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(id, CancellationToken.None));
        Assert.Equal("webdav", binding.StorageProviderId);
        Assert.Equal("https://example.test/dav", binding.Settings["baseUrl"]);
        Assert.True(binding.Settings.ContainsKey(CredentialSettingsService.ReferenceKey));
        Assert.False(binding.Settings.ContainsKey("password"));
        Assert.False(binding.Settings.ContainsKey("username"));
        Assert.False(binding.Settings.ContainsKey("rootPath"));
        await WaitForStatusAsync(details, "Compare failed");
        await details.StopStatusCheckAsync();
    }

    [Fact]
    public async Task ExistingGame_QueuedTaskCannotRunAgainstBindingChangedAfterEnqueue()
    {
        using var fixture = await Fixture.CreateAsync();
        var local = fixture.Folder("queued-race-save");
        await File.WriteAllTextAsync(Path.Combine(local, "slot.sav"), "local");
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Queued Race", local);
        Assert.Null(await wizard.CreateProfileAsync());
        var id = wizard.LastCreatedInstanceId!.Value;
        var original = Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(id, CancellationToken.None));
        var job = new SyncJob { GameInstanceId = id, Direction = SyncDirection.Upload };
        await fixture.Engine.QueueAsync(job, CancellationToken.None);
        var newRoot = fixture.Folder("queued-race-remote");
        await fixture.Bindings.UpsertAsync(new StorageBinding
        {
            Id = original.Id, GameInstanceId = id, StorageProviderId = original.StorageProviderId,
            RemoteNamespace = original.RemoteNamespace, CreatedAtUtc = original.CreatedAtUtc,
            UpdatedAtUtc = original.UpdatedAtUtc.AddSeconds(1), ContentSelections = original.ContentSelections,
            Settings = new Dictionary<string, string> { ["rootPath"] = newRoot },
        }, CancellationToken.None);
        using var cts = new CancellationTokenSource();
        var worker = fixture.Engine.ProcessQueuedJobsAsync(cts.Token);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (job.Status is SyncJobStatus.Queued or SyncJobStatus.Running)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(10);
        }
        Assert.Equal(SyncJobStatus.Failed, job.Status);
        Assert.Contains("changed after this task was queued", Assert.Single(await fixture.History.ListRecordsAsync(id, CancellationToken.None)).ErrorMessage);
        Assert.Empty(Directory.EnumerateFileSystemEntries(newRoot));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
    }

    [Fact]
    public async Task ExistingGame_ConflictDecisionCannotBeAppliedToAnotherTarget()
    {
        using var fixture = await Fixture.CreateAsync();
        var local = fixture.Folder("old-comparison-save");
        var file = Path.Combine(local, "slot.sav");
        await File.WriteAllTextAsync(file, "remote-version");
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Old Comparison", local);
        Assert.Null(await wizard.CreateProfileAsync());
        var id = wizard.LastCreatedInstanceId!.Value;
        await fixture.UploadAsync(id);
        var binding = Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(id, CancellationToken.None));
        await File.WriteAllTextAsync(file, "local-version");
        var comparison = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = id, Direction = SyncDirection.Compare }, CancellationToken.None);
        var newRoot = fixture.Folder("new-comparison-remote");
        var provider = new LocalFolderStorageProvider(Microsoft.Extensions.Options.Options.Create(new LocalFolderOptions { RootPath = newRoot }));
        var remote = await RemoteSaveSession.OpenAsync(provider, binding.RemoteNamespace, CancellationToken.None);
        using (var stream = new MemoryStream("remote-version"u8.ToArray()))
            await remote.UploadAsync(binding.RemoteNamespace + "/saves/slot.sav", stream, CancellationToken.None);
        await remote.CommitAsync(CancellationToken.None);
        var target = new SyncTargetConfig { Name = "Other", ProviderId = "local-folder", Settings = new() { ["rootPath"] = newRoot } };
        fixture.TargetStore.Upsert(target);
        var details = fixture.Details();
        await details.LoadAsync(id);
        Assert.Null(await details.UpdateDestinationAsync(target.Id, binding.RemoteNamespace));
        var conflicts = fixture.Conflicts();
        conflicts.LoadFrom(new ConflictNavigationContext(id, "Old Comparison", comparison));
        conflicts.SetAllActions(ConflictResolutionAction.KeepRemote);
        Assert.Null(await conflicts.ResolveAsync());
        var job = Assert.Single(fixture.Queue.PendingJobs);
        Assert.Equal(comparison.ConfigurationFingerprint, job.ConflictResolutionPlan!.ExpectedConfigurationFingerprint);
        using var cts = new CancellationTokenSource();
        var worker = fixture.Engine.ProcessQueuedJobsAsync(cts.Token);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (job.Status is SyncJobStatus.Queued or SyncJobStatus.Running)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(10);
        }
        Assert.Equal(SyncJobStatus.Failed, job.Status);
        Assert.Equal("local-version", await File.ReadAllTextAsync(file));
        await WaitForStatusAsync(details, "Differences");
        await details.StopStatusCheckAsync();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
    }

    [Fact]
    public async Task AutomaticPullSetting_DefaultsOffAndPersistsWhenEnabled()
    {
        using var fixture = await Fixture.CreateAsync();
        var settings = fixture.Settings();
        Assert.False(settings.BuildSettings().AutoPullEnabled);
        settings.AutoPullEnabled = true;
        settings.AutoSnapshotEnabled = false;
        settings.RefreshManifestEnabled = false;
        settings.SaveSettings();
        var reloaded = fixture.Settings();
        Assert.True(reloaded.AutoPullEnabled);
        Assert.False(reloaded.AutoSnapshotEnabled);
        Assert.False(reloaded.RefreshManifestEnabled);
        reloaded.RestoreDefaults();
        Assert.False(reloaded.AutoPullEnabled);
        Assert.True(reloaded.AutoSnapshotEnabled);
    }

    [Fact]
    public async Task AutomaticUpdateSettings_DefaultOnAndPersistWhenDisabled()
    {
        using var fixture = await Fixture.CreateAsync();
        var settings = fixture.Settings();
        Assert.True(settings.AutoCheckUpdates);
        Assert.True(settings.AutoInstallUpdates);
        settings.AutoCheckUpdates = false;
        settings.AutoInstallUpdates = false;
        settings.SaveSettings();
        var reloaded = fixture.Settings();
        Assert.False(reloaded.AutoCheckUpdates);
        Assert.False(reloaded.AutoInstallUpdates);
    }

    [Fact]
    public async Task CloseToTraySetting_DefaultsOnAndPersistsWhenDisabled()
    {
        using var fixture = await Fixture.CreateAsync();
        var settings = fixture.Settings();
        Assert.True(settings.CloseToTray);
        settings.CloseToTray = false;
        settings.SaveSettings();
        Assert.False(fixture.Settings().CloseToTray);
    }

    [Fact]
    public async Task BackgroundStartupDefaultsOnAndCanBeDisabledIndependently()
    {
        using var fixture = await Fixture.CreateAsync();
        var settings = fixture.Settings();
        Assert.True(settings.StartupInBackground);
        Assert.True(StartupLaunchPolicy.StartHidden([StartupLaunchPolicy.StartupArgument], settings.StartupInBackground, false));
        Assert.False(StartupLaunchPolicy.StartHidden([], settings.StartupInBackground, false));
        settings.StartupInBackground = false;
        settings.SaveSettings();
        Assert.False(fixture.Settings().StartupInBackground);
        Assert.False(StartupLaunchPolicy.StartHidden([StartupLaunchPolicy.StartupArgument], false, false));
        Assert.True(StartupLaunchPolicy.StartHidden([], false, true));
    }

    [Fact]
    public async Task TwoDeviceProfiles_ShareRemoteAndPullOnlyAfterLocalBaselineConfirmation()
    {
        using var fixture = await Fixture.CreateAsync();
        var firstDirectory = fixture.Folder("computer-a");
        var secondDirectory = fixture.Folder("computer-b");
        var firstFile = Path.Combine(firstDirectory, "slot.sav");
        var secondFile = Path.Combine(secondDirectory, "slot.sav");
        await File.WriteAllTextAsync(firstFile, "common");
        await File.WriteAllTextAsync(secondFile, "common");
        var definition = new GameContentDefinition { GameId = "shared-game", DisplayName = "Shared", ContentItems = new[]
        {
            new ContentItem { ContentId = "saves", Category = ContentCategory.Save, PathTemplates = new[] { "%GAME_INSTALL_DIR%" } }
        } };
        await fixture.UserStore.SaveDefinitionAsync(definition, CancellationToken.None);
        var first = new GameInstance { GameId = definition.GameId, DisplayName = "Computer A", SourceProviderId = "custom", InstallDirectory = firstDirectory };
        var second = new GameInstance { GameId = definition.GameId, DisplayName = "Computer B", SourceProviderId = "custom", InstallDirectory = secondDirectory };
        foreach (var game in new[] { first, second })
        {
            await fixture.Games.UpsertAsync(game, CancellationToken.None);
            await fixture.Bindings.UpsertAsync(new StorageBinding { GameInstanceId = game.Id, StorageProviderId = "local-folder", RemoteNamespace = "shared-game",
                Settings = new Dictionary<string, string> { ["rootPath"] = fixture.StoragePath } }, CancellationToken.None);
        }
        await fixture.UploadAsync(first.Id);
        var coordinator = new AutoPullCoordinator(fixture.Games, fixture.Engine, fixture.Queue, fixture.Baselines(), NullLogger<AutoPullCoordinator>.Instance);
        coordinator.SetEnabled(true);
        await coordinator.ScanAsync(CancellationToken.None);
        Assert.Empty(fixture.Queue.PendingJobs);
        await coordinator.ConfirmBaselineAsync(second.Id, CancellationToken.None);
        await File.WriteAllTextAsync(firstFile, "from-computer-a");
        await fixture.UploadAsync(first.Id);
        await coordinator.ScanAsync(CancellationToken.None);
        var job = Assert.Single(fixture.Queue.PendingJobs);
        Assert.Equal(second.Id, job.GameInstanceId);
        using var cts = new CancellationTokenSource();
        var worker = fixture.Engine.ProcessQueuedJobsAsync(cts.Token);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (job.Status is SyncJobStatus.Queued or SyncJobStatus.Running)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(10);
        }
        Assert.Equal(SyncJobStatus.Completed, job.Status);
        Assert.Equal("from-computer-a", await File.ReadAllTextAsync(secondFile));
        Assert.NotEmpty(await fixture.History.ListSnapshotsAsync(second.Id, CancellationToken.None));
        coordinator.SetEnabled(false);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
    }

    [Fact]
    public async Task DetailsStatus_ReflectsActualSaveDifferences_InsteadOfOnlyTheLastSuccessfulJob()
    {
        using var fixture = await Fixture.CreateAsync();
        var saves = fixture.Folder("status-saves");
        var file = Path.Combine(saves, "slot.sav");
        await File.WriteAllTextAsync(file, "first");
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "Status Game", saves);
        Assert.Null(await wizard.CreateProfileAsync());
        var gameId = wizard.LastCreatedInstanceId!.Value;
        var details = fixture.Details();
        await details.LoadAsync(gameId);
        await WaitForStatusAsync(details, "Differences");
        await fixture.UploadAsync(gameId);
        await details.LoadAsync(gameId);
        await WaitForStatusAsync(details, "Synced");
        await File.WriteAllTextAsync(file, "changed");
        await details.LoadAsync(gameId);
        await WaitForStatusAsync(details, "Differences");
        await details.StopStatusCheckAsync();
    }

    [Fact]
    public async Task DetailsLoad_IgnoresAnOlderGameThatFinishesAfterTheCurrentSelection()
    {
        using var fixture = await Fixture.CreateAsync();
        var firstGame = await AddHistoryGameAsync(fixture, "First Game", DateTimeOffset.UtcNow.AddMinutes(-1));
        var secondGame = await AddHistoryGameAsync(fixture, "Second Game", DateTimeOffset.UtcNow);
        var delayedGames = new DelayedGames(fixture.Games) { DelayGame = firstGame };
        var details = fixture.Details(delayedGames);
        var firstLoad = details.LoadAsync(firstGame);
        try
        {
            await delayedGames.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await details.LoadAsync(secondGame);
        }
        finally
        {
            delayedGames.Release.TrySetResult();
        }
        await firstLoad;
        Assert.Equal(secondGame, details.InstanceId);
        Assert.Equal("Second Game", details.Title);
        await WaitForStatusAsync(details, "No files");
        await details.StopStatusCheckAsync();
    }

    [Fact]
    public async Task DetailsStop_WaitsForEarlierComparisonsAfterReload()
    {
        using var fixture = await Fixture.CreateAsync();
        var gameId = await AddHistoryGameAsync(fixture, "Delayed compare", DateTimeOffset.UtcNow);
        var engine = new DelayedComparisonEngine();
        var details = fixture.Details(engine: engine);
        await details.LoadAsync(gameId);
        await engine.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await details.LoadAsync(gameId);
        var stopping = details.StopStatusCheckAsync();
        try { Assert.False(stopping.IsCompleted); }
        finally { engine.Release.TrySetResult(); }
        await stopping.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static async Task WaitForStatusAsync(GameDetailsViewModel details, string status)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (details.OverallStatusText == "Checking…" && DateTime.UtcNow < timeout) await Task.Delay(10);
        Assert.Equal(status, details.OverallStatusText);
    }

    [Fact]
    public void RecordDirections_DistinguishSnapshotRestoreFromDownload()
    {
        var restored = new SyncRecord { Direction = SyncDirection.Download, Status = SyncJobStatus.Completed,
            Summary = "Restored 1 paths from snapshot 0123." };
        var download = new SyncRecord { Direction = SyncDirection.Download, Status = SyncJobStatus.Completed,
            Summary = "Downloaded 1 files." };
        Assert.Equal("Restore snapshot", UiFormat.DirectionText(restored, false));
        Assert.Equal("Download", UiFormat.DirectionText(download, false));
        Assert.Equal("处理存档", UiFormat.DirectionText(SyncDirection.ResolveConflict, true));
    }

    [Fact]
    public async Task HistoryFiltering_ClearsHiddenSelection_AndReloadSelectsAVisibleRecord()
    {
        using var fixture = await Fixture.CreateAsync();
        var gameOne = await AddHistoryGameAsync(fixture, "Game One", DateTimeOffset.UtcNow.AddMinutes(-1));
        var gameTwo = await AddHistoryGameAsync(fixture, "Game Two", DateTimeOffset.UtcNow);
        var viewModel = new HistoryPageViewModel(new Localization(), fixture.History, fixture.Games, fixture.Bindings, fixture.Engine);
        await viewModel.LoadAsync();
        Assert.Equal("Game Two", viewModel.DetailGameName);
        Assert.Equal(Assert.Single(await fixture.History.ListSnapshotsAsync(gameTwo, CancellationToken.None)).Id, Assert.Single(viewModel.Snapshots).SnapshotId);

        viewModel.SearchText = "Game One";
        Assert.Equal("Game One", Assert.Single(viewModel.Entries).Game);
        Assert.Empty(viewModel.DetailGameName);
        Assert.Empty(viewModel.RecordDetails);
        Assert.Empty(viewModel.Snapshots);

        await viewModel.LoadAsync();
        Assert.Equal("Game One", viewModel.DetailGameName);
        Assert.Equal(Assert.Single(await fixture.History.ListSnapshotsAsync(gameOne, CancellationToken.None)).Id, Assert.Single(viewModel.Snapshots).SnapshotId);
    }

    [Fact]
    public async Task HistorySelection_IgnoresLateDetailsForAPreviouslySelectedGame()
    {
        using var fixture = await Fixture.CreateAsync();
        var gameOne = await AddHistoryGameAsync(fixture, "Game One", DateTimeOffset.UtcNow.AddMinutes(-1));
        var gameTwo = await AddHistoryGameAsync(fixture, "Game Two", DateTimeOffset.UtcNow);
        var delayedHistory = new DelayedHistory(fixture.History);
        var viewModel = new HistoryPageViewModel(new Localization(), delayedHistory, fixture.Games, fixture.Bindings, fixture.Engine);
        await viewModel.LoadAsync();
        var first = Assert.Single(viewModel.Entries, row => row.Game == "Game One");
        var second = Assert.Single(viewModel.Entries, row => row.Game == "Game Two");
        delayedHistory.DelayGame = gameOne;
        var firstSelection = viewModel.SelectRecordAsync(first);
        try
        {
            await delayedHistory.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await viewModel.SelectRecordAsync(second);
        }
        finally
        {
            delayedHistory.Release.TrySetResult();
        }
        await firstSelection;
        Assert.Equal("Game Two", viewModel.DetailGameName);
        Assert.Equal(Assert.Single(await fixture.History.ListSnapshotsAsync(gameTwo, CancellationToken.None)).Id, Assert.Single(viewModel.Snapshots).SnapshotId);
        Assert.True(viewModel.IsRecordSelected(second));
    }

    [Fact]
    public async Task History_DoesNotInventTargetFromCurrentBinding()
    {
        using var fixture = await Fixture.CreateAsync();
        await AddHistoryGameAsync(fixture, "A game", DateTimeOffset.UtcNow);
        var viewModel = new HistoryPageViewModel(new Localization(), fixture.History, fixture.Games, fixture.Bindings, fixture.Engine);
        await viewModel.LoadAsync();
        Assert.Equal("Target not recorded", Assert.Single(viewModel.Entries).Target);
        Assert.Contains(viewModel.RecordDetails, row => row.Label == "Remote Namespace" && row.Value == "Not recorded");
    }

    [Fact]
    public async Task History_DisplaysRecordedDestinationAfterBindingChanges()
    {
        using var fixture = await Fixture.CreateAsync();
        var gameId = await AddHistoryGameAsync(fixture, "A game", DateTimeOffset.UtcNow.AddMinutes(-1));
        await fixture.History.AddRecordAsync(new SyncRecord { GameInstanceId = gameId, Direction = SyncDirection.Upload,
            Status = SyncJobStatus.Completed, TargetId = Guid.NewGuid(), TargetName = "Original target", RemoteNamespace = "games/original" }, CancellationToken.None);
        var viewModel = new HistoryPageViewModel(new Localization(), fixture.History, fixture.Games, fixture.Bindings, fixture.Engine);
        await viewModel.LoadAsync();
        Assert.Equal("Original target", viewModel.Entries.First().Target);
        Assert.Contains(viewModel.RecordDetails, row => row.Label == "Remote Namespace" && row.Value == "games/original");
    }

    private static async Task<Guid> AddHistoryGameAsync(Fixture fixture, string name, DateTimeOffset timestamp)
    {
        var wizard = fixture.Wizard();
        PrepareManual(wizard, name, fixture.Folder(name));
        Assert.Null(await wizard.CreateProfileAsync());
        var gameId = wizard.LastCreatedInstanceId!.Value;
        await fixture.History.AddSnapshotAsync(new Snapshot { GameInstanceId = gameId, ArchivePath = name + ".zip" }, CancellationToken.None);
        await fixture.History.AddRecordAsync(new SyncRecord { GameInstanceId = gameId, Direction = SyncDirection.Upload,
            Status = SyncJobStatus.Completed, StartedAtUtc = timestamp }, CancellationToken.None);
        return gameId;
    }

    [Fact]
    public async Task VariablesFiltering_DoesNotEditOrDeleteAHiddenSelection()
    {
        using var fixture = await Fixture.CreateAsync();
        var viewModel = fixture.Variables();
        viewModel.SaveUserVariable("ONE", fixture.Folder("variable-one"), "first");
        viewModel.SaveUserVariable("TWO", fixture.Folder("variable-two"), "second");
        viewModel.SelectVariable(Assert.Single(viewModel.Variables, row => row.Name == "%TWO%"));
        viewModel.SearchText = "%ONE%";
        Assert.Null(viewModel.SelectedVariable);
        viewModel.DeleteSelectedUserVariable();
        viewModel.Load();
        Assert.Equal("%ONE%", viewModel.SelectedVariableName);
        Assert.Single(viewModel.Variables);
        viewModel.DeleteSelectedUserVariable();
        viewModel.SearchText = "%TWO%";
        Assert.Single(viewModel.Variables);
    }

    [Fact]
    public async Task BuiltInVariables_AreReadOnlyEvenWhenACustomOverrideHasTheSameName()
    {
        using var fixture = await Fixture.CreateAsync();
        var viewModel = fixture.Variables();
        viewModel.Load();
        var builtIn = viewModel.Variables.First(row => row.TypeCode == "SYS");
        viewModel.SaveUserVariable(builtIn.Name, fixture.Folder("override"), null);
        viewModel.SelectVariable(viewModel.Variables.First(row => row.TypeCode == "SYS" && row.Name == builtIn.Name));
        Assert.False(viewModel.CanEditSelectedVariable);
        Assert.Contains("read-only", viewModel.LastModifiedText);
        viewModel.DeleteSelectedUserVariable();
        Assert.True(viewModel.UserVariableExists(builtIn.Name));
        viewModel.SelectVariable(viewModel.Variables.First(row => row.TypeCode == "USR" && row.Name == builtIn.Name));
        Assert.True(viewModel.CanEditSelectedVariable);
    }

    [Fact]
    public async Task TargetFiltering_ClearsHiddenSelection_AndRemovesOnlyTheVisibleTarget()
    {
        using var fixture = await Fixture.CreateAsync();
        var first = new SyncTargetConfig { Name = "First Target", ProviderId = "local-folder",
            Settings = new() { ["rootPath"] = fixture.Folder("target-one") } };
        var second = new SyncTargetConfig { Name = "Second Target", ProviderId = "local-folder",
            Settings = new() { ["rootPath"] = fixture.Folder("target-two") } };
        var viewModel = fixture.Targets();
        await viewModel.SaveTargetAsync(first);
        await viewModel.SaveTargetAsync(second);
        Assert.Equal(second.Id, viewModel.SelectedTargetId);
        viewModel.SearchText = "First Target";
        Assert.Null(viewModel.SelectedTargetId);
        Assert.Empty(viewModel.ActiveBindings);
        await viewModel.RemoveSelectedAsync();
        await viewModel.LoadAsync();
        Assert.Equal(first.Id, viewModel.SelectedTargetId);
        await viewModel.RemoveSelectedAsync();
        viewModel.SearchText = "Second Target";
        Assert.Equal(second.Id, Assert.Single(viewModel.Targets).TargetId);
    }

    [Fact]
    public async Task TargetPage_SetsDefault_AndSavingSettingsDoesNotChangeIt()
    {
        using var fixture = await Fixture.CreateAsync();
        var second = new SyncTargetConfig { Name = "Second Target", ProviderId = "local-folder",
            Settings = new() { ["rootPath"] = fixture.Folder("settings-target") } };
        var targets = fixture.Targets();
        await targets.SaveTargetAsync(second);
        await targets.SelectTargetAsync(Assert.Single(targets.Targets, row => row.TargetId == second.Id));
        Assert.True(targets.CanSetSelectedDefault);
        await targets.SetSelectedDefaultAsync();
        Assert.False(targets.CanSetSelectedDefault);
        fixture.Settings().SaveSettings();
        Assert.Equal(second.Id, fixture.DefaultTargetId());
    }

    [Fact]
    public async Task LanguageSwitch_PersistsAndNotifiesWithoutAPackageIdentity()
    {
        using var fixture = await Fixture.CreateAsync();
        var localization = fixture.RealLocalization();
        var notifications = 0;
        localization.LanguageChanged += (_, _) => notifications++;
        Assert.True(localization.SetLanguage("en-US"));
        Assert.Equal("Library", localization.GetString("MainWindow.Nav.Library"));
        Assert.Equal("en-US", fixture.RealLocalization().CurrentLanguageTag);
        Assert.Equal(1, notifications);
        Assert.False(localization.SetLanguage("en-US"));
    }

    [Theory]
    [InlineData(true, "Enabled")]
    [InlineData(false, "Disabled")]
    public async Task SnapshotSetting_IsReflectedInLibraryAndDownloadWarning(bool enabled, string expected)
    {
        using var fixture = await Fixture.CreateAsync();
        fixture.SetAutoSnapshot(enabled);
        var library = fixture.Library();
        await library.LoadAsync();
        Assert.Equal(expected, Assert.Single(library.Stats, row => row.Label == "Snapshot before download").Value);
        var warning = UiFormat.DownloadConfirmationText(enabled, false);
        Assert.Contains(enabled ? "will be created" : "disabled", warning);
    }

    [Fact]
    public async Task ManualNamesWithTheSameOldSlug_AreSeparateProfilesAndDefinitions()
    {
        using var fixture = await Fixture.CreateAsync();
        foreach (var name in new[] { "Game-A", "Game A", "A \"quoted\" game" })
        {
            var wizard = fixture.Wizard();
            PrepareManual(wizard, name, fixture.Folder(GameIdentity.StorageNamespace(name)));
            var preview = Assert.Single(wizard.FinishSummary, row => row.Label == "Remote directory").Value;
            Assert.Null(await wizard.CreateProfileAsync());
            Assert.Equal(preview, Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(wizard.LastCreatedInstanceId!.Value, CancellationToken.None)).RemoteNamespace);
        }
        var games = await fixture.Games.ListAsync(CancellationToken.None);
        Assert.Equal(3, games.Count);
        Assert.Equal(3, games.Select(game => game.GameId).Distinct().Count());
        var namespaces = new List<string>();
        foreach (var game in games) namespaces.Add(Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(game.Id, CancellationToken.None)).RemoteNamespace);
        Assert.Equal(3, namespaces.Distinct().Count());
        Assert.Equal(3, (await fixture.UserStore.LoadDefinitionsAsync(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task DuplicateManualProfile_DoesNotReplaceTheExistingSavePath()
    {
        using var fixture = await Fixture.CreateAsync();
        var original = fixture.Folder("one");
        var first = fixture.Wizard();
        PrepareManual(first, "Same Game", original);
        Assert.Null(await first.CreateProfileAsync());
        var second = fixture.Wizard();
        PrepareManual(second, "Same Game", fixture.Folder("two"));
        Assert.NotNull(await second.CreateProfileAsync());
        Assert.Single(await fixture.Games.ListAsync(CancellationToken.None));
        Assert.Equal(original.Replace('\\', '/'), Assert.Single(Assert.Single((await fixture.UserStore.LoadDefinitionsAsync(CancellationToken.None))).ContentItems).PathTemplates.Single());
    }

    [Fact]
    public async Task MismatchedSteamRecipe_IsRejectedBeforeAnyBackupCanBeWritten()
    {
        using var fixture = await Fixture.CreateAsync();
        await fixture.UserStore.SaveDefinitionAsync(new GameContentDefinition
        {
            GameId = "legacy", DisplayName = "Legacy",
            SourceHints = new Dictionary<string, string> { ["steam.id"] = "123" },
            ContentItems = new[] { new ContentItem { ContentId = "saves", Category = ContentCategory.Save, PathTemplates = new[] { fixture.Folder("saves") } } },
        }, CancellationToken.None);
        var game = new GameInstance { GameId = "legacy", DisplayName = "Legacy", SourceProviderId = "steam", PlatformInstanceId = "456" };
        await fixture.Games.UpsertAsync(game, CancellationToken.None);
        await fixture.Bindings.UpsertAsync(new StorageBinding { GameInstanceId = game.Id, StorageProviderId = "local-folder", RemoteNamespace = "game" }, CancellationToken.None);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = game.Id, Direction = SyncDirection.Compare }, CancellationToken.None));
        Assert.Contains("Steam app ID", exception.Message);
        Assert.False(Directory.Exists(fixture.StoragePath));
    }

    [Fact]
    public async Task UnknownGame_NeverUsesItsInstallDirectoryAsAnAssumedSaveLocation()
    {
        using var fixture = await Fixture.CreateAsync();
        var game = new DiscoveredGame { GameId = "steam-123", DisplayName = "Unknown", SourceProviderId = "steam", InstallDirectory = fixture.Folder("game-binaries") };
        var wizard = fixture.Wizard(new Source("steam", new[] { game }));
        wizard.SelectOption(new WizardOption("steam", "Steam", ""));
        await wizard.ScanSelectedSourceAsync();
        wizard.CurrentStep = 2;
        wizard.SelectOption(Assert.Single(wizard.Games));
        wizard.GoNext();
        Assert.Empty(wizard.ContentItems);
        Assert.NotNull(wizard.ValidateCurrentStep());
        Assert.Empty(await fixture.Games.ListAsync(CancellationToken.None));
    }

    [Fact]
    public void NamedRemoteDirectory_UsesReadableNameWhenEnglishIsUnavailable()
    {
        var name = GameIdentity.NamedStorageNamespace("生化危机4", "custom-game-id");
        Assert.StartsWith("生化危机4-", name);
        Assert.NotEqual(name, GameIdentity.NamedStorageNamespace("生化危机4", "another-game-id"));
    }

    [Fact]
    public async Task ManualNameMatch_CreatesProfileWithCatalogIdentityAndChosenProgram()
    {
        using var fixture = await Fixture.CreateAsync();
        var saves = fixture.Folder("catalog-saves");
        var gameDirectory = fixture.Folder("game-install");
        var exe = Path.Combine(gameDirectory, "Game Name.exe");
        await File.WriteAllTextAsync(exe, "test program");
        await fixture.UserStore.SaveDefinitionAsync(new GameContentDefinition
        {
            GameId = "catalog-game-id", DisplayName = "Game Name",
            SourceHints = new Dictionary<string, string> { ["steam.id"] = "123" },
            ContentItems = [new ContentItem { ContentId = "saves", Category = ContentCategory.Save, PathTemplates = [saves], DefaultEnabled = true }],
        }, CancellationToken.None);
        var wizard = fixture.Wizard();
        wizard.SwitchToManualSetup();
        wizard.SetProgramSelection(GameProgramSelectionReader.Read(exe));
        Assert.Null(await wizard.PrepareManualCandidatesAsync());
        Assert.True(Assert.Single(wizard.Games).IsSelected);
        wizard.GoNext();
        Assert.Null(wizard.ValidateCurrentStep());
        while (wizard.CurrentStep < 6) wizard.GoNext();
        Assert.Null(await wizard.CreateProfileAsync());
        var game = Assert.Single(await fixture.Games.ListAsync(CancellationToken.None));
        Assert.Equal("catalog-game-id", game.GameId);
        Assert.Equal(gameDirectory, game.InstallDirectory);
        Assert.Equal(exe, game.Variables[GameProgramSelectionReader.ProgramPathVariable]);
        var binding = Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(game.Id, CancellationToken.None));
        Assert.Equal(GameIdentity.NamedStorageNamespace("Game Name", "catalog-game-id"), binding.RemoteNamespace);
        Assert.StartsWith("game-name-", binding.RemoteNamespace);
        Assert.Equal("123", GameArtworkResolver.Resolve(game, await fixture.Manifest.GetDefinitionAsync(game.GameId, CancellationToken.None)).CoverUri!.Split('/')[5]);
    }

    [Fact]
    public async Task ManualNameMatch_AmbiguousResultsRequireExplicitSelection()
    {
        using var fixture = await Fixture.CreateAsync();
        var saves = fixture.Folder("ambiguous-saves");
        foreach (var (id, title, steamId) in new[] { ("one", "Game One", "111"), ("two", "Game-One", "222") })
            await fixture.UserStore.SaveDefinitionAsync(new GameContentDefinition
            {
                GameId = id, DisplayName = title,
                SourceHints = new Dictionary<string, string> { ["steam.id"] = steamId },
                ContentItems = [new ContentItem { ContentId = "saves", Category = ContentCategory.Save, PathTemplates = [saves], DefaultEnabled = true }],
            }, CancellationToken.None);
        var wizard = fixture.Wizard();
        wizard.SwitchToManualSetup();
        wizard.ManualGameName = "Game One";
        Assert.Null(await wizard.PrepareManualCandidatesAsync());
        Assert.Equal(2, wizard.Games.Count);
        Assert.DoesNotContain(wizard.Games, item => item.IsSelected);
        wizard.GoNext();
        Assert.NotNull(wizard.ValidateCurrentStep());
        wizard.SelectOption(wizard.Games.Single(option => option.Key == "one"));
        wizard.SelectOption(wizard.Games.Single(option => option.Key == "two"));
        Assert.Null(wizard.ValidateCurrentStep());
        while (wizard.CurrentStep < 6) wizard.GoNext();
        Assert.Null(await wizard.CreateProfileAsync());
        Assert.Equal("222", Assert.Single(await fixture.Games.ListAsync(CancellationToken.None)).Variables["STORE_GAME_ID"]);
    }

    [Fact]
    public async Task ManualChineseName_UsesSteamSearchOnlyToFindALocalDefinition()
    {
        using var fixture = await Fixture.CreateAsync();
        var saves = fixture.Folder("localized-saves");
        await fixture.UserStore.SaveDefinitionAsync(new GameContentDefinition
        {
            GameId = "elden", DisplayName = "ELDEN RING",
            SourceHints = new Dictionary<string, string> { ["steam.id"] = "1245620" },
            ContentItems = [new ContentItem { ContentId = "saves", Category = ContentCategory.Save, PathTemplates = [saves], DefaultEnabled = true }],
        }, CancellationToken.None);
        var handler = new SteamSearchHandler("""{"items":[{"id":1245620,"name":"艾尔登法环"},{"id":999,"name":"Other Game"}]}""");
        var wizard = fixture.Wizard(steamSearch: new SteamCatalogSearchService(new HttpClient(handler)));
        wizard.SwitchToManualSetup();
        wizard.ManualGameName = "艾尔登法环";
        Assert.Null(await wizard.PrepareManualCandidatesAsync());
        Assert.Equal("elden", Assert.Single(wizard.Games).Key);
        Assert.True(Assert.Single(wizard.Games).IsSelected);
        Assert.Contains("store.steampowered.com", handler.LastRequest!.Host);
    }

    [Fact]
    public async Task ManualChineseName_WithoutSpaceBeforeNumberStillFindsResidentEvilTwo()
    {
        using var fixture = await Fixture.CreateAsync();
        await fixture.UserStore.SaveDefinitionAsync(new GameContentDefinition
        {
            GameId = "resident-evil-2", DisplayName = "Resident Evil 2 (2019)",
            SourceHints = new Dictionary<string, string> { ["steam.id"] = "883710" },
            ContentItems = [new ContentItem { ContentId = "default", Category = ContentCategory.Save, PathTemplates = ["%STEAM_ROOT%/userdata/%STORE_USER_ID%/883710/remote"] }],
        }, CancellationToken.None);
        var handler = new NumberSpacingSearchHandler();
        var service = new SteamCatalogSearchService(new HttpClient(handler));
        var results = await service.SearchAsync("生化危机2", true, CancellationToken.None);
        Assert.Equal("883710", Assert.Single(results).SteamId);
        Assert.Equal(2, handler.Calls);
        var wizard = fixture.Wizard(steamSearch: service);
        wizard.SwitchToManualSetup();
        wizard.ManualGameName = "生化危机2";
        Assert.Null(await wizard.PrepareManualCandidatesAsync());
        Assert.Equal("resident-evil-2", Assert.Single(wizard.Games).Key);
    }

    [Fact]
    public async Task ManualSearch_WhenOfflineKeepsCustomSavePathAvailable()
    {
        using var fixture = await Fixture.CreateAsync();
        var wizard = fixture.Wizard(steamSearch: new SteamCatalogSearchService(new HttpClient(new FailingSearchHandler())));
        wizard.SwitchToManualSetup();
        wizard.ManualGameName = "Offline Unknown Game";
        wizard.ManualSavePath = fixture.Folder("offline-save");
        Assert.Null(await wizard.PrepareManualCandidatesAsync());
        Assert.True(Assert.Single(wizard.Games).IsSelected);
        Assert.Contains("unavailable", wizard.ManualMatchMessage, StringComparison.OrdinalIgnoreCase);
        wizard.GoNext();
        while (wizard.CurrentStep < 6) wizard.GoNext();
        Assert.Null(await wizard.CreateProfileAsync());
        Assert.StartsWith("custom-", Assert.Single(await fixture.Games.ListAsync(CancellationToken.None)).GameId);
    }

    [Fact]
    public async Task ManualCatalogMatch_ResolvesStoreUserWildcardForNonSteamGame()
    {
        using var fixture = await Fixture.CreateAsync();
        var saves = fixture.Folder("wildcard-saves");
        var account = Path.Combine(saves, "user123");
        Directory.CreateDirectory(account);
        await File.WriteAllTextAsync(Path.Combine(account, "save.dat"), "test");
        await fixture.UserStore.SaveDefinitionAsync(new GameContentDefinition
        {
            GameId = "matched-game", DisplayName = "Matched Game",
            SourceHints = new Dictionary<string, string> { ["steam.id"] = "123" },
            ContentItems = [new ContentItem { ContentId = "saves", Category = ContentCategory.Save, PathTemplates = [saves + "/%STORE_USER_ID%"], DefaultEnabled = true }],
        }, CancellationToken.None);
        var wizard = fixture.Wizard();
        wizard.SwitchToManualSetup();
        wizard.ManualGameName = "Matched Game";
        Assert.Null(await wizard.PrepareManualCandidatesAsync());
        wizard.GoNext();
        wizard.GoNext();
        wizard.GoNext();
        Assert.Null(wizard.ValidateCurrentStep());
        while (wizard.CurrentStep < 6) wizard.GoNext();
        Assert.Null(await wizard.CreateProfileAsync());
        var game = Assert.Single(await fixture.Games.ListAsync(CancellationToken.None));
        Assert.Equal("*", game.Variables["STORE_USER_ID"]);
        Assert.Equal("123", game.Variables["STORE_GAME_ID"]);
        var (comparison, error) = await wizard.CompareCreatedProfileAsync();
        Assert.Null(error);
        Assert.Contains(comparison!.Comparison.Entries, entry => entry.LocalHash is not null);
    }

    [Fact]
    public async Task Wizard_UnresolvedResidentEvilPath_CanBeCorrectedWithoutChangingRemoteIdentity()
    {
        using var fixture = await Fixture.CreateAsync();
        var saves = fixture.Folder("resident-evil-saves");
        await File.WriteAllTextAsync(Path.Combine(saves, "slot.sav"), "machine-one-save");
        const string unresolved = "%STEAM_ROOT%/userdata/%STORE_USER_ID%/883710/remote";
        var definition = new GameContentDefinition
        {
            GameId = "resident-evil-2", DisplayName = "Resident Evil 2",
            SourceHints = new Dictionary<string, string> { ["steam.id"] = "883710" },
            ContentItems = [new ContentItem { ContentId = "default", Category = ContentCategory.Save,
                PathTemplates = [unresolved, "%OTHER_MISSING_ROOT%/saves"], DefaultEnabled = true }],
        };
        await fixture.UserStore.SaveDefinitionAsync(definition, CancellationToken.None);
        var wizard = fixture.Wizard();
        wizard.SwitchToManualSetup();
        wizard.ManualGameName = "Resident Evil 2";
        Assert.Null(await wizard.PrepareManualCandidatesAsync());
        wizard.GoNext(); wizard.GoNext(); wizard.GoNext();
        Assert.Equal(4, wizard.CurrentStep);
        Assert.NotNull(wizard.ValidateCurrentStep());
        var first = Assert.Single(wizard.PathReview, row => row.Template == unresolved);
        first.OverridePath = saves;
        var second = Assert.Single(wizard.PathReview, row => row.Template != unresolved);
        second.OverridePath = fixture.Folder("resident-evil-other-saves");
        wizard.ApplyPathChanges();
        Assert.Null(wizard.ValidateCurrentStep());
        Assert.True(wizard.Steps.Single(step => step.Index == 4).IsCurrent);
        Assert.True(wizard.Steps.Single(step => step.Index == 3).IsComplete);
        wizard.GoNext(); wizard.GoNext();
        Assert.Null(await wizard.CreateProfileAsync());

        var game = Assert.Single(await fixture.Games.ListAsync(CancellationToken.None));
        var binding = Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(game.Id, CancellationToken.None));
        var selection = Assert.Single(binding.ContentSelections);
        Assert.Equal(saves.Replace('\\', '/'), selection.EffectiveTemplate(unresolved));
        Assert.Contains(unresolved, Assert.Single((await fixture.Manifest.GetDefinitionAsync(game.GameId, CancellationToken.None))!.ContentItems).PathTemplates);
        var firstComparison = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = game.Id, Direction = SyncDirection.Compare }, CancellationToken.None);
        var firstRemotePath = Assert.Single(firstComparison.Entries, entry => entry.LocalHash is not null).RelativePath;
        await fixture.UploadAsync(game.Id);

        var machineTwo = fixture.Folder("resident-evil-machine-two");
        await File.WriteAllTextAsync(Path.Combine(machineTwo, "slot.sav"), "machine-one-save");
        var details = fixture.Details();
        await details.LoadAsync(game.Id);
        var row = Assert.Single(details.ContentItems, value => value.PathTemplate == unresolved);
        Assert.Null(await details.UpdateContentPathAsync(row, machineTwo));
        var secondComparison = await fixture.Engine.CompareAsync(new SyncJob { GameInstanceId = game.Id, Direction = SyncDirection.Compare }, CancellationToken.None);
        var matched = Assert.Single(secondComparison.Entries, entry => entry.LocalHash is not null);
        Assert.Equal(firstRemotePath, matched.RelativePath);
        Assert.Equal(SyncChangeKind.Unchanged, matched.ChangeKind);
        await details.StopStatusCheckAsync();
    }

    [Fact]
    public async Task Wizard_RemoteDirectoryCanBeCustomizedAndRejectsUnsafePaths()
    {
        using var fixture = await Fixture.CreateAsync();
        var wizard = fixture.Wizard();
        PrepareManual(wizard, "生化危机4", fixture.Folder("manual-saves"));
        wizard.RemoteNamespaceText = "../outside";
        Assert.NotNull(wizard.ValidateCurrentStep());
        Assert.NotNull(await wizard.CreateProfileAsync());
        Assert.Empty(await fixture.Games.ListAsync(CancellationToken.None));
        wizard.RemoteNamespaceText = "resident-evil-4";
        Assert.Null(wizard.ValidateCurrentStep());
        Assert.Null(await wizard.CreateProfileAsync());
        var game = Assert.Single(await fixture.Games.ListAsync(CancellationToken.None));
        Assert.Equal("resident-evil-4", Assert.Single(await fixture.Bindings.ListByGameInstanceAsync(game.Id, CancellationToken.None)).RemoteNamespace);
    }

    private static void PrepareManual(AddGameWizardViewModel wizard, string name, string path)
    {
        wizard.SwitchToManualSetup();
        wizard.SetManualGame(name, path);
        wizard.CurrentStep = 2;
        while (wizard.CurrentStep < 6) wizard.GoNext();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "gsync-app-logic-tests", Guid.NewGuid().ToString("N"));
        private readonly Localization _localization = new();
        private Paths _paths = null!;
        private UserVariablesStore _variables = null!;
        private SyncTargetStore _targets = null!;
        private GameProfileRepository _profiles = null!;
        public GameInstanceRepository Games { get; private set; } = null!;
        public StorageBindingRepository Bindings { get; private set; } = null!;
        public SyncHistoryRepository History { get; private set; } = null!;
        public YamlUserDefinitionStore UserStore { get; private set; } = null!;
        public ManifestService Manifest { get; private set; } = null!;
        public SyncEngine Engine { get; private set; } = null!;
        public SyncQueue Queue { get; } = new();
        public SyncTargetStore TargetStore => _targets;
        public AutoPullBaselineStore Baselines() => new(_paths);
        public string StoragePath => Path.Combine(_root, "data/storage");
        public string Folder(string name) { var path = Path.Combine(_root, name); Directory.CreateDirectory(path); return path; }
        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            Directory.CreateDirectory(fixture._root);
            fixture._paths = new Paths(Path.Combine(fixture._root, "data"));
            var factory = new SqliteConnectionFactory($"Data Source={fixture._paths.GetDatabasePath()};Pooling=False");
            await new DatabaseInitializer(factory, fixture._paths).InitializeAsync(CancellationToken.None);
            fixture.Games = new(factory); fixture.Bindings = new(factory); fixture.History = new(factory);
            fixture._profiles = new(factory);
            fixture.UserStore = new(fixture._paths.GetDefinitionsDirectory(), NullLogger<YamlUserDefinitionStore>.Instance);
            fixture.Manifest = new(fixture.UserStore, new DefinitionOverrideStore(factory), new CommunityDefinitionStore(factory),
                new LudusaviManifestParser(NullLogger<LudusaviManifestParser>.Instance), new NoUpdate(), NullLogger<ManifestService>.Instance);
            fixture._variables = new(fixture._paths); fixture._targets = new(fixture._paths);
            fixture.Engine = new(fixture.Queue, fixture.Games, fixture.Bindings, fixture.History, fixture.Manifest,
                new[] { new Source("custom", Array.Empty<DiscoveredGame>()) },
                new[] { new LocalFolderStorageProvider(Microsoft.Extensions.Options.Options.Create(new LocalFolderOptions { RootPath = fixture.StoragePath })) },
                fixture._paths, new PathResolver(), new SystemVariableProvider(), NullLogger<SyncEngine>.Instance,
                userVariablesProvider: () => fixture._variables.AsDictionary(),
                targetBindingResolver: (binding, targetId) =>
                {
                    var target = fixture._targets.Get(targetId) ?? throw new InvalidOperationException("Target missing.");
                    var settings = new Dictionary<string, string>(target.Settings, StringComparer.OrdinalIgnoreCase)
                    {
                        ["targetId"] = target.Id.ToString("D"), ["__gsyncHistoryTargetName"] = target.Name,
                        ["__gsyncTargetRevision"] = target.UpdatedAtUtc.ToUniversalTime().ToString("O"),
                    };
                    return new StorageBinding { Id = binding.Id, GameInstanceId = binding.GameInstanceId, StorageProviderId = target.ProviderId,
                        RemoteNamespace = binding.RemoteNamespace, Settings = settings, ContentSelections = binding.ContentSelections,
                        CreatedAtUtc = binding.CreatedAtUtc, UpdatedAtUtc = binding.UpdatedAtUtc };
                });
            return fixture;
        }
        public GameDetailsViewModel Details(IGameInstanceRepository? games = null, ISyncEngine? engine = null, IUserDefinitionStore? definitionStore = null) => new(_localization, games ?? Games, Bindings, History, Manifest, engine ?? Engine,
            new[] { new Source("custom", Array.Empty<DiscoveredGame>()) }, definitionStore ?? UserStore, new PathResolver(), new SystemVariableProvider(), _variables, _targets, Engine);
        public ConflictResolutionViewModel Conflicts() => new(_localization, Engine);
        public VariablesPageViewModel Variables() => new(_localization, new SystemVariableProvider(), new PathResolver(), _variables,
            Array.Empty<ISourceProvider>());
        public SyncTargetsPageViewModel Targets() => new(_localization, _targets,
            Array.Empty<IStorageProvider>(), Games, Bindings);
        public SettingsPageViewModel Settings() => new(_localization, new UiSettingsStore(_paths), _paths, Games);
        public LibraryPageViewModel Library(ISyncEngine? engine = null) => new(_localization, Games, Bindings, History, Manifest, engine ?? Engine, Queue, _targets, new UiSettingsStore(_paths));
        public void SetAutoSnapshot(bool enabled) => new UiSettingsStore(_paths).Save(new AppUiSettings { AutoSnapshotBeforeDownload = enabled });
        public LocalizationService RealLocalization() => new(new UiSettingsStore(_paths));
        public Guid? DefaultTargetId() => _targets.GetDefault()?.Id;
        public AddGameWizardViewModel Wizard(ISourceProvider? source = null, SteamCatalogSearchService? steamSearch = null) => new(_localization,
            new[] { source ?? new Source("custom", Array.Empty<DiscoveredGame>()) }, Manifest, Games, Bindings, _targets,
            new AddGameMatchService(), new AddGamePathValidationService(new PathResolver(), new SystemVariableProvider(), () => _variables.AsDictionary()),
            new PathResolver(), new SystemVariableProvider(), Engine, UserStore, _profiles, steamSearch);
        public Task<RemoteSaveSession> OpenRemoteAsync(string pathNamespace) => RemoteSaveSession.OpenAsync(
            new LocalFolderStorageProvider(Microsoft.Extensions.Options.Options.Create(new LocalFolderOptions { RootPath = StoragePath })), pathNamespace, CancellationToken.None);
        public async Task UploadAsync(Guid gameId)
        {
            using var cts = new CancellationTokenSource();
            var worker = Engine.ProcessQueuedJobsAsync(cts.Token);
            var job = new SyncJob { GameInstanceId = gameId, Direction = SyncDirection.Upload };
            await Engine.QueueAsync(job, CancellationToken.None);
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (job.Status is SyncJobStatus.Queued or SyncJobStatus.Running)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException();
                await Task.Delay(10);
            }
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
            Assert.Equal(SyncJobStatus.Completed, job.Status);
        }
        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
    private sealed class FailingDefinitionStore(IUserDefinitionStore inner) : IUserDefinitionStore
    {
        public Task<IReadOnlyList<GameContentDefinition>> LoadDefinitionsAsync(CancellationToken token) => inner.LoadDefinitionsAsync(token);
        public Task SaveDefinitionAsync(GameContentDefinition definition, CancellationToken token, bool overwrite = true) =>
            Task.FromException(new IOException("definition save failed"));
    }
    private sealed class DelayedDefinitionStore(IUserDefinitionStore inner) : IUserDefinitionStore
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<GameContentDefinition>> LoadDefinitionsAsync(CancellationToken token) => inner.LoadDefinitionsAsync(token);
        public async Task SaveDefinitionAsync(GameContentDefinition definition, CancellationToken token, bool overwrite = true)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(token);
            await inner.SaveDefinitionAsync(definition, token, overwrite);
        }
    }
    private sealed class SteamSearchHandler(string response) : HttpMessageHandler
    {
        public Uri? LastRequest { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(response) });
        }
    }
    private sealed class FailingSearchHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));
    }
    private sealed class NumberSpacingSearchHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var query = Uri.UnescapeDataString(request.RequestUri!.Query);
            var json = query.Contains("生化危机 2", StringComparison.Ordinal)
                ? """{"items":[{"id":883710,"name":"Resident Evil 2"}]}"""
                : """{"items":[]}""";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }
    private sealed class FailingQueueEngine : ISyncEngine
    {
        private int _queued;
        public Task QueueAsync(SyncJob job, CancellationToken cancellationToken)
        {
            if (_queued++ > 0) throw new InvalidOperationException("queue unavailable");
            return Task.CompletedTask;
        }
        public Task<SyncComparison> CompareAsync(SyncJob job, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RestoreSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ResolveConflictsAsync(ConflictResolutionPlan plan, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class RecordingRestoreEngine : ISyncEngine
    {
        public Guid? RestoredId { get; private set; }
        public Task QueueAsync(SyncJob job, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<SyncComparison> CompareAsync(SyncJob job, CancellationToken cancellationToken) => Task.FromResult(new SyncComparison { Entries = [] });
        public Task RestoreSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken) { RestoredId = snapshotId; return Task.CompletedTask; }
        public Task ResolveConflictsAsync(ConflictResolutionPlan plan, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class DelayedComparisonEngine : ISyncEngine
    {
        private int _calls;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<SyncComparison> CompareAsync(SyncJob job, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) == 1) { Started.TrySetResult(); await Release.Task; }
            return new SyncComparison { Entries = [] };
        }
        public Task QueueAsync(SyncJob job, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RestoreSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResolveConflictsAsync(ConflictResolutionPlan plan, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class Source(string id, IReadOnlyList<DiscoveredGame> games) : ISourceProvider
    {
        public string ProviderId => id; public string DisplayName => id;
        public Task<IReadOnlyList<DiscoveredGame>> ScanAsync(CancellationToken cancellationToken) => Task.FromResult(games);
        public IReadOnlyDictionary<string, string> ResolveVariables(GameInstance instance) => new Dictionary<string, string> { ["GAME_INSTALL_DIR"] = instance.InstallDirectory ?? "" };
    }
    private sealed class DelayedHistory(ISyncHistoryRepository inner) : ISyncHistoryRepository
    {
        public Guid? DelayGame { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task AddRecordAsync(SyncRecord record, CancellationToken token) => inner.AddRecordAsync(record, token);
        public Task<IReadOnlyList<SyncRecord>> ListRecordsAsync(Guid gameId, CancellationToken token) => inner.ListRecordsAsync(gameId, token);
        public Task<IReadOnlyList<SyncRecord>> ListRecentRecordsAsync(int limit, CancellationToken token) => inner.ListRecentRecordsAsync(limit, token);
        public Task AddSnapshotAsync(Snapshot snapshot, CancellationToken token) => inner.AddSnapshotAsync(snapshot, token);
        public async Task<IReadOnlyList<Snapshot>> ListSnapshotsAsync(Guid gameId, CancellationToken token)
        {
            if (gameId == DelayGame)
            {
                Started.TrySetResult();
                await Release.Task.WaitAsync(token);
            }
            return await inner.ListSnapshotsAsync(gameId, token);
        }
        public Task<Snapshot?> GetSnapshotAsync(Guid id, CancellationToken token) => inner.GetSnapshotAsync(id, token);
    }
    private sealed class DelayedGames(IGameInstanceRepository inner) : IGameInstanceRepository
    {
        public Guid? DelayGame { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<GameInstance>> ListAsync(CancellationToken token) => inner.ListAsync(token);
        public async Task<GameInstance?> GetAsync(Guid id, CancellationToken token)
        {
            if (id == DelayGame)
            {
                Started.TrySetResult();
                await Release.Task.WaitAsync(token);
            }
            return await inner.GetAsync(id, token);
        }
        public Task UpsertAsync(GameInstance instance, CancellationToken token) => inner.UpsertAsync(instance, token);
        public Task DeleteAsync(Guid id, CancellationToken token) => inner.DeleteAsync(id, token);
    }
    private sealed class NoUpdate : IManifestUpdateSource
    {
        public Task<string?> TryFetchLatestManifestAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    }
    private sealed class Localization : ILocalizationService
    {
        public string CurrentLanguageTag => "en-US";
        public IReadOnlyList<LanguageOption> SupportedLanguages => new[] { new LanguageOption("en-US", "English") };
        public event EventHandler? LanguageChanged { add { } remove { } }
        public string GetString(string key) => key;
        public bool SetLanguage(string tag) => false;
    }
    private sealed class Paths(string root) : IAppPathService
    {
        public string GetAppDataRoot() => root;
        public string GetDatabasePath() => Path.Combine(root, "data.db");
        public string GetDefinitionsDirectory() => Path.Combine(root, "definitions");
        public string GetLogsDirectory() => Path.Combine(root, "logs");
        public string GetManifestCachePath() => Path.Combine(root, "manifest.yaml");
        public string GetSnapshotsDirectory() => Path.Combine(root, "snapshots");
        public string GetUiSettingsPath() => Path.Combine(root, "ui.json");
    }
}
