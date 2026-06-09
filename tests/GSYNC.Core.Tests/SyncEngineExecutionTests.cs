using GSYNC.Core.Abstractions;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Services.Sync;
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
    public async Task CompareJob_WritesCompareRecord()
    {
        var fixture = new SyncEngineFixture();
        var localFile = Path.Combine(fixture.RootPath, "game-a", "save1.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(localFile)!);
        await File.WriteAllTextAsync(localFile, "same-data");
        fixture.StorageProvider.RemoteFiles["games/game-a/default/save1.sav"] = "same-data"u8.ToArray();

        var job = new SyncJob
        {
            GameInstanceId = fixture.GameInstance.Id,
            Direction = SyncDirection.Compare,
        };

        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, job, cts);

        Assert.Single(fixture.SyncHistoryRepository.Records);
        Assert.Equal(SyncDirection.Compare, fixture.SyncHistoryRepository.Records[0].Direction);
        Assert.Equal(SyncJobStatus.Completed, fixture.SyncHistoryRepository.Records[0].Status);
    }

    [Fact]
    public async Task DownloadJob_WhenCancelled_WritesCancelledRecord()
    {
        var fixture = new SyncEngineFixture(delayDownloads: true);
        fixture.StorageProvider.RemoteFiles["games/game-a/default/save1.sav"] = new byte[1024];
        using var jobCts = new CancellationTokenSource(10);

        var job = new SyncJob
        {
            GameInstanceId = fixture.GameInstance.Id,
            Direction = SyncDirection.Download,
            CancellationToken = jobCts.Token,
        };

        using var cts = new CancellationTokenSource();
        await RunQueuedJobAsync(fixture, job, cts);

        Assert.Single(fixture.SyncHistoryRepository.Records);
        Assert.Equal(SyncJobStatus.Cancelled, fixture.SyncHistoryRepository.Records[0].Status);
    }

    private static async Task RunQueuedJobAsync(SyncEngineFixture fixture, SyncJob job, CancellationTokenSource queueCts)
    {
        var worker = fixture.RunQueueAsync(queueCts.Token);
        await fixture.Queue.QueueAsync(job, CancellationToken.None);
        await fixture.WaitForRecordsAsync(1);
        queueCts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await worker);
    }

    private sealed class SyncEngineFixture
    {
        public SyncEngineFixture(bool delayDownloads = false)
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
            };

            Queue = new SyncQueue();
            StorageProvider = new FakeStorageProvider(delayDownloads);
            SyncHistoryRepository = new FakeSyncHistoryRepository();
            Engine = new SyncEngine(
                Queue,
                new FakeGameInstanceRepository(GameInstance),
                new FakeStorageBindingRepository(Binding),
                SyncHistoryRepository,
                new FakeManifestService(),
                new ISourceProvider[] { new FakeSourceProvider() },
                new IStorageProvider[] { StorageProvider },
                new FakeAppPathService(RootPath),
                new PathResolver(),
                new SystemVariableProvider(),
                NullLogger<SyncEngine>.Instance);
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
    }

    private sealed class FakeManifestService : IManifestService
    {
        private readonly GameContentDefinition _definition = new()
        {
            GameId = "game-a",
            DisplayName = "Game A",
            ContentItems = new[]
            {
                new ContentItem
                {
                    ContentId = "default",
                    Category = ContentCategory.Save,
                    PathTemplates = new[] { "%GAME_INSTALL_DIR%" },
                    DefaultEnabled = true,
                },
            },
        };

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

        public Task AddRecordAsync(SyncRecord record, CancellationToken cancellationToken)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SyncRecord>> ListRecordsAsync(Guid gameInstanceId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SyncRecord>>(Records.Where(r => r.GameInstanceId == gameInstanceId).ToArray());

        public Task AddSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken)
        {
            Snapshots.Add(snapshot);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Snapshot>> ListSnapshotsAsync(Guid gameInstanceId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Snapshot>>(Snapshots.Where(s => s.GameInstanceId == gameInstanceId).ToArray());
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

        public Dictionary<string, byte[]> RemoteFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> UploadedPaths { get; } = new();

        public Task<ConnectionResult> TestConnectionAsync(IReadOnlyDictionary<string, string> configuration, CancellationToken cancellationToken)
            => Task.FromResult(ConnectionResult.Success());

        public async Task UploadAsync(string remotePath, Stream content, CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream();
            await content.CopyToAsync(memory, cancellationToken);
            UploadedPaths.Add(remotePath);
            RemoteFiles[remotePath] = memory.ToArray();
        }

        public async Task<StorageDownloadResult> DownloadAsync(string remotePath, CancellationToken cancellationToken)
        {
            if (_delayDownloads)
            {
                await Task.Delay(100, cancellationToken);
            }

            var bytes = RemoteFiles[remotePath];
            return new StorageDownloadResult
            {
                Content = new MemoryStream(bytes),
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
                    Hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pair.Value)),
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
    }
}
