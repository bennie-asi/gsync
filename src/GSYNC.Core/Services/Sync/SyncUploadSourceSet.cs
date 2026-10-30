using GSYNC.Core.Utilities;

namespace GSYNC.Core.Services.Sync;

/// <summary>Captures all selected sources while read leases exclude game writes, then releases the game files before network IO.</summary>
internal sealed class SyncUploadSourceSet : IDisposable
{
    private readonly Dictionary<string, AtomicFile.PreparedFile> _files = new(StringComparer.OrdinalIgnoreCase);

    internal static async Task<SyncUploadSourceSet> CaptureAsync(IEnumerable<ResolvedLocalFile> files,
        IReadOnlyDictionary<string, string?>? expectedHashes, CancellationToken cancellationToken)
    {
        var result = new SyncUploadSourceSet();
        var leases = new Dictionary<string, FileStream>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var path in files.Select(file => file.AbsolutePath).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                leases.Add(path, File.OpenRead(path));
            }
            foreach (var pair in leases)
            {
                var prepared = await AtomicFile.PrepareAsync(pair.Value, Path.Combine(Path.GetTempPath(), "gsync-source-" + Guid.NewGuid().ToString("N")), cancellationToken);
                result._files.Add(pair.Key, prepared);
                if (expectedHashes is not null && expectedHashes.TryGetValue(pair.Key, out var expected) &&
                    !string.Equals(RecoverableFileTransaction.Hash(prepared.TemporaryPath!), expected, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("A local save changed after comparison. Compare the latest versions before uploading.");
            }
            return result;
        }
        catch { result.Dispose(); throw; }
        finally { foreach (var lease in leases.Values) lease.Dispose(); }
    }

    internal Stream OpenRead(string sourcePath) => File.OpenRead(_files[sourcePath].TemporaryPath!);
    public void Dispose() { foreach (var file in _files.Values) file.Dispose(); }
}
