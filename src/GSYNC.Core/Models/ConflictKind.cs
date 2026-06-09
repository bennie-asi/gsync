namespace GSYNC.Core.Models;

public enum ConflictKind
{
    MissingLocal,
    MissingRemote,
    ModifiedBoth,
    TypeMismatch,
}
