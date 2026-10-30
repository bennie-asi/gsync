using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Models;
using GSYNC.Core.Utilities;
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

    public IStorageProvider WithConfiguration(IReadOnlyDictionary<string, string> configuration)
    {
        if (!configuration.TryGetValue("baseUrl", out var baseUrl))
        {
            if (configuration.ContainsKey("targetId")) throw new InvalidOperationException("The selected WebDAV target has no endpoint URL.");
            return new WebDavStorageProvider(_httpClient, Microsoft.Extensions.Options.Options.Create(_options.Value));
        }
        if (string.IsNullOrWhiteSpace(baseUrl)) throw new InvalidOperationException("The selected WebDAV target has an empty endpoint URL.");

        return new WebDavStorageProvider(_httpClient, Microsoft.Extensions.Options.Options.Create(new WebDavOptions
        {
            BaseUrl = baseUrl,
            Username = configuration.GetValueOrDefault("username"),
            Password = configuration.GetValueOrDefault("password"),
        }));
    }

    private const string PropfindBody = """
        <?xml version="1.0" encoding="utf-8" ?>
        <d:propfind xmlns:d="DAV:">
          <d:prop>
            <d:getcontentlength />
            <d:getlastmodified />
            <d:resourcetype />
          </d:prop>
        </d:propfind>
        """;

    public async Task<ConnectionResult> TestConnectionAsync(IReadOnlyDictionary<string, string> configuration, CancellationToken cancellationToken)
    {
        var baseUrl = configuration.TryGetValue("baseUrl", out var url) ? url?.Trim() : null;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return ConnectionResult.Failure("WebDAV endpoint URL is required.");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return ConnectionResult.Failure("WebDAV endpoint URL must be an absolute http(s) URL.");
        }

        try
        {
            // PROPFIND (not OPTIONS) so the request is actually authenticated against the
            // target collection: many servers answer OPTIONS anonymously, which made every
            // credential combination look like a success.
            using var request = CreateRequest(new HttpMethod("PROPFIND"), string.Empty, configuration);
            request.Headers.Add("Depth", "0");
            request.Content = new StringContent(PropfindBody, Encoding.UTF8, "application/xml");
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => ConnectionResult.Failure("Authentication failed (401). Check the username and password."),
                HttpStatusCode.Forbidden => ConnectionResult.Failure("Access denied (403). The account lacks permission for this path."),
                HttpStatusCode.NotFound => ConnectionResult.Failure("The WebDAV path was not found (404). Check the endpoint URL."),
                HttpStatusCode.MethodNotAllowed => ConnectionResult.Failure("The server rejected WebDAV PROPFIND (405). This may not be a WebDAV endpoint."),
                _ when response.IsSuccessStatusCode => ConnectionResult.Success(),
                _ => ConnectionResult.Failure($"WebDAV connection failed with status {(int)response.StatusCode} ({response.ReasonPhrase})."),
            };
        }
        catch (Exception exception)
        {
            return ConnectionResult.Failure(exception.Message);
        }
    }

    /// <summary>
    /// Lists the immediate child collections (folders) under <paramref name="relativePath"/> on the
    /// remote server, authenticating with the supplied ad-hoc configuration. Used by the target editor
    /// so the user can browse remote folders instead of picking a local path.
    /// </summary>
    public async Task<IReadOnlyList<RemoteEntry>> BrowseDirectoriesAsync(IReadOnlyDictionary<string, string> configuration, string relativePath, CancellationToken cancellationToken)
    {
        var requested = relativePath.Replace('\\', '/').TrimEnd('/');
        using var request = CreateRequest(new HttpMethod("PROPFIND"), requested.Length == 0 ? "" : requested + "/", configuration);
        request.Headers.Add("Depth", "1");
        request.Content = new StringContent(PropfindBody, Encoding.UTF8, "application/xml");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(xml)) throw new InvalidDataException("The WebDAV listing has no response body.");
        var document = XDocument.Parse(xml);
        XNamespace dav = "DAV:";
        if (document.Root?.Name != dav + "multistatus") throw new InvalidDataException("The server did not return a WebDAV listing.");
        var entries = new List<RemoteEntry>();
        foreach (var responseElement in document.Root.Elements(dav + "response"))
        {
            var hrefRaw = responseElement.Element(dav + "href")?.Value;
            var path = GetRelativeEntryPath(hrefRaw, requested, includeSelf: true, configuration);
            if (path is null) continue;
            var properties = ReadSuccessfulProperties(responseElement);
            if (path == requested || properties.Element(dav + "resourcetype")?.Element(dav + "collection") is null) continue;
            var name = requested.Length == 0 ? path : path[(requested.Length + 1)..];
            if (name.Length == 0 || name.Contains('/'))
            {
                continue; // only immediate children
            }

            entries.Add(new RemoteEntry { Path = name, IsDirectory = true });
        }

        return entries
            .OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task UploadAsync(string remotePath, Stream content, CancellationToken cancellationToken)
    {
        await EnsureParentCollectionAsync(remotePath, cancellationToken);
        var separator = remotePath.LastIndexOf('/');
        var temporaryPath = (separator < 0 ? string.Empty : remotePath[..(separator + 1)]) + AtomicFile.TemporaryName();
        try
        {
            using (var request = CreateRequest(HttpMethod.Put, temporaryPath))
            {
                request.Content = new StreamContent(content);
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
            }
            cancellationToken.ThrowIfCancellationRequested();
            using var move = CreateRequest(new HttpMethod("MOVE"), temporaryPath);
            move.Headers.Add("Destination", BuildUri(remotePath).AbsoluteUri);
            move.Headers.Add("Overwrite", "T");
            using var moved = await _httpClient.SendAsync(move, cancellationToken);
            moved.EnsureSuccessStatusCode();
        }
        finally
        {
            using var cleanupToken = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                using var cleanup = CreateRequest(HttpMethod.Delete, temporaryPath);
                using var response = await _httpClient.SendAsync(cleanup, cleanupToken.Token);
            }
            catch (HttpRequestException) { }
            catch (OperationCanceledException) { }
        }
    }

    public async Task<StorageDownloadResult> DownloadAsync(string remotePath, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, remotePath);
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        try
        {
            response.EnsureSuccessStatusCode();
            return new StorageDownloadResult
            {
                Content = new ResponseStream(await response.Content.ReadAsStreamAsync(cancellationToken), response),
                ContentType = response.Content.Headers.ContentType?.MediaType,
                Length = response.Content.Headers.ContentLength,
                Revision = response.Headers.ETag is { IsWeak: false } tag ? tag.ToString() : null,
            };
        }
        catch { response.Dispose(); throw; }
    }

    public async Task<IReadOnlyList<RemoteEntry>> ListAsync(string pathNamespace, CancellationToken cancellationToken)
    {
        var initial = pathNamespace.Replace('\\', '/').TrimEnd('/');
        var pending = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { initial };
        var entries = new Dictionary<string, RemoteEntry>(StringComparer.Ordinal);
        pending.Enqueue(initial);
        XNamespace dav = "DAV:";
        while (pending.TryDequeue(out var collection))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var request = CreateRequest(new HttpMethod("PROPFIND"), collection.Length == 0 ? "" : collection + "/");
            // RFC 4918 requires Depth 1 support; servers may disable Depth infinity.
            request.Headers.Add("Depth", "1");
            request.Content = new StringContent(PropfindBody, Encoding.UTF8, "application/xml");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound && collection == initial) return Array.Empty<RemoteEntry>();
            response.EnsureSuccessStatusCode();
            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(xml)) throw new InvalidDataException("The WebDAV listing has no response body.");
            var document = XDocument.Parse(xml);
            if (document.Root?.Name != dav + "multistatus") throw new InvalidDataException("The server did not return a WebDAV listing.");
            foreach (var responseElement in document.Root.Elements(dav + "response"))
            {
                var href = responseElement.Element(dav + "href")?.Value;
                var relativePath = GetRelativeEntryPath(href, collection, includeSelf: true);
                if (relativePath is null) continue;
                var prop = ReadSuccessfulProperties(responseElement);
                if (relativePath == collection || AtomicFile.IsTemporary(relativePath)) continue;
                var isDirectory = prop.Element(dav + "resourcetype")?.Element(dav + "collection") is not null;
                var sizeText = prop.Element(dav + "getcontentlength")?.Value;
                if (!isDirectory && (!long.TryParse(sizeText, out var length) || length < 0))
                    throw new InvalidDataException($"WebDAV did not return a valid size for '{relativePath}'.");
                var entry = new RemoteEntry
                {
                    Path = relativePath,
                    IsDirectory = isDirectory,
                    Size = long.TryParse(sizeText, out var size) ? size : 0,
                    LastModifiedUtc = DateTimeOffset.TryParse(prop.Element(dav + "getlastmodified")?.Value, out var modified) ? modified : null,
                };
                if (entries.TryGetValue(relativePath, out var previous) && previous.IsDirectory != isDirectory)
                    throw new InvalidDataException($"The type of WebDAV resource '{relativePath}' changed while listing.");
                entries[relativePath] = entry;
                if (isDirectory && visited.Add(relativePath)) pending.Enqueue(relativePath);
            }
        }
        return entries.Values.ToArray();
    }

    private static XElement ReadSuccessfulProperties(XElement response)
    {
        XNamespace dav = "DAV:";
        static int Status(XElement? element)
        {
            if (element is null) return 200; // Some otherwise interoperable servers omit this field.
            var parts = element.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var status)) throw new InvalidDataException("Invalid WebDAV resource status.");
            return status;
        }
        static void EnsureStatus(int status)
        {
            if (status is < 200 or >= 300)
                throw new HttpRequestException($"WebDAV could not list a resource (HTTP {status}).", null, (HttpStatusCode)status);
        }
        EnsureStatus(Status(response.Element(dav + "status")));
        var properties = new XElement(dav + "prop");
        foreach (var propstat in response.Elements(dav + "propstat"))
        {
            var status = Status(propstat.Element(dav + "status"));
            var prop = propstat.Element(dav + "prop");
            // Missing optional metadata is legal, but missing resource types or access errors
            // must not turn an inaccessible save directory into an apparently empty backup.
            if (status == 404 && prop?.Element(dav + "resourcetype") is null) continue;
            EnsureStatus(status);
            if (prop is not null) properties.Add(prop.Elements());
        }
        if (properties.Element(dav + "resourcetype") is null) throw new InvalidDataException("WebDAV omitted a resource type.");
        return properties;
    }

    private string? GetRelativeEntryPath(string? href, string pathNamespace, bool includeSelf = false, IReadOnlyDictionary<string, string>? configuration = null)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var baseUri = BuildUri(string.Empty, configuration);
        // WebDAV hrefs may be absolute URLs, server-root paths, or paths relative to the request.
        var requestUri = new Uri(BuildUri(pathNamespace, configuration).AbsoluteUri.TrimEnd('/') + "/");
        var entryUri = new Uri(requestUri, href.Trim());
        if (!baseUri.IsBaseOf(entryUri))
        {
            return null;
        }

        var path = Uri.UnescapeDataString(baseUri.MakeRelativeUri(entryUri).ToString()).Trim('/');
        var requested = pathNamespace.Trim('/');
        if (string.Equals(path, requested, StringComparison.Ordinal)) return includeSelf ? path : null;
        return requested.Length > 0 && !path.StartsWith(requested + '/', StringComparison.Ordinal) ? null : path;
    }

    public async Task DeleteAsync(string remotePath, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Delete, remotePath);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<StorageDownloadResult> DownloadVersionedAsync(string remotePath, CancellationToken cancellationToken) => DownloadAsync(remotePath, cancellationToken);

    public async Task<bool> TryUploadAsync(string remotePath, Stream content, string? expectedRevision, CancellationToken cancellationToken)
    {
        await EnsureParentCollectionAsync(remotePath, cancellationToken);
        using var request = CreateRequest(HttpMethod.Put, remotePath);
        if (expectedRevision is null) request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Any);
        else
        {
            if (!EntityTagHeaderValue.TryParse(expectedRevision, out var tag) || tag.IsWeak)
                throw new InvalidDataException("A strong WebDAV ETag is required to replace a committed save.");
            request.Headers.IfMatch.Add(tag);
        }
        request.Content = new StreamContent(content);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.PreconditionFailed) return false;
        response.EnsureSuccessStatusCode();
        return true;
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
            if (request.RequestUri!.Scheme != Uri.UriSchemeHttps && !request.RequestUri.IsLoopback)
                throw new InvalidOperationException("Authenticated WebDAV requires HTTPS. Plain HTTP is allowed only for localhost development.");
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.Value.username}:{credentials.Value.password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        }

        return request;
    }

    private Uri BuildUri(string relativePath, IReadOnlyDictionary<string, string>? configuration = null)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (Path.IsPathRooted(normalized) || normalized.Split('/').Any(segment => segment is "." or ".." || segment.Contains(':')))
            throw new InvalidDataException("WebDAV paths must remain inside the configured collection.");
        var baseUrl = ResolveBaseUrl(configuration).TrimEnd('/');
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps) ||
            endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 || endpoint.UserInfo.Length != 0)
            throw new InvalidOperationException("WebDAV endpoint must be an absolute HTTP(S) collection URL without a query or fragment.");
        var path = string.Join('/', normalized.Split('/').Select(Uri.EscapeDataString));
        return string.IsNullOrWhiteSpace(path)
            ? new Uri(baseUrl + "/", UriKind.Absolute)
            : new Uri(baseUrl + "/" + path, UriKind.Absolute);
    }

    private string ResolveBaseUrl(IReadOnlyDictionary<string, string>? configuration = null)
    {
        if (configuration is not null)
        {
            return configuration.TryGetValue("baseUrl", out var configured) && !string.IsNullOrWhiteSpace(configured)
                ? configured.Trim() : throw new InvalidOperationException("WebDAV base URL has not been configured.");
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
        var username = configuration is not null ? configuration.GetValueOrDefault("username") : _options.Value.Username;
        var password = configuration is not null ? configuration.GetValueOrDefault("password") : _options.Value.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        return (username, password ?? string.Empty);
    }

    private sealed class ResponseStream(Stream inner, HttpResponseMessage response) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) { inner.Dispose(); response.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
