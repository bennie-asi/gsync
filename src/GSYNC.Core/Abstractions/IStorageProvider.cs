using GSYNC.Core.Models;

namespace GSYNC.Core.Abstractions;

public interface IStorageProvider
{
    string ProviderId { get; }

    string DisplayName { get; }

    string? LocalRootPath => null;

    // Providers with target settings return an independent instance to keep running jobs isolated.
    IStorageProvider WithConfiguration(IReadOnlyDictionary<string, string> configuration) => this;

    Task<ConnectionResult> TestConnectionAsync(IReadOnlyDictionary<string, string> configuration, CancellationToken cancellationToken);

    Task UploadAsync(string remotePath, Stream content, CancellationToken cancellationToken);

    Task<StorageDownloadResult> DownloadAsync(string remotePath, CancellationToken cancellationToken);

    // The revision must describe the exact bytes returned by this read.
    Task<StorageDownloadResult> DownloadVersionedAsync(string remotePath, CancellationToken cancellationToken) => DownloadAsync(remotePath, cancellationToken);

    // null means create only if absent. A non-null revision means replace only that version.
    Task<bool> TryUploadAsync(string remotePath, Stream content, string? expectedRevision, CancellationToken cancellationToken)
        => throw new NotSupportedException("This storage provider does not support conditional commits.");

    Task<IReadOnlyList<RemoteEntry>> ListAsync(string pathNamespace, CancellationToken cancellationToken);

    Task DeleteAsync(string remotePath, CancellationToken cancellationToken);
}
