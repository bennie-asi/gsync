using System.Text.Json;

namespace GSYNC.Data.Services;

public sealed class CredentialMigrationService(SqliteConnectionFactory connections, CredentialSettingsService credentials)
{
    public async Task MigrateBindingsAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA secure_delete = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id, SettingsJson FROM StorageBindings;";
        var changes = new List<(string Id, string Settings)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(1))
                    ?? throw new InvalidDataException("A storage binding has invalid settings.");
                if (CredentialSettingsService.ContainsSecrets(settings))
                    changes.Add((reader.GetString(0), JsonSerializer.Serialize(credentials.Protect(settings))));
            }
        }
        foreach (var change in changes)
        {
            command.CommandText = "UPDATE StorageBindings SET SettingsJson = $settings WHERE Id = $id;";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$settings", change.Settings);
            command.Parameters.AddWithValue("$id", change.Id);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        transaction.Commit();
    }
}
