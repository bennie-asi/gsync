namespace GSYNC.Core.Models;

public enum SyncChangeKind
{
    Unchanged,
    AddedLocally,
    AddedRemotely,
    UpdatedLocally,
    UpdatedRemotely,
    Conflict,
}
