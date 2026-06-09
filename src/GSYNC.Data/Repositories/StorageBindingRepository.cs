using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using GSYNC.Data.Services;
using Microsoft.Data.Sqlite;

namespace GSYNC.Data.Repositories;

public sealed class StorageBindingRepository : IStorageBindingRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public StorageBindingRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<StorageBinding>> ListByGameInstanceAsync(Guid gameInstanceId, CancellationToken cancellationToken)
    {
        var results = new List<StorageBinding>();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, GameInstanceId, StorageProviderId, RemoteNamespace, SettingsJson, ContentSelectionsJson, CreatedAtUtc, UpdatedAtUtc FROM StorageBindings WHERE GameInstanceId = $gameInstanceId ORDER BY CreatedAtUtc;";
        command.Parameters.AddWithValue("$gameInstanceId", gameInstanceId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(Read(reader));
        }

        return results;
    }

    public async Task<StorageBinding?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, GameInstanceId, StorageProviderId, RemoteNamespace, SettingsJson, ContentSelectionsJson, CreatedAtUtc, UpdatedAtUtc FROM StorageBindings WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? Read(reader)
            : null;
    }

    public async Task UpsertAsync(StorageBinding binding, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO StorageBindings (Id, GameInstanceId, StorageProviderId, RemoteNamespace, SettingsJson, ContentSelectionsJson, CreatedAtUtc, UpdatedAtUtc)
            VALUES ($id, $gameInstanceId, $storageProviderId, $remoteNamespace, $settingsJson, $contentSelectionsJson, $createdAtUtc, $updatedAtUtc)
            ON CONFLICT(Id) DO UPDATE SET
                GameInstanceId = excluded.GameInstanceId,
                StorageProviderId = excluded.StorageProviderId,
                RemoteNamespace = excluded.RemoteNamespace,
                SettingsJson = excluded.SettingsJson,
                ContentSelectionsJson = excluded.ContentSelectionsJson,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;
        command.Parameters.AddWithValue("$id", binding.Id.ToString("D"));
        command.Parameters.AddWithValue("$gameInstanceId", binding.GameInstanceId.ToString("D"));
        command.Parameters.AddWithValue("$storageProviderId", binding.StorageProviderId);
        command.Parameters.AddWithValue("$remoteNamespace", binding.RemoteNamespace);
        command.Parameters.AddWithValue("$settingsJson", Services.JsonStorage.Serialize(binding.Settings));
        command.Parameters.AddWithValue("$contentSelectionsJson", Services.JsonStorage.Serialize(binding.ContentSelections));
        command.Parameters.AddWithValue("$createdAtUtc", binding.CreatedAtUtc.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$updatedAtUtc", binding.UpdatedAtUtc.UtcDateTime.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM StorageBindings WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static StorageBinding Read(SqliteDataReader reader)
    {
        return new StorageBinding
        {
            Id = Guid.Parse(reader.GetString(0)),
            GameInstanceId = Guid.Parse(reader.GetString(1)),
            StorageProviderId = reader.GetString(2),
            RemoteNamespace = reader.GetString(3),
            Settings = Services.JsonStorage.Deserialize<Dictionary<string, string>>(reader.GetString(4)) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ContentSelections = Services.JsonStorage.Deserialize<List<ContentSelection>>(reader.GetString(5)) ?? new List<ContentSelection>(),
            CreatedAtUtc = DateTimeOffset.Parse(reader.GetString(6)),
            UpdatedAtUtc = DateTimeOffset.Parse(reader.GetString(7)),
        };
    }
}
