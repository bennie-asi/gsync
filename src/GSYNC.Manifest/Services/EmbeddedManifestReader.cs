using System.Reflection;
using GSYNC.Manifest.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GSYNC.Manifest.Services;

public sealed class EmbeddedManifestReader
{
    private readonly ManifestOptions _options;
    private readonly ILogger<EmbeddedManifestReader> _logger;

    public EmbeddedManifestReader(IOptions<ManifestOptions> options, ILogger<EmbeddedManifestReader> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> ReadAsync(CancellationToken cancellationToken)
    {
        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(_options.EmbeddedResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{_options.EmbeddedResourceName}' was not found.");

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken);
        _logger.LogInformation("Loaded embedded Ludusavi manifest resource {ResourceName}.", _options.EmbeddedResourceName);
        return content;
    }
}
