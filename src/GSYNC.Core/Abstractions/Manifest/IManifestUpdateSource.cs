namespace GSYNC.Core.Abstractions.Manifest;

public interface IManifestUpdateSource
{
    Task<string?> TryFetchLatestManifestAsync(CancellationToken cancellationToken);
}
