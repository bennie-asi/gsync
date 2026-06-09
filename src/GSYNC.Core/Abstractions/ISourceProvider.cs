using GSYNC.Core.Models;

namespace GSYNC.Core.Abstractions;

public interface ISourceProvider
{
    string ProviderId { get; }

    string DisplayName { get; }

    Task<IReadOnlyList<DiscoveredGame>> ScanAsync(CancellationToken cancellationToken);

    IReadOnlyDictionary<string, string> ResolveVariables(GameInstance instance);
}
