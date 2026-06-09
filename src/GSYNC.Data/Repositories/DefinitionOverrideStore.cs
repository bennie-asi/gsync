using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Core.Models.Manifest;
using GSYNC.Data.Services;
using Microsoft.Data.Sqlite;

namespace GSYNC.Data.Repositories;

public sealed class DefinitionOverrideStore : IDefinitionOverrideStore
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public DefinitionOverrideStore(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<GameContentDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
    {
        var results = new List<GameContentDefinition>();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DefinitionJson FROM DefinitionOverrides ORDER BY GameId;";

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

    public async Task SaveDefinitionsAsync(IEnumerable<GameContentDefinition> definitions, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM DefinitionOverrides;";
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var definition in definitions)
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = "INSERT INTO DefinitionOverrides (GameId, DefinitionJson, UpdatedAtUtc) VALUES ($gameId, $definitionJson, $updatedAtUtc);";
            insertCommand.Parameters.AddWithValue("$gameId", definition.GameId);
            insertCommand.Parameters.AddWithValue("$definitionJson", Services.JsonStorage.Serialize(definition));
            insertCommand.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.UtcDateTime.ToString("O"));
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
