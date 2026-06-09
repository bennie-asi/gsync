using GSYNC.Core.Abstractions.Data;
using Microsoft.Data.Sqlite;

namespace GSYNC.Data.Services;

public sealed class DatabaseInitializer
{
    private const int CurrentSchemaVersion = 1;
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

        foreach (var commandText in new[]
        {
            Entities.SqliteSchema.CreateMetadata,
            Entities.SqliteSchema.CreateGameInstances,
            Entities.SqliteSchema.CreateStorageBindings,
            Entities.SqliteSchema.CreateSyncRecords,
            Entities.SqliteSchema.CreateSnapshots,
            Entities.SqliteSchema.CreateCommunityDefinitions,
            Entities.SqliteSchema.CreateDefinitionOverrides,
        })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await UpsertSchemaVersionAsync(connection, cancellationToken);
    }

    private static async Task UpsertSchemaVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Metadata (Key, Value) VALUES ('schema-version', $value) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;";
        command.Parameters.AddWithValue("$value", CurrentSchemaVersion.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
