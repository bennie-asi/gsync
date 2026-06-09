using GSYNC.Core.Abstractions;
using GSYNC.Core.Models;

namespace GSYNC.Providers.Services;

public sealed class CustomSourceProvider : ISourceProvider
{
    public string ProviderId => "custom";

    public string DisplayName => "Custom Folder";

    public Task<IReadOnlyList<DiscoveredGame>> ScanAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<DiscoveredGame>>(Array.Empty<DiscoveredGame>());
    }

    public IReadOnlyDictionary<string, string> ResolveVariables(GameInstance instance)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(instance.InstallDirectory))
        {
            variables["GAME_INSTALL_DIR"] = instance.InstallDirectory;
        }

        return variables;
    }
}
