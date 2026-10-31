using GSYNC.Core.Abstractions.Data;
using Microsoft.Data.Sqlite;

namespace GSYNC.Data.Services;

public sealed class DatabaseInitializer
{
    public const int CurrentSchemaVersion = 3;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly IAppPathService _appPathService;

    public DatabaseInitializer(SqliteConnectionFactory connectionFactory, IAppPathService appPathService)
    {
        _connectionFactory = connectionFactory;
        _appPathService = appPathService;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_appPathService.GetAppDataRoot());

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        var version = await ReadSchemaVersionAsync(connection, transaction, cancellationToken);
        if (version > CurrentSchemaVersion)
            throw new InvalidDataException($"This database uses schema {version}; this application supports up to {CurrentSchemaVersion}. Open it with a newer GSYNC version.");

        for (var next = version + 1; next <= CurrentSchemaVersion; next++)
        {
            var statements = next switch
            {
                1 => new[]
                {
                    Entities.SqliteSchema.CreateMetadata,
                    Entities.SqliteSchema.CreateGameInstances,
                    Entities.SqliteSchema.CreateStorageBindings,
                    Entities.SqliteSchema.CreateSyncRecords,
                    Entities.SqliteSchema.CreateSnapshots,
                    Entities.SqliteSchema.CreateCommunityDefinitions,
                    Entities.SqliteSchema.CreateDefinitionOverrides,
                },
                2 => new[]
                {
                    "CREATE INDEX IF NOT EXISTS IX_StorageBindings_GameInstanceId ON StorageBindings (GameInstanceId, CreatedAtUtc);",
                    "CREATE INDEX IF NOT EXISTS IX_SyncRecords_GameInstanceId ON SyncRecords (GameInstanceId, StartedAtUtc DESC);",
                    "CREATE INDEX IF NOT EXISTS IX_SyncRecords_StartedAtUtc ON SyncRecords (StartedAtUtc DESC);",
                    "CREATE INDEX IF NOT EXISTS IX_Snapshots_GameInstanceId ON Snapshots (GameInstanceId, CreatedAtUtc DESC);",
                },
                3 => new[]
                {
                    "ALTER TABLE SyncRecords ADD COLUMN TargetId TEXT;",
                    "ALTER TABLE SyncRecords ADD COLUMN TargetName TEXT;",
                    "ALTER TABLE SyncRecords ADD COLUMN RemoteNamespace TEXT;",
                },
                _ => throw new InvalidOperationException($"No migration exists for schema {next}."),
            };
            foreach (var statement in statements)
                await ExecuteAsync(connection, transaction, statement, cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO Metadata (Key, Value) VALUES ('schema-version', $value) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;";
            command.Parameters.AddWithValue("$value", next.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
    }

    private static async Task<int> ReadSchemaVersionAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Metadata';";
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) == 0) return 0;
        command.CommandText = "SELECT Value FROM Metadata WHERE Key = 'schema-version';";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null) return 0;
        if (value is not string text || !int.TryParse(text, out var version) || version < 1)
            throw new InvalidDataException("The database schema version is invalid; the database was left unchanged.");
        return version;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
