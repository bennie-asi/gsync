using GSYNC.Core.Abstractions;
using GSYNC.Core.Models;
using GSYNC.Core.Utilities;
using GSYNC.Storage.Options;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace GSYNC.Storage.Services;

public sealed class LocalFolderStorageProvider : IStorageProvider
{
    private readonly IOptions<LocalFolderOptions> _options;

    public LocalFolderStorageProvider(IOptions<LocalFolderOptions> options)
    {
        _options = options;
    }

    public string ProviderId => "local-folder";

    public string DisplayName => "Local Folder";

    public string? LocalRootPath => Path.GetFullPath(ResolveRootPath());

    public IStorageProvider WithConfiguration(IReadOnlyDictionary<string, string> configuration)
    {
        return new LocalFolderStorageProvider(Microsoft.Extensions.Options.Options.Create(new LocalFolderOptions
        {
            RootPath = ResolveRootPath(configuration),
        }));
    }

    public Task<ConnectionResult> TestConnectionAsync(IReadOnlyDictionary<string, string> configuration, CancellationToken cancellationToken)
    {
        try
        {
            var rootPath = ResolveRootPath(configuration);
            Directory.CreateDirectory(rootPath);
            return Task.FromResult(ConnectionResult.Success());
        }
        catch (Exception exception)
        {
            return Task.FromResult(ConnectionResult.Failure(exception.Message));
        }
    }

    public async Task UploadAsync(string remotePath, Stream content, CancellationToken cancellationToken)
    {
        var fullPath = ResolveFullPath(remotePath);
        await AtomicFile.CopyAsync(content, fullPath, cancellationToken);
    }

    public Task<StorageDownloadResult> DownloadAsync(string remotePath, CancellationToken cancellationToken)
    {
        var fullPath = ResolveFullPath(remotePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Remote entry '{remotePath}' was not found.", fullPath);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var fileInfo = new FileInfo(fullPath);
        return Task.FromResult(new StorageDownloadResult
        {
            Content = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, useAsync: true),
            Length = fileInfo.Length,
        });
    }

    public Task<IReadOnlyList<RemoteEntry>> ListAsync(string pathNamespace, CancellationToken cancellationToken)
    {
        var fullPath = ResolveFullPath(pathNamespace, allowRoot: true);
        if (!Directory.Exists(fullPath))
        {
            return Task.FromResult<IReadOnlyList<RemoteEntry>>(Array.Empty<RemoteEntry>());
        }

        cancellationToken.ThrowIfCancellationRequested();
        var entries = SafePath.EnumerateTree(fullPath)
            .Where(path => !AtomicFile.IsTemporary(path))
            .Select(path =>
            {
                var isDirectory = Directory.Exists(path);
                var relativePath = Path.GetRelativePath(ResolveRootPath(), path).Replace('\\', '/');
                var info = isDirectory ? null : new FileInfo(path);
                return new RemoteEntry
                {
                    Path = relativePath,
                    IsDirectory = isDirectory,
                    Size = info?.Length ?? 0,
                    LastModifiedUtc = isDirectory ? null : info?.LastWriteTimeUtc,
                };
            })
            .ToArray();

        return Task.FromResult<IReadOnlyList<RemoteEntry>>(entries);
    }

    public Task DeleteAsync(string remotePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveFullPath(remotePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        else if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }

        return Task.CompletedTask;
    }

    public async Task<StorageDownloadResult> DownloadVersionedAsync(string remotePath, CancellationToken cancellationToken)
    {
        using var source = await DownloadAsync(remotePath, cancellationToken);
        if (source.Length > 32 * 1024 * 1024) throw new InvalidDataException("The remote commit manifest is too large.");
        var memory = new MemoryStream();
        try
        {
            await source.Content.CopyToAsync(memory, cancellationToken);
            if (memory.Length > 32 * 1024 * 1024) throw new InvalidDataException("The remote commit manifest is too large.");
            var revision = Convert.ToHexString(SHA256.HashData(memory.GetBuffer().AsSpan(0, checked((int)memory.Length))));
            memory.Position = 0;
            return new StorageDownloadResult { Content = memory, Length = memory.Length, Revision = revision };
        }
        catch { memory.Dispose(); throw; }
    }

    public async Task<bool> TryUploadAsync(string remotePath, Stream content, string? expectedRevision, CancellationToken cancellationToken)
    {
        var path = ResolveFullPath(remotePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        FileStream? lease = null;
        while (lease is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { lease = new FileStream(ResolveFullPath(remotePath + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException exception) when ((exception.HResult & 0xffff) is 32 or 33)
            { await Task.Delay(25, cancellationToken); }
        }
        using (lease)
        {
            string? current = null;
            if (File.Exists(path))
            {
                await using var source = File.OpenRead(path);
                current = Convert.ToHexString(await SHA256.HashDataAsync(source, cancellationToken));
            }
            if (!string.Equals(current, expectedRevision, StringComparison.Ordinal)) return false;
            await AtomicFile.CopyAsync(content, ResolveFullPath(remotePath), cancellationToken);
            return true;
        }
    }

    private string ResolveFullPath(string remotePath, bool allowRoot = false)
    {
        return SafePath.ResolveUnderRoot(ResolveRootPath(), remotePath, allowRoot);
    }

    private string ResolveRootPath(IReadOnlyDictionary<string, string>? configuration = null)
    {
        if (configuration is not null && configuration.TryGetValue("rootPath", out var configured))
        {
            if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured))
                throw new InvalidOperationException("The selected storage target needs an absolute local folder path.");
            return configured;
        }
        if (configuration?.ContainsKey("targetId") == true)
            throw new InvalidOperationException("The selected storage target has no local folder path.");

        var options = _options.Value;
        if (string.IsNullOrWhiteSpace(options.RootPath) || !Path.IsPathFullyQualified(options.RootPath))
        {
            throw new InvalidOperationException("Local folder root path has not been configured.");
        }

        return options.RootPath;
    }
}
