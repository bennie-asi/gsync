using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Models;
using GSYNC.Core.Utilities;

namespace GSYNC.Core.Services.Sync;

/// <summary>
/// Pins reads to an immutable save generation. Writes become visible together through one conditional manifest commit.
/// Storage providers only implement byte IO and compare-and-swap; save version policy stays in Core.
/// </summary>
public sealed class RemoteSaveSession : IStorageProvider
{
    private const int MaximumManifestBytes = 32 * 1024 * 1024;
    private const int MaximumFiles = 100_000;
    private readonly IStorageProvider _storage;
    private readonly string _namespace;
    private readonly string? _revision;
    private readonly bool _hasManifest;
    private readonly Dictionary<string, SaveFile> _files;
    private readonly Dictionary<string, SaveFile> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _removed = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _generation = Guid.NewGuid().ToString("N");
    private bool _committed;

    private RemoteSaveSession(IStorageProvider storage, string pathNamespace, string? revision, SaveManifest? manifest)
    {
        _storage = storage;
        _namespace = pathNamespace;
        _revision = revision;
        _hasManifest = manifest is not null;
        _files = manifest?.Files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase)
            ?? new(StringComparer.OrdinalIgnoreCase);
    }

    public string ProviderId => _storage.ProviderId;
    public string DisplayName => _storage.DisplayName;
    public string? LocalRootPath => _storage.LocalRootPath;
    private string ManifestPath => _namespace + "/__gsync_meta/current.json";

    public static async Task<RemoteSaveSession> OpenAsync(IStorageProvider storage, string pathNamespace, CancellationToken cancellationToken)
    {
        pathNamespace = pathNamespace.Trim('/');
        SafePath.ValidateRelative(pathNamespace);
        try
        {
            using var download = await storage.DownloadVersionedAsync(pathNamespace + "/__gsync_meta/current.json", cancellationToken);
            using var bytes = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await download.Content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (bytes.Length + read > MaximumManifestBytes) throw new InvalidDataException("The remote save manifest is too large.");
                bytes.Write(buffer, 0, read);
            }
            var manifest = JsonSerializer.Deserialize<SaveManifest>(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length)))
                ?? throw new InvalidDataException("The remote save manifest is empty.");
            ValidateManifest(manifest, pathNamespace);
            return new RemoteSaveSession(storage, pathNamespace, download.Revision, manifest);
        }
        catch (FileNotFoundException) { return new RemoteSaveSession(storage, pathNamespace, null, null); }
        catch (DirectoryNotFoundException) { return new RemoteSaveSession(storage, pathNamespace, null, null); }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        { return new RemoteSaveSession(storage, pathNamespace, null, null); }
        catch (JsonException exception) { throw new InvalidDataException("The remote save manifest is invalid; no save was changed.", exception); }
    }

    public async Task UploadAsync(string remotePath, Stream content, CancellationToken cancellationToken)
    {
        if (_committed) throw new InvalidOperationException("This save generation was already committed.");
        if (_hasManifest && string.IsNullOrWhiteSpace(_revision))
            throw new InvalidOperationException("This storage server did not supply a strong revision. Conditional commits are required before uploading.");
        var logical = LogicalPath(remotePath);
        // Spool a stable copy before hashing or uploading, including when the caller supplies a non-seekable stream.
        var temporary = Path.Combine(Path.GetTempPath(), AtomicFile.TemporaryName());
        await using var staged = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        await content.CopyToAsync(staged, cancellationToken);
        staged.Position = 0;
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(staged, cancellationToken));
        staged.Position = 0;
        var blob = _namespace + "/__gsync_versions/" + _generation + "/" + logical;
        var size = staged.Length;
        // Providers may dispose the stream after sending; all metadata is captured first.
        await _storage.UploadAsync(blob, staged, cancellationToken);
        _pending[logical] = new SaveFile(logical, blob, size, hash, DateTimeOffset.UtcNow);
        _removed.Remove(logical);
    }

    public Task<StorageDownloadResult> DownloadAsync(string remotePath, CancellationToken cancellationToken)
    {
        var logical = LogicalPath(remotePath);
        if (!_hasManifest) return _storage.DownloadAsync(remotePath, cancellationToken);
        if (!_files.TryGetValue(logical, out var file)) throw new FileNotFoundException($"Remote save '{logical}' was not found.");
        return _storage.DownloadAsync(file.BlobPath, cancellationToken);
    }

    internal async Task<T?> ReadMetadataAsync<T>(string remotePath, CancellationToken cancellationToken) where T : class
    {
        using var download = await DownloadAsync(remotePath, cancellationToken);
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await download.Content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (bytes.Length + read > 64 * 1024) throw new InvalidDataException("The remote content descriptor is too large.");
            bytes.Write(buffer, 0, read);
        }
        if (_hasManifest && _files.TryGetValue(LogicalPath(remotePath), out var file) &&
            (bytes.Length != file.Size || !string.Equals(Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length)))), file.Hash, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("The remote content descriptor failed its integrity check.");
        return JsonSerializer.Deserialize<T>(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length)));
    }

    public async Task<IReadOnlyList<RemoteEntry>> ListAsync(string pathNamespace, CancellationToken cancellationToken)
    {
        var logical = pathNamespace.TrimEnd('/') == _namespace ? string.Empty : LogicalPath(pathNamespace);
        if (!_hasManifest)
            return (await _storage.ListAsync(pathNamespace, cancellationToken)).Where(entry => !IsInternalBlob(entry.Path)).ToArray();
        return _files.Values.Where(file => logical.Length == 0 || file.Path.StartsWith(logical + '/', StringComparison.OrdinalIgnoreCase))
            .Select(file => new RemoteEntry { Path = _namespace + '/' + file.Path, Size = file.Size, Hash = file.Hash, LastModifiedUtc = file.ModifiedAtUtc }).ToArray();
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_committed) throw new InvalidOperationException("This save generation was already committed.");
        if (_pending.Count == 0 && _removed.Count == 0) return;
        if (_hasManifest && string.IsNullOrWhiteSpace(_revision))
            throw new InvalidOperationException("The storage server did not supply a strong revision for the current save. Conditional commits are required.");
        if (!_hasManifest)
        {
            // Import untouched legacy files as immutable blobs, so the first commit also pins a complete generation.
            var legacy = await _storage.ListAsync(_namespace, cancellationToken);
            foreach (var entry in legacy.Where(entry => !entry.IsDirectory && !IsInternalBlob(entry.Path)))
            {
                var logical = LogicalPath(entry.Path);
                if (logical == "__gsync_meta/current.json" || logical == "__gsync_meta/current.json.lock" || _pending.ContainsKey(logical) || _removed.Contains(logical)) continue;
                using var download = await _storage.DownloadAsync(entry.Path, cancellationToken);
                await UploadAsync(entry.Path, download.Content, cancellationToken);
            }
        }
        var next = new Dictionary<string, SaveFile>(_files, StringComparer.OrdinalIgnoreCase);
        foreach (var path in _removed) next.Remove(path);
        foreach (var pair in _pending) next[pair.Key] = pair.Value;
        if (next.Count > MaximumFiles) throw new InvalidDataException("The save contains too many files.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new SaveManifest(1, _generation, next.Values.OrderBy(file => file.Path, StringComparer.Ordinal).ToArray()));
        if (bytes.Length > MaximumManifestBytes) throw new InvalidDataException("The save manifest is too large.");
        cancellationToken.ThrowIfCancellationRequested();
        using var content = new MemoryStream(bytes, writable: false);
        if (!await _storage.TryUploadAsync(ManifestPath, content, _revision, cancellationToken))
            throw new RemoteSaveChangedException();
        _committed = true;
    }

    public Task<ConnectionResult> TestConnectionAsync(IReadOnlyDictionary<string, string> configuration, CancellationToken cancellationToken)
        => _storage.TestConnectionAsync(configuration, cancellationToken);
    public Task DeleteAsync(string remotePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_committed) throw new InvalidOperationException("This save generation was already committed.");
        var logical = LogicalPath(remotePath);
        if (logical.StartsWith("__gsync_meta/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Internal save metadata cannot be removed as a save file.");
        if (_hasManifest && !_files.ContainsKey(logical)) throw new FileNotFoundException("The remote save no longer exists.");
        _pending.Remove(logical);
        _removed.Add(logical);
        return Task.CompletedTask;
    }

    private string LogicalPath(string remotePath)
    {
        if (!remotePath.StartsWith(_namespace + '/', StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The save path is outside its game namespace.");
        var logical = remotePath[(_namespace.Length + 1)..];
        SafePath.ValidateRelative(logical);
        if (IsInternalBlob(remotePath)) throw new InvalidDataException("A save path uses a reserved generation directory.");
        return logical;
    }

    private bool IsInternalBlob(string path) => path.StartsWith(_namespace + "/__gsync_versions/", StringComparison.OrdinalIgnoreCase) || AtomicFile.IsTemporary(path);

    private static void ValidateManifest(SaveManifest manifest, string pathNamespace)
    {
        if (manifest.Version != 1 || !Guid.TryParseExact(manifest.Generation, "N", out _) || manifest.Files is null || manifest.Files.Length > MaximumFiles)
            throw new InvalidDataException("Unsupported remote save manifest.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            if (file is null || string.IsNullOrWhiteSpace(file.Path) || string.IsNullOrWhiteSpace(file.BlobPath) || file.Size < 0 ||
                file.Hash is null || file.Hash.Length != 64 || !file.Hash.All(Uri.IsHexDigit) || !paths.Add(file.Path))
                throw new InvalidDataException("The remote save manifest has invalid or duplicate entries.");
            SafePath.ValidateRelative(file.Path);
            SafePath.ValidateRelative(file.BlobPath);
            var prefix = pathNamespace + "/__gsync_versions/";
            if (!file.BlobPath.StartsWith(prefix, StringComparison.Ordinal) ||
                file.BlobPath.Length < prefix.Length + 34 ||
                !Guid.TryParseExact(file.BlobPath.Substring(prefix.Length, 32), "N", out _) ||
                file.BlobPath[prefix.Length + 32] != '/' || file.BlobPath[(prefix.Length + 33)..] != file.Path ||
                file.Path.StartsWith("__gsync_versions/", StringComparison.OrdinalIgnoreCase) ||
                file.Path.Equals("__gsync_meta/current.json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A remote save entry points outside an immutable generation.");
        }
    }

    private sealed record SaveManifest(int Version, string Generation, SaveFile[] Files);
    private sealed record SaveFile(string Path, string BlobPath, long Size, string Hash, DateTimeOffset ModifiedAtUtc);
}

public sealed class RemoteSaveChangedException() : IOException("The remote save changed during synchronization. Compare the latest versions and retry; the newer remote save was preserved.");
