namespace GSYNC.Data.Entities;

internal static class SqliteSchema
{
    public const string CreateMetadata = """
        CREATE TABLE IF NOT EXISTS Metadata (
            Key TEXT NOT NULL PRIMARY KEY,
            Value TEXT NULL
        );
        """;

    public const string CreateGameInstances = """
        CREATE TABLE IF NOT EXISTS GameInstances (
            Id TEXT NOT NULL PRIMARY KEY,
            GameId TEXT NOT NULL,
            DisplayName TEXT NOT NULL,
            SourceProviderId TEXT NOT NULL,
            InstallDirectory TEXT NULL,
            PlatformInstanceId TEXT NULL,
            VariablesJson TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL
        );
        """;

    public const string CreateStorageBindings = """
        CREATE TABLE IF NOT EXISTS StorageBindings (
            Id TEXT NOT NULL PRIMARY KEY,
            GameInstanceId TEXT NOT NULL,
            StorageProviderId TEXT NOT NULL,
            RemoteNamespace TEXT NOT NULL,
            SettingsJson TEXT NOT NULL,
            ContentSelectionsJson TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL
        );
        """;

    public const string CreateSyncRecords = """
        CREATE TABLE IF NOT EXISTS SyncRecords (
            Id TEXT NOT NULL PRIMARY KEY,
            GameInstanceId TEXT NOT NULL,
            SnapshotId TEXT NULL,
            Direction TEXT NOT NULL,
            Status TEXT NOT NULL,
            StartedAtUtc TEXT NOT NULL,
            CompletedAtUtc TEXT NULL,
            Summary TEXT NULL,
            ErrorMessage TEXT NULL,
            ProcessedFiles INTEGER NOT NULL,
            TotalFiles INTEGER NOT NULL
        );
        """;

    public const string CreateSnapshots = """
        CREATE TABLE IF NOT EXISTS Snapshots (
            Id TEXT NOT NULL PRIMARY KEY,
            GameInstanceId TEXT NOT NULL,
            ArchivePath TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            FileCount INTEGER NOT NULL,
            TotalBytes INTEGER NOT NULL
        );
        """;

    public const string CreateCommunityDefinitions = """
        CREATE TABLE IF NOT EXISTS CommunityDefinitions (
            GameId TEXT NOT NULL PRIMARY KEY,
            DefinitionJson TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL
        );
        """;

    public const string CreateDefinitionOverrides = """
        CREATE TABLE IF NOT EXISTS DefinitionOverrides (
            GameId TEXT NOT NULL PRIMARY KEY,
            DefinitionJson TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL
        );
        """;
}
