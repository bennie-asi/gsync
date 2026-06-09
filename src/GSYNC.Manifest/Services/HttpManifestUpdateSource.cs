using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Manifest.Options;
using Microsoft.Extensions.Options;

namespace GSYNC.Manifest.Services;

public sealed class HttpManifestUpdateSource : IManifestUpdateSource
{
    private readonly HttpClient _httpClient;
    private readonly ManifestOptions _options;

    public HttpManifestUpdateSource(HttpClient httpClient, IOptions<ManifestOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string?> TryFetchLatestManifestAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.RemoteManifestUrl))
        {
            return null;
        }

        using var response = await _httpClient.GetAsync(_options.RemoteManifestUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
