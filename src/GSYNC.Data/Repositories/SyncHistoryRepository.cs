using GSYNC.Core.Abstractions.Data;
using GSYNC.Core.Models;
using GSYNC.Data.Services;
using Microsoft.Data.Sqlite;

namespace GSYNC.Data.Repositories;

public sealed class SyncHistoryRepository : ISyncHistoryRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SyncHistoryRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task AddRecordAsync(SyncRecord record, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO SyncRecords (Id, GameInstanceId, SnapshotId, Direction, Status, StartedAtUtc, CompletedAtUtc, Summary, ErrorMessage, ProcessedFiles, TotalFiles)
            VALUES ($id, $gameInstanceId, $snapshotId, $direction, $status, $startedAtUtc, $completedAtUtc, $summary, $errorMessage, $processedFiles, $totalFiles);
            """;
        command.Parameters.AddWithValue("$id", record.Id.ToString("D"));
        command.Parameters.AddWithValue("$gameInstanceId", record.GameInstanceId.ToString("D"));
        command.Parameters.AddWithValue("$snapshotId", record.SnapshotId?.ToString("D") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$direction", record.Direction.ToString());
        command.Parameters.AddWithValue("$status", record.Status.ToString());
        command.Parameters.AddWithValue("$startedAtUtc", record.StartedAtUtc.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$completedAtUtc", record.CompletedAtUtc?.UtcDateTime.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$summary", (object?)record.Summary ?? DBNull.Value);
        command.Parameters.AddWithValue("$errorMessage", (object?)record.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$processedFiles", record.ProcessedFiles);
        command.Parameters.AddWithValue("$totalFiles", record.TotalFiles);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SyncRecord>> ListRecordsAsync(Guid gameInstanceId, CancellationToken cancellationToken)
    {
        var results = new List<SyncRecord>();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, GameInstanceId, SnapshotId, Direction, Status, StartedAtUtc, CompletedAtUtc, Summary, ErrorMessage, ProcessedFiles, TotalFiles FROM SyncRecords WHERE GameInstanceId = $gameInstanceId ORDER BY StartedAtUtc DESC;";
        command.Parameters.AddWithValue("$gameInstanceId", gameInstanceId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadRecord(reader));
        }

        return results;
    }

    public async Task<IReadOnlyList<SyncRecord>> ListRecentRecordsAsync(int limit, CancellationToken cancellationToken)
    {
        var results = new List<SyncRecord>();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, GameInstanceId, SnapshotId, Direction, Status, StartedAtUtc, CompletedAtUtc, Summary, ErrorMessage, ProcessedFiles, TotalFiles FROM SyncRecords ORDER BY StartedAtUtc DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadRecord(reader));
        }

        return results;
    }

    private static SyncRecord ReadRecord(SqliteDataReader reader)
    {
        return new SyncRecord
        {
            Id = Guid.Parse(reader.GetString(0)),
            GameInstanceId = Guid.Parse(reader.GetString(1)),
            SnapshotId = reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)),
            Direction = Enum.Parse<SyncDirection>(reader.GetString(3), ignoreCase: true),
            Status = Enum.Parse<SyncJobStatus>(reader.GetString(4), ignoreCase: true),
            StartedAtUtc = DateTimeOffset.Parse(reader.GetString(5)),
            CompletedAtUtc = reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6)),
            Summary = reader.IsDBNull(7) ? null : reader.GetString(7),
            ErrorMessage = reader.IsDBNull(8) ? null : reader.GetString(8),
            ProcessedFiles = reader.GetInt32(9),
            TotalFiles = reader.GetInt32(10),
        };
    }

    public async Task AddSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Snapshots (Id, GameInstanceId, ArchivePath, CreatedAtUtc, FileCount, TotalBytes)
            VALUES ($id, $gameInstanceId, $archivePath, $createdAtUtc, $fileCount, $totalBytes);
            """;
        command.Parameters.AddWithValue("$id", snapshot.Id.ToString("D"));
        command.Parameters.AddWithValue("$gameInstanceId", snapshot.GameInstanceId.ToString("D"));
        command.Parameters.AddWithValue("$archivePath", snapshot.ArchivePath);
        command.Parameters.AddWithValue("$createdAtUtc", snapshot.CreatedAtUtc.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$fileCount", snapshot.FileCount);
        command.Parameters.AddWithValue("$totalBytes", snapshot.TotalBytes);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Snapshot>> ListSnapshotsAsync(Guid gameInstanceId, CancellationToken cancellationToken)
    {
        var results = new List<Snapshot>();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, GameInstanceId, ArchivePath, CreatedAtUtc, FileCount, TotalBytes FROM Snapshots WHERE GameInstanceId = $gameInstanceId ORDER BY CreatedAtUtc DESC;";
        command.Parameters.AddWithValue("$gameInstanceId", gameInstanceId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadSnapshot(reader));
        }

        return results;
    }

    public async Task<Snapshot?> GetSnapshotAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, GameInstanceId, ArchivePath, CreatedAtUtc, FileCount, TotalBytes FROM Snapshots WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return ReadSnapshot(reader);
        }

        return null;
    }

    private static Snapshot ReadSnapshot(SqliteDataReader reader)
    {
        return new Snapshot
        {
            Id = Guid.Parse(reader.GetString(0)),
            GameInstanceId = Guid.Parse(reader.GetString(1)),
            ArchivePath = reader.GetString(2),
            CreatedAtUtc = DateTimeOffset.Parse(reader.GetString(3)),
            FileCount = reader.GetInt32(4),
            TotalBytes = reader.GetInt64(5),
        };
    }
}
