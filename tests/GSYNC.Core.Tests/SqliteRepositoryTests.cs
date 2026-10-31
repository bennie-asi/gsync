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
    public async Task DatabaseInitializer_UpgradesVersionOneWithoutLosingData_AndIsIdempotent()
    {
        var initializer = new DatabaseInitializer(_connectionFactory, _appPaths);
        await initializer.InitializeAsync(CancellationToken.None);
        var repository = new GameInstanceRepository(_connectionFactory);
        var game = new GameInstance { GameId = "existing", DisplayName = "Existing save", SourceProviderId = "custom" };
        await repository.UpsertAsync(game, CancellationToken.None);
        await ExecuteSqlAsync("UPDATE Metadata SET Value = '1' WHERE Key = 'schema-version'; DROP INDEX IX_StorageBindings_GameInstanceId; DROP INDEX IX_SyncRecords_GameInstanceId; DROP INDEX IX_SyncRecords_StartedAtUtc; DROP INDEX IX_Snapshots_GameInstanceId; ALTER TABLE SyncRecords DROP COLUMN TargetId; ALTER TABLE SyncRecords DROP COLUMN TargetName; ALTER TABLE SyncRecords DROP COLUMN RemoteNamespace;");

        await initializer.InitializeAsync(CancellationToken.None);
        await initializer.InitializeAsync(CancellationToken.None);

        Assert.Equal("Existing save", (await repository.GetAsync(game.Id, CancellationToken.None))!.DisplayName);
        Assert.Equal(DatabaseInitializer.CurrentSchemaVersion.ToString(), await ScalarAsync("SELECT Value FROM Metadata WHERE Key = 'schema-version';"));
        Assert.Equal(4L, await ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name LIKE 'IX_%';"));
    }

    [Fact]
    public async Task DatabaseInitializer_UpgradesHistoryWithoutInventingOldTargets()
    {
        var initializer = new DatabaseInitializer(_connectionFactory, _appPaths);
        await initializer.InitializeAsync(CancellationToken.None);
        var repository = new SyncHistoryRepository(_connectionFactory);
        var gameId = Guid.NewGuid();
        await repository.AddRecordAsync(new SyncRecord { GameInstanceId = gameId, Direction = SyncDirection.Upload, Status = SyncJobStatus.Completed }, CancellationToken.None);
        await ExecuteSqlAsync("UPDATE Metadata SET Value = '2' WHERE Key = 'schema-version'; ALTER TABLE SyncRecords DROP COLUMN TargetId; ALTER TABLE SyncRecords DROP COLUMN TargetName; ALTER TABLE SyncRecords DROP COLUMN RemoteNamespace;");

        await initializer.InitializeAsync(CancellationToken.None);
        var old = Assert.Single(await repository.ListRecordsAsync(gameId, CancellationToken.None));
        Assert.Null(old.TargetId);
        Assert.Null(old.TargetName);
        Assert.Null(old.RemoteNamespace);
    }

    [Theory]
    [InlineData("999")]
    [InlineData("broken")]
    [InlineData("-1")]
    public async Task DatabaseInitializer_RejectsUnsupportedVersionsWithoutChangingThem(string version)
    {
        var initializer = new DatabaseInitializer(_connectionFactory, _appPaths);
        await initializer.InitializeAsync(CancellationToken.None);
        await using (var connection = _connectionFactory.CreateConnection())
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Metadata SET Value = $value WHERE Key = 'schema-version';";
            command.Parameters.AddWithValue("$value", version);
            await command.ExecuteNonQueryAsync();
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => initializer.InitializeAsync(CancellationToken.None));
        Assert.Equal(version, await ScalarAsync("SELECT Value FROM Metadata WHERE Key = 'schema-version';"));
    }

    [Fact]
    public async Task DatabaseInitializer_FailedMigrationRollsBackBothSchemaAndVersion()
    {
        await ExecuteSqlAsync("CREATE TABLE StorageBindings (Id TEXT);");
        var initializer = new DatabaseInitializer(_connectionFactory, _appPaths);
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => initializer.InitializeAsync(CancellationToken.None));
        Assert.Equal(0L, await ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Metadata', 'GameInstances');"));
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task GameProfileCreation_RollsBackBothRows_WhenBindingWriteFails()
    {
        await new DatabaseInitializer(_connectionFactory, _appPaths).InitializeAsync(CancellationToken.None);
        await ExecuteSqlAsync("CREATE TRIGGER RejectBindings BEFORE INSERT ON StorageBindings BEGIN SELECT RAISE(ABORT, 'binding rejected'); END;");
        var game = new GameInstance { GameId = "profile", DisplayName = "Profile", SourceProviderId = "custom" };
        var binding = new StorageBinding { GameInstanceId = game.Id, StorageProviderId = "local-folder", RemoteNamespace = "profile" };
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => new GameProfileRepository(_connectionFactory).CreateAsync(game, binding, CancellationToken.None));
        Assert.Empty(await new GameInstanceRepository(_connectionFactory).ListAsync(CancellationToken.None));
        Assert.Empty(await new StorageBindingRepository(_connectionFactory).ListByGameInstanceAsync(game.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GameProfileCreation_RejectsADuplicate_WithoutReplacingTheExistingBinding()
    {
        await new DatabaseInitializer(_connectionFactory, _appPaths).InitializeAsync(CancellationToken.None);
        var profiles = new GameProfileRepository(_connectionFactory);
        var first = new GameInstance { GameId = "profile", DisplayName = "Profile", SourceProviderId = "custom" };
        var binding = new StorageBinding { GameInstanceId = first.Id, StorageProviderId = "local-folder", RemoteNamespace = "original" };
        await profiles.CreateAsync(first, binding, CancellationToken.None);
        var duplicate = new GameInstance { GameId = "profile", DisplayName = "Profile", SourceProviderId = "custom" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => profiles.CreateAsync(duplicate,
            new StorageBinding { GameInstanceId = duplicate.Id, StorageProviderId = "local-folder", RemoteNamespace = "replacement" }, CancellationToken.None));
        Assert.Single(await new GameInstanceRepository(_connectionFactory).ListAsync(CancellationToken.None));
        Assert.Equal("original", Assert.Single(await new StorageBindingRepository(_connectionFactory).ListByGameInstanceAsync(first.Id, CancellationToken.None)).RemoteNamespace);
    }

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
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
        Assert.Equal("C:/Games/GameA", loadedInstance.Variables["game_install_dir"]);

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
        Assert.Equal("D:/SyncRoot", loadedBinding.Settings["ROOTPATH"]);
    }

    [Fact]
    public async Task SyncHistoryRepository_RoundTripsRecordsAndSnapshots()
    {
        var initializer = new DatabaseInitializer(_connectionFactory, _appPaths);
        await initializer.InitializeAsync(CancellationToken.None);

        var repository = new SyncHistoryRepository(_connectionFactory);
        var gameInstanceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
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
            TargetId = targetId,
            TargetName = "Original target",
            RemoteNamespace = "games/game-a",
        }, CancellationToken.None);

        var snapshots = await repository.ListSnapshotsAsync(gameInstanceId, CancellationToken.None);
        var records = await repository.ListRecordsAsync(gameInstanceId, CancellationToken.None);

        Assert.Single(snapshots);
        Assert.Single(records);
        Assert.Equal(snapshot.Id, records[0].SnapshotId);
        Assert.Equal(SyncDirection.Download, records[0].Direction);
        Assert.Equal(targetId, records[0].TargetId);
        Assert.Equal("Original target", records[0].TargetName);
        Assert.Equal("games/game-a", records[0].RemoteNamespace);
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

        public string GetUiSettingsPath() => Path.Combine(_rootPath, "ui-settings.json");
    }
}
