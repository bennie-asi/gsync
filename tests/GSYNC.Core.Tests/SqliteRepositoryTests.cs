using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using GSYNC.Data.Repositories;
using GSYNC.Data.Services;
using Xunit;

namespace GSYNC.Core.Tests;

public sealed class SqliteRepositoryTests : IDisposable
{
    private readonly string _rootPath;
    private readonly IAppPathService _appPaths;
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteRepositoryTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "gsync-data-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _appPaths = new TestAppPathService(_rootPath);
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_appPaths.GetDatabasePath()}");
    }

    [Fact]
    public async Task DatabaseInitializer_CreatesSchema()
    {
        var initializer = new DatabaseInitializer(_connectionFactory, _appPaths);

        await initializer.InitializeAsync(CancellationToken.None);

        Assert.True(File.Exists(_appPaths.GetDatabasePath()));
    }

    [Fact]
    public async Task GameInstanceAndBindingRepositories_RoundTripEntities()
    {
        var initializer = new DatabaseInitializer(_connectionFactory, _appPaths);
        await initializer.InitializeAsync(CancellationToken.None);

        var gameInstanceRepository = new GameInstanceRepository(_connectionFactory);
        var bindingRepository = new StorageBindingRepository(_connectionFactory);

        var instance = new GameInstance
        {
            GameId = "game-a",
            DisplayName = "Game A",
            SourceProviderId = "steam",
            InstallDirectory = "C:/Games/GameA",
            PlatformInstanceId = "1234",
            Variables = new Dictionary<string, string>
            {
                ["GAME_INSTALL_DIR"] = "C:/Games/GameA",
            },
        };

        await gameInstanceRepository.UpsertAsync(instance, CancellationToken.None);
        var loadedInstance = await gameInstanceRepository.GetAsync(instance.Id, CancellationToken.None);

        Assert.NotNull(loadedInstance);
        Assert.Equal("Game A", loadedInstance!.DisplayName);
        Assert.Equal("C:/Games/GameA", loadedInstance.InstallDirectory);

        var binding = new StorageBinding
        {
            GameInstanceId = instance.Id,
            StorageProviderId = "local-folder",
            RemoteNamespace = "games/game-a",
            Settings = new Dictionary<string, string>
            {
                ["rootPath"] = "D:/SyncRoot",
            },
            ContentSelections = new[]
            {
                new ContentSelection { ContentId = "default", IsEnabled = true },
            },
        };

        await bindingRepository.UpsertAsync(binding, CancellationToken.None);
        var loadedBindings = await bindingRepository.ListByGameInstanceAsync(instance.Id, CancellationToken.None);

        var loadedBinding = Assert.Single(loadedBindings);
        Assert.Equal("local-folder", loadedBinding.StorageProviderId);
        Assert.Equal("games/game-a", loadedBinding.RemoteNamespace);
    }

    [Fact]
    public async Task SyncHistoryRepository_RoundTripsRecordsAndSnapshots()
    {
        var initializer = new DatabaseInitializer(_connectionFactory, _appPaths);
        await initializer.InitializeAsync(CancellationToken.None);

        var repository = new SyncHistoryRepository(_connectionFactory);
        var gameInstanceId = Guid.NewGuid();
        var snapshot = new Snapshot
        {
            GameInstanceId = gameInstanceId,
            ArchivePath = Path.Combine(_rootPath, "snapshot.zip"),
            FileCount = 3,
            TotalBytes = 100,
        };

        await repository.AddSnapshotAsync(snapshot, CancellationToken.None);
        await repository.AddRecordAsync(new SyncRecord
        {
            GameInstanceId = gameInstanceId,
            SnapshotId = snapshot.Id,
            Direction = SyncDirection.Download,
            Status = SyncJobStatus.Completed,
            Summary = "ok",
            ProcessedFiles = 3,
            TotalFiles = 3,
        }, CancellationToken.None);

        var snapshots = await repository.ListSnapshotsAsync(gameInstanceId, CancellationToken.None);
        var records = await repository.ListRecordsAsync(gameInstanceId, CancellationToken.None);

        Assert.Single(snapshots);
        Assert.Single(records);
        Assert.Equal(snapshot.Id, records[0].SnapshotId);
        Assert.Equal(SyncDirection.Download, records[0].Direction);
    }

    public void Dispose()
    {
        try
        {
            var databasePath = _appPaths.GetDatabasePath();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
        catch
        {
        }

        try
        {
            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed class TestAppPathService : IAppPathService
    {
        private readonly string _rootPath;

        public TestAppPathService(string rootPath)
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
