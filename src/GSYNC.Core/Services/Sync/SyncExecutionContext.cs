using GSYNC.Core.Abstractions;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;

namespace GSYNC.Core.Services.Sync;

internal sealed record SnapshotCandidate(string LocalPath, string SnapshotEntry, bool? RootIsDirectory = null);
internal sealed record SnapshotFile(string Path, bool Existed, string? Hash, bool? RootIsDirectory = null);
internal sealed record SnapshotManifest(int Version, SnapshotFile[] Files);
internal sealed record RootDescriptor(int Version, bool? IsDirectory);

internal sealed record SyncExecutionContext(
    GameInstance GameInstance,
    StorageBinding Binding,
    ISourceProvider SourceProvider,
    IStorageProvider StorageProvider,
    IReadOnlyList<ResolvedContentRoot> ContentRoots);

internal sealed class ResolvedContentRoot(ContentItem contentItem, string localRootPath, bool treatAsDirectory, string storageKey)
{
    public ContentItem ContentItem { get; } = contentItem;
    public string LocalRootPath { get; } = localRootPath;
    public string StorageKey { get; } = storageKey;
    public bool TreatAsDirectory { get; private set; } = treatAsDirectory;
    public bool KindKnownLocally { get; } = File.Exists(localRootPath) || Directory.Exists(localRootPath);
    public bool KindDeclared { get; private set; }
    public string? GlobPattern { get; init; }
    public void UseRemoteKind(bool directory)
    {
        if ((KindKnownLocally || KindDeclared) && TreatAsDirectory != directory)
            throw new InvalidDataException($"Content path '{LocalRootPath}' has a different file/directory type from its backup.");
        if (GlobPattern is not null && !directory) throw new InvalidDataException("A glob template requires a directory root.");
        TreatAsDirectory = directory;
        KindDeclared = true;
    }
}

internal sealed record ResolvedLocalFile(ContentItem ContentItem, string AbsolutePath, string RelativePath, DateTime LastModifiedUtc)
{
    public string StorageKey { get; init; } = string.Empty;
}

internal sealed record RemoteFileEntry(string RelativePath, string StoragePath, string? Hash, DateTimeOffset? LastModifiedUtc, long Size);
