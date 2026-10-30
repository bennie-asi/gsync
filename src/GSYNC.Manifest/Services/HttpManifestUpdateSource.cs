using GSYNC.Core.Abstractions.Manifest;
using GSYNC.Manifest.Options;
using Microsoft.Extensions.Options;

namespace GSYNC.Manifest.Services;

public sealed class HttpManifestUpdateSource : IManifestUpdateSource
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<ManifestOptions> _options;

    public HttpManifestUpdateSource(HttpClient httpClient, IOptions<ManifestOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<string?> TryFetchLatestManifestAsync(CancellationToken cancellationToken)
    {
        var url = _options.Value.RemoteManifestUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        const int limit = 32 * 1024 * 1024;
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("The downloaded manifest is too large.");
        using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var bytes = new byte[81920];
        int count;
        while ((count = await source.ReadAsync(bytes, cancellationToken)) > 0)
        {
            if (buffer.Length + count > limit) throw new InvalidDataException("The downloaded manifest is too large.");
            await buffer.WriteAsync(bytes.AsMemory(0, count), cancellationToken);
        }
        buffer.Position = 0;
        using var reader = new StreamReader(buffer);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
