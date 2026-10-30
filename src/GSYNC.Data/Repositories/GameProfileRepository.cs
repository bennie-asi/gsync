using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Models;
using GSYNC.Core.Utilities;
using GSYNC.Data.Services;

namespace GSYNC.Data.Repositories;

/// <summary>Creates the game and its binding in one database transaction, including duplicate checks.</summary>
public sealed class GameProfileRepository(SqliteConnectionFactory connections, CredentialSettingsService? credentials = null) : IGameProfileRepository
{
    public async Task CreateAsync(GameInstance instance, StorageBinding binding, CancellationToken cancellationToken)
    {
        if (instance.Id == Guid.Empty || binding.Id == Guid.Empty || binding.GameInstanceId != instance.Id ||
            string.IsNullOrWhiteSpace(instance.GameId) || string.IsNullOrWhiteSpace(instance.DisplayName) ||
            string.IsNullOrWhiteSpace(instance.SourceProviderId) || string.IsNullOrWhiteSpace(binding.StorageProviderId))
            throw new ArgumentException("The game profile has invalid identity or binding data.");
        SafePath.ValidateRelative(binding.RemoteNamespace);
        var settings = CredentialSettingsService.ContainsSecrets(binding.Settings)
            ? (credentials ?? throw new InvalidOperationException("A credential store is required to save credentials.")).Protect(binding.Settings)
            : binding.Settings;
        await using var connection = connections.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await using var check = connection.CreateCommand();
        check.Transaction = transaction;
        check.CommandText = """
            SELECT COUNT(*) FROM GameInstances WHERE Id = $id OR
            (SourceProviderId = $source COLLATE NOCASE AND
                (GameId = $game COLLATE NOCASE OR
                 ($platform <> '' AND PlatformInstanceId = $platform COLLATE NOCASE) OR
                 ($source = 'custom' AND TRIM(DisplayName) = $name COLLATE NOCASE)));
            """;
        check.Parameters.AddWithValue("$id", instance.Id.ToString("D"));
        check.Parameters.AddWithValue("$source", instance.SourceProviderId);
        check.Parameters.AddWithValue("$game", instance.GameId);
        check.Parameters.AddWithValue("$platform", instance.PlatformInstanceId ?? string.Empty);
        check.Parameters.AddWithValue("$name", instance.DisplayName.Trim());
        if (Convert.ToInt64(await check.ExecuteScalarAsync(cancellationToken)) != 0)
            throw new InvalidOperationException("This game is already in the library. Edit its existing profile.");
        await GameInstanceRepository.WriteAsync(instance, connection, transaction, cancellationToken);
        await StorageBindingRepository.WriteAsync(binding, settings, connection, transaction, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
    }
}
