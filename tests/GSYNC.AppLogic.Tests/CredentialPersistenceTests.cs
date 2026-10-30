using System.Text;
using System.Text.Json;
using GSYNC.App.Infrastructure.Configuration;
using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Models;
using GSYNC.Data.Repositories;
using GSYNC.Data.Services;
using Xunit;

namespace GSYNC.AppLogic.Tests;

public sealed class CredentialPersistenceTests : IDisposable
{
    private readonly TestPaths _paths = new();
    private readonly MemoryCredentials _vault = new();

    [Fact]
    public void LegacyTargetsAreMigrated_AndEditsNeverPersistPlaintext()
    {
        var credentials = new CredentialSettingsService(_vault);
        var target = new SyncTargetConfig { Name = "DAV", ProviderId = "webdav", Settings = new()
        { ["baseUrl"] = "https://example.test/dav", ["username"] = "player", ["password"] = "unique-old-secret" } };
        var path = Path.Combine(_paths.Root, "sync-targets.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { targets = new[] { target } }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var store = new SyncTargetStore(_paths, credentials);
        Assert.False(Assert.Single(store.List()).Settings.ContainsKey("password"));
        Assert.DoesNotContain("unique-old-secret", File.ReadAllText(path));
        var editing = store.GetForEditing(target.Id)!;
        Assert.Equal("unique-old-secret", editing.Settings["password"]);
        editing.Settings["password"] = "unique-new-secret";
        store.Upsert(editing);
        Assert.DoesNotContain("unique-new-secret", File.ReadAllText(path));
        Assert.Equal("unique-new-secret", new SyncTargetStore(_paths, credentials).GetForEditing(target.Id)!.Settings["password"]);
    }

    [Fact]
    public async Task LegacyBindingSecretsAreRemovedFromDatabase_AndNewBindingsStoreOnlyReferences()
    {
        var connections = new SqliteConnectionFactory($"Data Source={_paths.GetDatabasePath()};Pooling=False");
        await new DatabaseInitializer(connections, _paths).InitializeAsync(CancellationToken.None);
        var credentials = new CredentialSettingsService(_vault);
        var repository = new StorageBindingRepository(connections, credentials);
        var binding = new StorageBinding { GameInstanceId = Guid.NewGuid(), StorageProviderId = "webdav", RemoteNamespace = "game", Settings = new Dictionary<string, string>() };
        await repository.UpsertAsync(binding, CancellationToken.None);
        await using (var connection = connections.CreateConnection())
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE StorageBindings SET SettingsJson = '{\"password\":\"legacy-db-secret\",\"baseUrl\":\"https://example.test/dav\"}';";
            await command.ExecuteNonQueryAsync();
        }
        await new CredentialMigrationService(connections, credentials).MigrateBindingsAsync(CancellationToken.None);
        var loaded = (await repository.GetAsync(binding.Id, CancellationToken.None))!;
        Assert.False(loaded.Settings.ContainsKey("password"));
        Assert.Equal("legacy-db-secret", credentials.Resolve(loaded.Settings)["password"]);
        Assert.DoesNotContain("legacy-db-secret", Encoding.UTF8.GetString(File.ReadAllBytes(_paths.GetDatabasePath())));
        await repository.UpsertAsync(new StorageBinding { GameInstanceId = Guid.NewGuid(), StorageProviderId = "webdav", RemoteNamespace = "new", Settings = new Dictionary<string, string> { ["password"] = "new-db-secret" } }, CancellationToken.None);
        Assert.DoesNotContain("new-db-secret", Encoding.UTF8.GetString(File.ReadAllBytes(_paths.GetDatabasePath())));
    }

    [Fact]
    public void MissingCredentialsCanBeReenteredWithoutLosingTheTarget()
    {
        var credentials = new CredentialSettingsService(_vault);
        var store = new SyncTargetStore(_paths, credentials);
        var target = new SyncTargetConfig { Name = "DAV", ProviderId = "webdav", Settings = new()
        { ["baseUrl"] = "https://example.test/dav", ["username"] = "player", ["password"] = "old-secret" } };
        store.Upsert(target);
        _vault.Forget(store.Get(target.Id)!.Settings[CredentialSettingsService.ReferenceKey]);
        var editing = store.GetForEditing(target.Id)!;
        Assert.True(editing.CredentialsUnavailable);
        editing.Settings["username"] = "player";
        editing.Settings["password"] = "replacement-secret";
        store.Upsert(editing);
        Assert.Equal("replacement-secret", store.ResolveSettings(store.Get(target.Id)!.Settings)["password"]);
    }

    [Fact]
    public void SystemProtectedCredentialsRoundTrip_WithoutPlaintextOnDisk()
    {
        if (!OperatingSystem.IsWindows()) return;
        var vault = new ProtectedFileCredentialStore(_paths);
        var reference = vault.Store(new Dictionary<string, string> { ["password"] = "test-dpapi-secret-1234" });
        var file = Assert.Single(Directory.GetFiles(Path.Combine(_paths.Root, "credentials")));
        Assert.DoesNotContain("test-dpapi-secret-1234", Encoding.UTF8.GetString(File.ReadAllBytes(file)));
        Assert.Equal("test-dpapi-secret-1234", vault.Read(reference)["password"]);
        Assert.Throws<InvalidDataException>(() => vault.Read("../escape"));
    }

    public void Dispose() => Directory.Delete(_paths.Root, recursive: true);

    private sealed class MemoryCredentials : ICredentialStore
    {
        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _secrets = new();
        public string Store(IReadOnlyDictionary<string, string> secrets) { var id = Guid.NewGuid().ToString("N"); _secrets[id] = new Dictionary<string, string>(secrets); return id; }
        public IReadOnlyDictionary<string, string> Read(string reference) => _secrets.TryGetValue(reference, out var secrets) ? secrets : throw new FileNotFoundException("Credential missing");
        public void Forget(string reference) => _secrets.Remove(reference);
    }

    private sealed class TestPaths : IAppPathService
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "gsync-credential-tests", Guid.NewGuid().ToString("N"));
        public TestPaths() => Directory.CreateDirectory(Root);
        public string GetAppDataRoot() => Root;
        public string GetDatabasePath() => Path.Combine(Root, "data.db");
        public string GetDefinitionsDirectory() => Path.Combine(Root, "definitions");
        public string GetLogsDirectory() => Path.Combine(Root, "logs");
        public string GetManifestCachePath() => Path.Combine(Root, "manifest.yaml");
        public string GetSnapshotsDirectory() => Path.Combine(Root, "snapshots");
        public string GetUiSettingsPath() => Path.Combine(Root, "ui.json");
    }
}
