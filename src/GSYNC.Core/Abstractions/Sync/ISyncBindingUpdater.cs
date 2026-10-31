using GSYNC.Core.Models;

namespace GSYNC.Core.Abstractions.Sync;

/// <summary>Updates a game's binding while excluding sync operations for that game.</summary>
public interface ISyncBindingUpdater
{
    Task UpdateBindingAsync(StorageBinding binding, Guid? expectedBindingId, DateTimeOffset? expectedUpdatedAtUtc, CancellationToken cancellationToken);
}

public sealed class SyncBindingBusyException() : InvalidOperationException("Wait for this game's queued or running sync tasks before changing its destination.") { }
public sealed class SyncBindingChangedException() : InvalidOperationException("The game's sync destination changed. Reload it before saving.") { }
