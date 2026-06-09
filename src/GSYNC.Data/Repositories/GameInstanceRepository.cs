using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Models;
using GSYNC.Data.Services;
using Microsoft.Data.Sqlite;

namespace GSYNC.Data.Repositories;

public sealed class GameInstanceRepository : IGameInstanceRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public GameInstanceRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<GameInstance>> ListAsync(CancellationToken cancellationToken)
    {
        var results = new List<GameInstance>();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, GameId, DisplayName, SourceProviderId, InstallDirectory, PlatformInstanceId, VariablesJson, CreatedAtUtc, UpdatedAtUtc FROM GameInstances ORDER BY DisplayName;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(Read(reader));
        }

        return results;
    }

    public async Task<GameInstance?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, GameId, DisplayName, SourceProviderId, InstallDirectory, PlatformInstanceId, VariablesJson, CreatedAtUtc, UpdatedAtUtc FROM GameInstances WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? Read(reader)
            : null;
    }

    public async Task UpsertAsync(GameInstance instance, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO GameInstances (Id, GameId, DisplayName, SourceProviderId, InstallDirectory, PlatformInstanceId, VariablesJson, CreatedAtUtc, UpdatedAtUtc)
            VALUES ($id, $gameId, $displayName, $sourceProviderId, $installDirectory, $platformInstanceId, $variablesJson, $createdAtUtc, $updatedAtUtc)
            ON CONFLICT(Id) DO UPDATE SET
                GameId = excluded.GameId,
                DisplayName = excluded.DisplayName,
                SourceProviderId = excluded.SourceProviderId,
                InstallDirectory = excluded.InstallDirectory,
                PlatformInstanceId = excluded.PlatformInstanceId,
                VariablesJson = excluded.VariablesJson,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;
        command.Parameters.AddWithValue("$id", instance.Id.ToString("D"));
        command.Parameters.AddWithValue("$gameId", instance.GameId);
        command.Parameters.AddWithValue("$displayName", instance.DisplayName);
        command.Parameters.AddWithValue("$sourceProviderId", instance.SourceProviderId);
        command.Parameters.AddWithValue("$installDirectory", (object?)instance.InstallDirectory ?? DBNull.Value);
        command.Parameters.AddWithValue("$platformInstanceId", (object?)instance.PlatformInstanceId ?? DBNull.Value);
        command.Parameters.AddWithValue("$variablesJson", Services.JsonStorage.Serialize(instance.Variables));
        command.Parameters.AddWithValue("$createdAtUtc", instance.CreatedAtUtc.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$updatedAtUtc", instance.UpdatedAtUtc.UtcDateTime.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM GameInstances WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static GameInstance Read(SqliteDataReader reader)
    {
        return new GameInstance
        {
            Id = Guid.Parse(reader.GetString(0)),
            GameId = reader.GetString(1),
            DisplayName = reader.GetString(2),
            SourceProviderId = reader.GetString(3),
            InstallDirectory = reader.IsDBNull(4) ? null : reader.GetString(4),
            PlatformInstanceId = reader.IsDBNull(5) ? null : reader.GetString(5),
            Variables = Services.JsonStorage.Deserialize<Dictionary<string, string>>(reader.GetString(6)) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            CreatedAtUtc = DateTimeOffset.Parse(reader.GetString(7)),
            UpdatedAtUtc = DateTimeOffset.Parse(reader.GetString(8)),
        };
    }
}
