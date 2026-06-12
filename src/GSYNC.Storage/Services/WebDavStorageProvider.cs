using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Models;
using GSYNC.Storage.Options;
using Microsoft.Extensions.Options;

namespace GSYNC.Storage.Services;

public sealed class WebDavStorageProvider : IStorageProvider
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<WebDavOptions> _options;

    public WebDavStorageProvider(HttpClient httpClient, IOptions<WebDavOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public string ProviderId => "webdav";

    public string DisplayName => "WebDAV";

    public async Task<ConnectionResult> TestConnectionAsync(IReadOnlyDictionary<string, string> configuration, CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Options, string.Empty, configuration);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? ConnectionResult.Success()
                : ConnectionResult.Failure($"WebDAV connection failed with status {(int)response.StatusCode}.");
        }
        catch (Exception exception)
        {
            return ConnectionResult.Failure(exception.Message);
        }
    }

    public async Task UploadAsync(string remotePath, Stream content, CancellationToken cancellationToken)
    {
        await EnsureParentCollectionAsync(remotePath, cancellationToken);
        using var request = CreateRequest(HttpMethod.Put, remotePath);
        request.Content = new StreamContent(content);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<StorageDownloadResult> DownloadAsync(string remotePath, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, remotePath);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new StorageDownloadResult
        {
            Content = new MemoryStream(bytes),
            ContentType = response.Content.Headers.ContentType?.MediaType,
            Length = bytes.LongLength,
        };
    }

    public async Task<IReadOnlyList<RemoteEntry>> ListAsync(string pathNamespace, CancellationToken cancellationToken)
    {
        var method = new HttpMethod("PROPFIND");
        using var request = CreateRequest(method, pathNamespace);
        request.Headers.Add("Depth", "infinity");
        request.Content = new StringContent("""
            <?xml version="1.0" encoding="utf-8" ?>
            <d:propfind xmlns:d="DAV:">
              <d:prop>
                <d:getcontentlength />
                <d:getlastmodified />
                <d:resourcetype />
              </d:prop>
            </d:propfind>
            """, Encoding.UTF8, "application/xml");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(xml))
        {
            return Array.Empty<RemoteEntry>();
        }

        var document = XDocument.Parse(xml);
        XNamespace dav = "DAV:";
        var entries = document.Descendants(dav + "response")
            .Select(responseElement =>
            {
                var href = responseElement.Element(dav + "href")?.Value?.Trim('/') ?? string.Empty;
                var prop = responseElement.Descendants(dav + "prop").FirstOrDefault();
                var isDirectory = prop?.Element(dav + "resourcetype")?.Element(dav + "collection") is not null;
                var sizeText = prop?.Element(dav + "getcontentlength")?.Value;
                var modifiedText = prop?.Element(dav + "getlastmodified")?.Value;
                DateTimeOffset? modified = null;
                if (DateTimeOffset.TryParse(modifiedText, out var parsedModified))
                {
                    modified = parsedModified;
                }

                return new RemoteEntry
                {
                    Path = href,
                    IsDirectory = isDirectory,
                    Size = long.TryParse(sizeText, out var size) ? size : 0,
                    LastModifiedUtc = modified,
                };
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
            .ToArray();

        return entries;
    }

    public async Task DeleteAsync(string remotePath, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Delete, remotePath);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task EnsureParentCollectionAsync(string remotePath, CancellationToken cancellationToken)
    {
        var segments = remotePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 1)
        {
            return;
        }

        var current = new List<string>();
        foreach (var segment in segments.Take(segments.Length - 1))
        {
            current.Add(segment);
            var collectionPath = string.Join('/', current);
            using var request = CreateRequest(new HttpMethod("MKCOL"), collectionPath);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.Created or HttpStatusCode.Conflict)
            {
                continue;
            }

            response.EnsureSuccessStatusCode();
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath, IReadOnlyDictionary<string, string>? configuration = null)
    {
        var request = new HttpRequestMessage(method, BuildUri(relativePath, configuration));
        var credentials = ResolveCredentials(configuration);
        if (credentials is not null)
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.Value.username}:{credentials.Value.password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        }

        return request;
    }

    private Uri BuildUri(string relativePath, IReadOnlyDictionary<string, string>? configuration = null)
    {
        var baseUrl = ResolveBaseUrl(configuration).TrimEnd('/');
        var path = relativePath.TrimStart('/');
        return string.IsNullOrWhiteSpace(path)
            ? new Uri(baseUrl + "/", UriKind.Absolute)
            : new Uri(baseUrl + "/" + path, UriKind.Absolute);
    }

    private string ResolveBaseUrl(IReadOnlyDictionary<string, string>? configuration = null)
    {
        if (configuration is not null && configuration.TryGetValue("baseUrl", out var configured) && !string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var options = _options.Value;
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException("WebDAV base URL has not been configured.");
        }

        return options.BaseUrl;
    }

    private (string username, string password)? ResolveCredentials(IReadOnlyDictionary<string, string>? configuration = null)
    {
        var options = _options.Value;
        var username = configuration is not null && configuration.TryGetValue("username", out var configuredUsername)
            ? configuredUsername
            : options.Username;
        var password = configuration is not null && configuration.TryGetValue("password", out var configuredPassword)
            ? configuredPassword
            : options.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        return (username, password ?? string.Empty);
    }
}
