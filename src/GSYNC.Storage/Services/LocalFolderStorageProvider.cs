using GSYNC.Core.Abstractions;
using GSYNC.Core.Models;
using GSYNC.Storage.Options;
using Microsoft.Extensions.Options;

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

    public Task<ConnectionResult> TestConnectionAsync(IReadOnlyDictionary<string, string> configuration, CancellationToken cancellationToken)
    {
        var rootPath = ResolveRootPath(configuration);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return Task.FromResult(ConnectionResult.Failure("A local folder path is required."));
        }

        try
        {
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
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var target = File.Create(fullPath);
        await content.CopyToAsync(target, cancellationToken);
    }

    public async Task<StorageDownloadResult> DownloadAsync(string remotePath, CancellationToken cancellationToken)
    {
        var fullPath = ResolveFullPath(remotePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Remote entry '{remotePath}' was not found.", fullPath);
        }

        var buffer = new MemoryStream();
        await using (var source = File.OpenRead(fullPath))
        {
            await source.CopyToAsync(buffer, cancellationToken);
        }

        buffer.Position = 0;
        var fileInfo = new FileInfo(fullPath);
        return new StorageDownloadResult
        {
            Content = buffer,
            Length = fileInfo.Length,
        };
    }

    public Task<IReadOnlyList<RemoteEntry>> ListAsync(string pathNamespace, CancellationToken cancellationToken)
    {
        var fullPath = ResolveFullPath(pathNamespace);
        if (!Directory.Exists(fullPath))
        {
            return Task.FromResult<IReadOnlyList<RemoteEntry>>(Array.Empty<RemoteEntry>());
        }

        var entries = Directory.EnumerateFileSystemEntries(fullPath, "*", SearchOption.AllDirectories)
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

    private string ResolveFullPath(string remotePath)
    {
        var normalized = remotePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        return Path.Combine(ResolveRootPath(), normalized);
    }

    private string ResolveRootPath(IReadOnlyDictionary<string, string>? configuration = null)
    {
        if (configuration is not null && configuration.TryGetValue("rootPath", out var configured) && !string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var options = _options.Value;
        if (string.IsNullOrWhiteSpace(options.RootPath))
        {
            throw new InvalidOperationException("Local folder root path has not been configured.");
        }

        return options.RootPath;
    }
}
