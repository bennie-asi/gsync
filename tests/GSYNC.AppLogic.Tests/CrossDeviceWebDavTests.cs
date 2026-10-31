using System.Net;
using System.Net.Http.Headers;
using System.Security;
using GSYNC.App.Infrastructure.Configuration;
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
using Microsoft.Extensions.Options;
using Xunit;

namespace GSYNC.AppLogic.Tests;

public sealed class CrossDeviceWebDavTests
{
    [Fact]
    public async Task TwoIndependentProfilesLinkExistingSaveAndSyncBothWays()
    {
        var root = Path.Combine(Path.GetTempPath(), "gsync-cross-device", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var server = new DavServer();
            var provider = new WebDavStorageProvider(new HttpClient(server), Options.Create(new WebDavOptions { BaseUrl = "https://example.test/dav" }));
            var first = await Device.CreateAsync(Path.Combine(root, "pc-a"), provider);
            var second = await Device.CreateAsync(Path.Combine(root, "pc-b"), provider);
            var firstSaves = Path.Combine(root, "pc-a", "game-saves");
            var secondSaves = Path.Combine(root, "pc-b", "other-location");
            Directory.CreateDirectory(firstSaves);
            Directory.CreateDirectory(secondSaves);
            await File.WriteAllTextAsync(Path.Combine(firstSaves, "slot.sav"), "first-device");

            var firstWizard = first.Wizard();
            Prepare(firstWizard, "Resident Evil Four", firstSaves);
            Assert.Null(await firstWizard.CreateProfileAsync());
            var firstId = firstWizard.LastCreatedInstanceId!.Value;
            var firstDetails = first.Details();
            await firstDetails.LoadAsync(firstId);
            Assert.Null(await firstDetails.AddCustomContentItemAsync("default", firstSaves));
            Assert.Null(await firstDetails.UpdateContentSelectionAsync(firstDetails.ContentItems.Single(item => item.Name == "saves"), false));
            await firstDetails.StopStatusCheckAsync();
            var originalBinding = Assert.Single(await first.Bindings.ListByGameInstanceAsync(firstId, default));
            Assert.Equal(SyncJobStatus.Completed, await first.RunAsync(firstId, SyncDirection.Upload));

            // A differently named, independently configured game would normally use a different namespace.
            var secondWizard = second.Wizard();
            Prepare(secondWizard, "生化危机4", secondSaves);
            Assert.NotEqual(originalBinding.RemoteNamespace, secondWizard.RemoteNamespaceText);
            var settings = new Dictionary<string, string> { ["baseUrl"] = "https://example.test/dav" };
            var targetFolders = await provider.BrowseDirectoriesAsync(settings, "", default);
            Assert.Contains(targetFolders, entry => entry.Path == originalBinding.RemoteNamespace);
            var remoteRoots = await RemoteContentLayout.ListRootsAsync(provider, originalBinding.RemoteNamespace, default);
            Assert.Equal("default", Assert.Single(remoteRoots));
            var localRoot = Assert.Single(secondWizard.GetLinkableRoots());
            secondWizard.LinkExistingRemote(second.TargetId, originalBinding.RemoteNamespace,
                [new ContentRootLink(localRoot.ContentId, localRoot.Template, remoteRoots[0])]);
            Assert.Null(await secondWizard.CreateProfileAsync());
            var secondId = secondWizard.LastCreatedInstanceId!.Value;
            var secondDetails = second.Details();
            await secondDetails.LoadAsync(secondId);
            var localItem = Assert.Single(secondDetails.ContentItems);
            Assert.Null(await secondDetails.UpdateContentPathAsync(localItem, secondSaves));
            Assert.Null(await secondDetails.UpdateContentSelectionAsync(localItem, false));
            Assert.Null(await secondDetails.UpdateContentSelectionAsync(localItem, true));
            await secondDetails.StopStatusCheckAsync();
            var persistedBinding = Assert.Single(await second.Bindings.ListByGameInstanceAsync(secondId, default));
            Assert.Equal("default", Assert.Single(persistedBinding.ContentSelections).RemoteRoot(localItem.PathTemplate));
            var comparison = await second.CompareAsync(secondId);
            Assert.Equal(SyncChangeKind.AddedRemotely, Assert.Single(comparison.Entries).ChangeKind);
            Assert.Equal(SyncJobStatus.Completed, await second.RunAsync(secondId, SyncDirection.Download));
            Assert.Equal("first-device", await File.ReadAllTextAsync(Path.Combine(secondSaves, "slot.sav")));

            await File.WriteAllTextAsync(Path.Combine(secondSaves, "slot.sav"), "second-device");
            // The normal protected upload cannot silently overwrite a different remote version.
            Assert.Equal(SyncJobStatus.Failed, await second.RunAsync(secondId, SyncDirection.Upload, protect: true));
            comparison = await second.CompareAsync(secondId);
            var difference = Assert.Single(comparison.Entries);
            Assert.Equal(SyncChangeKind.Conflict, difference.ChangeKind);
            await second.Engine.ResolveConflictsAsync(new ConflictResolutionPlan
            {
                GameInstanceId = secondId,
                ExpectedConfigurationFingerprint = comparison.ConfigurationFingerprint,
                Decisions = [new ConflictResolutionDecision { RelativePath = difference.RelativePath, Action = ConflictResolutionAction.KeepLocal,
                    VerifyComparedVersions = true, ExpectedLocalHash = difference.LocalHash, ExpectedRemoteHash = difference.RemoteHash }],
            }, default);
            Assert.Equal(SyncChangeKind.Conflict, Assert.Single((await first.CompareAsync(firstId)).Entries).ChangeKind);
            Assert.Equal(SyncJobStatus.Completed, await first.RunAsync(firstId, SyncDirection.Download));
            Assert.Equal("second-device", await File.ReadAllTextAsync(Path.Combine(firstSaves, "slot.sav")));

            // Relinking an already-added game persists the same mapping, without changing the remote save.
            var oldRoot = Assert.Single(firstDetails.GetLinkableRoots());
            Assert.Null(await firstDetails.UpdateDestinationAsync(first.TargetId, originalBinding.RemoteNamespace, default,
                [new ContentRootLink(oldRoot.ContentId, oldRoot.Template, "default")]));
            await firstDetails.StopStatusCheckAsync();
            Assert.Equal(SyncChangeKind.Unchanged, Assert.Single((await first.CompareAsync(firstId)).Entries).ChangeKind);

            var unrelated = second.Wizard();
            Prepare(unrelated, "Another game", Path.Combine(root, "another-game"));
            Assert.NotEqual(originalBinding.RemoteNamespace, unrelated.RemoteNamespaceText);
            var otherTarget = second.AddTarget();
            Assert.Null(await secondDetails.UpdateDestinationAsync(otherTarget, originalBinding.RemoteNamespace));
            await secondDetails.StopStatusCheckAsync();
            Assert.Equal(SyncChangeKind.AddedLocally, Assert.Single((await second.CompareAsync(secondId)).Entries).ChangeKind);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static void Prepare(AddGameWizardViewModel wizard, string name, string path)
    {
        wizard.SwitchToManualSetup();
        wizard.SetManualGame(name, path);
        wizard.CurrentStep = 2;
        while (wizard.CurrentStep < 6) wizard.GoNext();
    }

    private sealed class Device
    {
        private Paths _paths = null!;
        private SyncTargetStore _targets = null!;
        private ManifestService _manifest = null!;
        private YamlUserDefinitionStore _definitions = null!;
        private GameInstanceRepository _games = null!;
        private GameProfileRepository _profiles = null!;
        private UserVariablesStore _variables = null!;
        private SyncHistoryRepository _history = null!;
        private readonly Source _source = new();
        private readonly SyncQueue _queue = new();
        public StorageBindingRepository Bindings { get; private set; } = null!;
        public SyncEngine Engine { get; private set; } = null!;
        public Guid TargetId => _targets.GetDefault()!.Id;

        public static async Task<Device> CreateAsync(string root, IStorageProvider provider)
        {
            var device = new Device { _paths = new Paths(root) };
            var factory = new SqliteConnectionFactory($"Data Source={device._paths.GetDatabasePath()};Pooling=False");
            await new DatabaseInitializer(factory, device._paths).InitializeAsync(default);
            device._games = new(factory); device.Bindings = new(factory); device._profiles = new(factory);
            device._history = new(factory);
            device._definitions = new(device._paths.GetDefinitionsDirectory(), NullLogger<YamlUserDefinitionStore>.Instance);
            device._manifest = new(device._definitions, new DefinitionOverrideStore(factory), new CommunityDefinitionStore(factory),
                new LudusaviManifestParser(NullLogger<LudusaviManifestParser>.Instance), new NoUpdate(), NullLogger<ManifestService>.Instance);
            device._variables = new(device._paths);
            device._targets = new(device._paths);
            var target = new SyncTargetConfig { Name = "Shared WebDAV", ProviderId = "webdav", Settings = new() { ["baseUrl"] = "https://example.test/dav" } };
            device._targets.Upsert(target); device._targets.SetDefault(target.Id);
            device.Engine = new(device._queue, device._games, device.Bindings, device._history, device._manifest, [device._source], [provider],
                device._paths, new PathResolver(), new SystemVariableProvider(), NullLogger<SyncEngine>.Instance);
            return device;
        }

        public AddGameWizardViewModel Wizard() => new(new Localization(), [_source], _manifest, _games, Bindings, _targets,
            new AddGameMatchService(), new AddGamePathValidationService(new PathResolver(), new SystemVariableProvider(), () => _variables.AsDictionary()),
            new PathResolver(), new SystemVariableProvider(), Engine, _definitions, _profiles);
        public GameDetailsViewModel Details() => new(new Localization(), _games, Bindings, _history, _manifest, Engine, [_source], _definitions,
            new PathResolver(), new SystemVariableProvider(), _variables, _targets, Engine);
        public Guid AddTarget()
        {
            var target = new SyncTargetConfig { Name = "Other WebDAV", ProviderId = "webdav", Settings = new() { ["baseUrl"] = "https://example.test/dav" } };
            _targets.Upsert(target);
            return target.Id;
        }
        public Task<SyncComparison> CompareAsync(Guid id) => Engine.CompareAsync(new SyncJob { GameInstanceId = id, Direction = SyncDirection.Compare }, default);
        public async Task<SyncJobStatus> RunAsync(Guid id, SyncDirection direction, bool protect = false)
        {
            using var stop = new CancellationTokenSource();
            var worker = Engine.ProcessQueuedJobsAsync(stop.Token);
            var job = new SyncJob { GameInstanceId = id, Direction = direction, ProtectRemoteChanges = protect };
            await Engine.QueueAsync(job, default);
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (job.Status is SyncJobStatus.Queued or SyncJobStatus.Running)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException();
                await Task.Delay(10);
            }
            stop.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
            return job.Status;
        }
    }

    private sealed class DavServer : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _revisions = new(StringComparer.Ordinal);
        private readonly HashSet<string> _directories = new(StringComparer.Ordinal) { "" };
        private int _nextRevision;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath)["/dav".Length..].Trim('/');
            switch (request.Method.Method)
            {
                case "MKCOL": _directories.Add(path); return new(HttpStatusCode.Created);
                case "GET":
                    if (!_files.TryGetValue(path, out var data)) return new(HttpStatusCode.NotFound);
                    var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
                    response.Headers.ETag = new EntityTagHeaderValue($"\"{_revisions[path]}\"");
                    return response;
                case "PUT":
                    if (request.Headers.IfNoneMatch.Count > 0 && _files.ContainsKey(path)) return new(HttpStatusCode.PreconditionFailed);
                    if (request.Headers.IfMatch.Count > 0 && (!_revisions.TryGetValue(path, out var revision) || request.Headers.IfMatch.Single().Tag != $"\"{revision}\""))
                        return new(HttpStatusCode.PreconditionFailed);
                    _files[path] = await request.Content!.ReadAsByteArrayAsync(token); _revisions[path] = ++_nextRevision;
                    return new(HttpStatusCode.Created);
                case "MOVE":
                    var destination = Uri.UnescapeDataString(new Uri(request.Headers.GetValues("Destination").Single()).AbsolutePath)["/dav".Length..].Trim('/');
                    _files[destination] = _files[path]; _revisions[destination] = ++_nextRevision; _files.Remove(path); _revisions.Remove(path);
                    return new(HttpStatusCode.Created);
                case "DELETE": _files.Remove(path); _revisions.Remove(path); return new(HttpStatusCode.NoContent);
                case "PROPFIND":
                    if (!_directories.Contains(path)) return new(HttpStatusCode.NotFound);
                    var prefix = path.Length == 0 ? "" : path + "/";
                    var children = _directories.Where(value => value.StartsWith(prefix, StringComparison.Ordinal) && value.Length > prefix.Length && !value[prefix.Length..].Contains('/'))
                        .Select(value => (Path: value, Directory: true, Size: 0L))
                        .Concat(_files.Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal) && !pair.Key[prefix.Length..].Contains('/'))
                            .Select(pair => (Path: pair.Key, Directory: false, Size: (long)pair.Value.Length)));
                    static string Element(string value, bool directory, long size) =>
                        $"<d:response><d:href>{SecurityElement.Escape("/dav/" + string.Join('/', value.Split('/').Select(Uri.EscapeDataString)) + (directory ? "/" : ""))}</d:href><d:propstat><d:prop><d:resourcetype>{(directory ? "<d:collection/>" : "")}</d:resourcetype><d:getcontentlength>{size}</d:getcontentlength></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>";
                    var xml = "<d:multistatus xmlns:d='DAV:'>" + Element(path, true, 0) + string.Concat(children.Select(value => Element(value.Path, value.Directory, value.Size))) + "</d:multistatus>";
                    return new((HttpStatusCode)207) { Content = new StringContent(xml) };
                default: throw new NotSupportedException(request.Method.Method);
            }
        }
    }

    private sealed class Source : ISourceProvider
    {
        public string ProviderId => "custom"; public string DisplayName => "Custom";
        public Task<IReadOnlyList<DiscoveredGame>> ScanAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<DiscoveredGame>>([]);
        public IReadOnlyDictionary<string, string> ResolveVariables(GameInstance instance) => new Dictionary<string, string>();
    }
    private sealed class NoUpdate : IManifestUpdateSource
    {
        public Task<string?> TryFetchLatestManifestAsync(CancellationToken token) => Task.FromResult<string?>(null);
    }
    private sealed class Localization : ILocalizationService
    {
        public string CurrentLanguageTag => "en-US";
        public IReadOnlyList<LanguageOption> SupportedLanguages => [new("en-US", "English")];
        public event EventHandler? LanguageChanged { add { } remove { } }
        public string GetString(string key) => key;
        public bool SetLanguage(string languageTag) => false;
    }
    private sealed class Paths(string root) : IAppPathService
    {
        public string GetAppDataRoot() => root;
        public string GetDatabasePath() { Directory.CreateDirectory(root); return Path.Combine(root, "gsync.db"); }
        public string GetDefinitionsDirectory() => Path.Combine(root, "definitions");
        public string GetLogsDirectory() => Path.Combine(root, "logs");
        public string GetManifestCachePath() => Path.Combine(root, "manifest.yaml");
        public string GetSnapshotsDirectory() => Path.Combine(root, "snapshots");
        public string GetUiSettingsPath() => Path.Combine(root, "settings.json");
    }
}
