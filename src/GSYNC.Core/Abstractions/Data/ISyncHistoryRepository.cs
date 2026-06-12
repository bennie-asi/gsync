using GSYNC.Core.Models;

namespace GSYNC.Core.Abstractions.Data;

public interface ISyncHistoryRepository
{
    Task AddRecordAsync(SyncRecord record, CancellationToken cancellationToken);

    Task<IReadOnlyList<SyncRecord>> ListRecordsAsync(Guid gameInstanceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SyncRecord>> ListRecentRecordsAsync(int limit, CancellationToken cancellationToken);

    Task AddSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken);

    Task<IReadOnlyList<Snapshot>> ListSnapshotsAsync(Guid gameInstanceId, CancellationToken cancellationToken);

    Task<Snapshot?> GetSnapshotAsync(Guid id, CancellationToken cancellationToken);
}
