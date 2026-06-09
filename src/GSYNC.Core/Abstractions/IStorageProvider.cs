using GSYNC.Core.Models;

namespace GSYNC.Core.Abstractions;

public interface IStorageProvider
{
    string ProviderId { get; }

    string DisplayName { get; }

    Task<ConnectionResult> TestConnectionAsync(IReadOnlyDictionary<string, string> configuration, CancellationToken cancellationToken);

    Task UploadAsync(string remotePath, Stream content, CancellationToken cancellationToken);

    Task<StorageDownloadResult> DownloadAsync(string remotePath, CancellationToken cancellationToken);

    Task<IReadOnlyList<RemoteEntry>> ListAsync(string pathNamespace, CancellationToken cancellationToken);

    Task DeleteAsync(string remotePath, CancellationToken cancellationToken);
}
