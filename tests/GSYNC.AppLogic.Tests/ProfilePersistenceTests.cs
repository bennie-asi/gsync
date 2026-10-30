using GSYNC.App.Infrastructure.Configuration;
using GSYNC.App.Infrastructure;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.App.Infrastructure.Wizard;
using GSYNC.App.ViewModels;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Manifest;
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
        details.StopStatusCheck();
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
        details.StopStatusCheck();
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
        Assert.Equal("解决冲突", UiFormat.DirectionText(SyncDirection.ResolveConflict, true));
    }

    [Fact]
    public async Task HistoryFiltering_ClearsHiddenSelection_AndReloadSelectsAVisibleRecord()
    {
        using var fixture = await Fixture.CreateAsync();
        await AddHistoryGameAsync(fixture, "Game One", DateTimeOffset.UtcNow.AddMinutes(-1));
        await AddHistoryGameAsync(fixture, "Game Two", DateTimeOffset.UtcNow);
        var viewModel = new HistoryPageViewModel(new Localization(), fixture.History, fixture.Games, fixture.Bindings, fixture.Engine);
        await viewModel.LoadAsync();
        Assert.Equal("Game Two", viewModel.DetailGameName);
        Assert.Equal("Game Two.zip", Assert.Single(viewModel.Snapshots).Name);

        viewModel.SearchText = "Game One";
        Assert.Equal("Game One", Assert.Single(viewModel.Entries).Game);
        Assert.Empty(viewModel.DetailGameName);
        Assert.Empty(viewModel.RecordDetails);
        Assert.Empty(viewModel.Snapshots);

        await viewModel.LoadAsync();
        Assert.Equal("Game One", viewModel.DetailGameName);
        Assert.Equal("Game One.zip", Assert.Single(viewModel.Snapshots).Name);
    }

    [Fact]
    public async Task HistorySelection_IgnoresLateDetailsForAPreviouslySelectedGame()
    {
        using var fixture = await Fixture.CreateAsync();
        var gameOne = await AddHistoryGameAsync(fixture, "Game One", DateTimeOffset.UtcNow.AddMinutes(-1));
        await AddHistoryGameAsync(fixture, "Game Two", DateTimeOffset.UtcNow);
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
        Assert.Equal("Game Two.zip", Assert.Single(viewModel.Snapshots).Name);
        Assert.True(viewModel.IsRecordSelected(second));
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
    public async Task SettingsRefresh_PreservesSelectedDefaultTargetEvenIfTheControlClearsItsSelection()
    {
        using var fixture = await Fixture.CreateAsync();
        var second = new SyncTargetConfig { Name = "Second Target", ProviderId = "local-folder",
            Settings = new() { ["rootPath"] = fixture.Folder("settings-target") } };
        await fixture.Targets().SaveTargetAsync(second);
        var viewModel = fixture.Settings();
        viewModel.SelectedDefaultTarget = Assert.Single(viewModel.AvailableTargets, option => option.Id == second.Id);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsPageViewModel.AvailableTargets)) viewModel.SelectedDefaultTarget = null;
        };
        await viewModel.LoadGamesAsync();
        Assert.Equal(second.Id, viewModel.SelectedDefaultTarget?.Id);
        Assert.Same(Assert.Single(viewModel.AvailableTargets, option => option.Id == second.Id), viewModel.SelectedDefaultTarget);
        viewModel.SaveSettings();
        Assert.Equal(second.Id, viewModel.SelectedDefaultTarget?.Id);
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
            var preview = Assert.Single(wizard.FinishSummary, row => row.Label == "Remote Namespace").Value;
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
            fixture.Engine = new(new SyncQueue(), fixture.Games, fixture.Bindings, fixture.History, fixture.Manifest,
                new[] { new Source("custom", Array.Empty<DiscoveredGame>()) },
                new[] { new LocalFolderStorageProvider(Microsoft.Extensions.Options.Options.Create(new LocalFolderOptions { RootPath = fixture.StoragePath })) },
                fixture._paths, new PathResolver(), new SystemVariableProvider(), NullLogger<SyncEngine>.Instance,
                userVariablesProvider: () => fixture._variables.AsDictionary());
            return fixture;
        }
        public GameDetailsViewModel Details(IGameInstanceRepository? games = null) => new(_localization, games ?? Games, Bindings, History, Manifest, Engine,
            new[] { new Source("custom", Array.Empty<DiscoveredGame>()) }, UserStore, new PathResolver(), new SystemVariableProvider(), _variables);
        public VariablesPageViewModel Variables() => new(_localization, new SystemVariableProvider(), new PathResolver(), _variables,
            Array.Empty<ISourceProvider>());
        public SyncTargetsPageViewModel Targets() => new(_localization, _targets,
            Array.Empty<IStorageProvider>(), Games, Bindings);
        public SettingsPageViewModel Settings() => new(_localization, new UiSettingsStore(_paths), _paths, _targets, Games);
        public LibraryPageViewModel Library() => new(_localization, Games, Bindings, History, Manifest, Engine, new SyncQueue(), _targets, new UiSettingsStore(_paths));
        public void SetAutoSnapshot(bool enabled) => new UiSettingsStore(_paths).Save(new AppUiSettings { AutoSnapshotBeforeDownload = enabled });
        public LocalizationService RealLocalization() => new(new UiSettingsStore(_paths));
        public Guid? DefaultTargetId() => _targets.GetDefault()?.Id;
        public AddGameWizardViewModel Wizard(ISourceProvider? source = null) => new(_localization,
            new[] { source ?? new Source("custom", Array.Empty<DiscoveredGame>()) }, Manifest, Games, Bindings, _targets,
            new AddGameMatchService(), new AddGamePathValidationService(new PathResolver(), new SystemVariableProvider(), () => _variables.AsDictionary()),
            new PathResolver(), new SystemVariableProvider(), Engine, UserStore, _profiles);
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
