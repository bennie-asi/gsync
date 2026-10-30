using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Models.Manifest;
using GSYNC.Data.Services;
using Microsoft.Data.Sqlite;

namespace GSYNC.Data.Repositories;

public sealed class CommunityDefinitionStore : ICommunityDefinitionStore
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public CommunityDefinitionStore(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<GameContentDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
    {
        return await ReadDefinitionsAsync("CommunityDefinitions", cancellationToken);
    }

    public async Task<GameContentDefinition?> GetDefinitionAsync(string gameId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DefinitionJson FROM CommunityDefinitions WHERE GameId = $gameId;";
        command.Parameters.AddWithValue("$gameId", gameId);
        var json = await command.ExecuteScalarAsync(cancellationToken) as string;
        if (json is not null) return JsonStorage.Deserialize<GameContentDefinition>(json);

        command.CommandText = "SELECT DefinitionJson FROM CommunityDefinitions WHERE GameId = $gameId COLLATE NOCASE LIMIT 2;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var definition = JsonStorage.Deserialize<GameContentDefinition>(reader.GetString(0));
        if (await reader.ReadAsync(cancellationToken)) throw new InvalidDataException($"Ambiguous legacy game ID '{gameId}'.");
        return definition;
    }

    public async Task SaveDefinitionsAsync(IEnumerable<GameContentDefinition> definitions, CancellationToken cancellationToken)
    {
        await SaveDefinitionsAsync("CommunityDefinitions", definitions, cancellationToken);
    }

    private async Task<IReadOnlyList<GameContentDefinition>> ReadDefinitionsAsync(string tableName, CancellationToken cancellationToken)
    {
        var results = new List<GameContentDefinition>();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT DefinitionJson FROM {tableName} ORDER BY GameId;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var definition = Services.JsonStorage.Deserialize<GameContentDefinition>(reader.GetString(0));
            if (definition is not null)
            {
                results.Add(definition);
            }
        }

        return results;
    }

    private async Task SaveDefinitionsAsync(string tableName, IEnumerable<GameContentDefinition> definitions, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = $"DELETE FROM {tableName};";
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var definition in definitions)
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = $"INSERT INTO {tableName} (GameId, DefinitionJson, UpdatedAtUtc) VALUES ($gameId, $definitionJson, $updatedAtUtc);";
            insertCommand.Parameters.AddWithValue("$gameId", definition.GameId);
            insertCommand.Parameters.AddWithValue("$definitionJson", Services.JsonStorage.Serialize(definition));
            insertCommand.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.UtcDateTime.ToString("O"));
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
