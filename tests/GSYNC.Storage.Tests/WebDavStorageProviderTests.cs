using System.Net;
using System.Text;
using GSYNC.Storage.Options;
using GSYNC.Storage.Services;
using Xunit;

namespace GSYNC.Storage.Tests;

public sealed class WebDavStorageProviderTests
{
    [Theory]
    [InlineData("/dav/backups/game/saves/slot%20%23%3F.sav")]
    [InlineData("https://example.test/dav/backups/game/saves/slot%20%23%3F.sav")]
    [InlineData("slot%20%23%3F.sav")]
    public async Task ListAndDownload_RoundTripHrefFormsAndEscapedFileNames(string href)
    {
        var handler = new RecordingHandler(href);
        var provider = Create(handler);

        var entry = Assert.Single(await provider.ListAsync("game/saves", CancellationToken.None));
        Assert.Equal("game/saves/slot #?.sav", entry.Path);
        using var download = await provider.DownloadAsync(entry.Path, CancellationToken.None);

        Assert.Equal("https://example.test/dav/backups/game/saves/slot%20%23%3F.sav", handler.Requests.Last().uri.AbsoluteUri);
        using var reader = new StreamReader(download.Content);
        Assert.Equal("save-data", await reader.ReadToEndAsync());
    }

    [Theory]
    [InlineData("/dav/backups/game/saves/")]
    [InlineData("/dav/other/game/saves/slot.sav")]
    [InlineData("https://other.test/dav/backups/game/saves/slot.sav")]
    [InlineData("/dav/backups/game/other/slot.sav")]
    public async Task List_ExcludesCollectionItselfAndEntriesOutsideRequestedNamespace(string href)
    {
        Assert.Empty(await Create(new RecordingHandler(href)).ListAsync("game/saves", CancellationToken.None));
    }

    [Fact]
    public async Task ScopedProvider_UsesSelectedEndpointAndCredentials_WithoutLeakingDefaultCredentials()
    {
        var handler = new RecordingHandler("unused");
        var provider = Create(handler);
        var selected = provider.WithConfiguration(new Dictionary<string, string>
        {
            ["baseUrl"] = "https://selected.test/storage",
            ["username"] = "selected-user",
            ["password"] = "selected-pass",
        });
        var anonymous = provider.WithConfiguration(new Dictionary<string, string> { ["baseUrl"] = "https://anonymous.test/storage" });

        using var first = await selected.DownloadAsync("slot.sav", CancellationToken.None);
        using var second = await anonymous.DownloadAsync("slot.sav", CancellationToken.None);
        using var third = await provider.DownloadAsync("slot.sav", CancellationToken.None);

        Assert.Equal("https://selected.test/storage/slot.sav", handler.Requests[0].uri.AbsoluteUri);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("selected-user:selected-pass")), handler.Requests[0].auth);
        Assert.Equal("https://anonymous.test/storage/slot.sav", handler.Requests[1].uri.AbsoluteUri);
        Assert.Null(handler.Requests[1].auth);
        Assert.Equal("https://example.test/dav/backups/slot.sav", handler.Requests[2].uri.AbsoluteUri);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("default-user:default-pass")), handler.Requests[2].auth);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task List_MissingCollectionIsEmpty_WhileOtherErrorsAreReported(HttpStatusCode status)
    {
        var provider = Create(new StatusHandler(status));
        if (status == HttpStatusCode.NotFound)
            Assert.Empty(await provider.ListAsync("new-game/saves", CancellationToken.None));
        else
            await Assert.ThrowsAsync<HttpRequestException>(() => provider.ListAsync("new-game/saves", CancellationToken.None));
    }

    [Fact]
    public async Task Browse_HandlesMissingDirectorySizeBeforeSuccessfulResourceType_AndRejectsOtherHosts()
    {
        const string xml = """
            <d:multistatus xmlns:d='DAV:'>
              <d:response><d:href>/dav/backups/</d:href><d:propstat><d:prop><d:getcontentlength/></d:prop><d:status>HTTP/1.1 404 Not Found</d:status></d:propstat><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>
              <d:response><d:href>/dav/backups/Game%20%231/</d:href><d:propstat><d:prop><d:getcontentlength/></d:prop><d:status>HTTP/1.1 404 Not Found</d:status></d:propstat><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>
              <d:response><d:href>https://other.test/dav/backups/outside/</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>
            </d:multistatus>
            """;
        var provider = Create(new XmlHandler(xml));
        var directories = await provider.BrowseDirectoriesAsync(new Dictionary<string, string> { ["baseUrl"] = "https://example.test/dav/backups" }, "", CancellationToken.None);
        Assert.Equal("Game #1", Assert.Single(directories).Path);
    }

    [Fact]
    public async Task List_TraversesNestedDirectoriesUsingDepthOne_AndToleratesMissingOptionalMetadata()
    {
        var handler = new TreeHandler();
        var entries = await Create(handler).ListAsync("game/saves", CancellationToken.None);
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, entry => entry.IsDirectory && entry.Path == "game/saves/sub one");
        Assert.Contains(entries, entry => !entry.IsDirectory && entry.Path == "game/saves/sub one/slot.sav" && entry.Size == 9);
        Assert.Equal(2, handler.Paths.Count);
        Assert.Equal(2, handler.Paths.Distinct().Count());
    }

    [Theory]
    [InlineData("resource")]
    [InlineData("property")]
    public async Task List_ReportsAccessDeniedInsideMultiStatus_IncludingTheRequestedCollection(string denial)
    {
        await Assert.ThrowsAsync<HttpRequestException>(() => Create(new TreeHandler(denial)).ListAsync("game/saves", CancellationToken.None));
    }

    [Fact]
    public async Task AdHocConnectionAndBrowse_DoNotSendAnotherTargetsCredentials()
    {
        var handler = new RecordingHandler("unused");
        var provider = Create(handler);
        var config = new Dictionary<string, string> { ["baseUrl"] = "https://selected.test/storage" };
        Assert.True((await provider.TestConnectionAsync(config, CancellationToken.None)).IsSuccess);
        await provider.BrowseDirectoriesAsync(config, "", CancellationToken.None);
        Assert.All(handler.Requests, request => Assert.Null(request.auth));
    }

    [Theory]
    [InlineData("ftp://example.test/dav")]
    [InlineData("https://example.test/dav?token=x")]
    [InlineData("https://example.test/dav#folder")]
    public async Task InvalidEndpoint_IsRejectedBeforeAnyRequest(string url)
    {
        var handler = new RecordingHandler("unused");
        var provider = new WebDavStorageProvider(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(new WebDavOptions { BaseUrl = url }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.DownloadAsync("slot.sav", CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void InvalidBoundTarget_NeverUsesDefaultEndpointOrCredentials()
    {
        var provider = Create(new RecordingHandler("unused"));
        Assert.Throws<InvalidOperationException>(() => provider.WithConfiguration(new Dictionary<string, string> { ["targetId"] = Guid.NewGuid().ToString() }));
        Assert.Throws<InvalidOperationException>(() => provider.WithConfiguration(new Dictionary<string, string> { ["baseUrl"] = "" }));
    }

    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status));
    }

    private sealed class XmlHandler(string xml) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage((HttpStatusCode)207) { Content = new StringContent(xml) });
    }

    private sealed class TreeHandler(string? denial = null) : HttpMessageHandler
    {
        public List<string> Paths { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("1", Assert.Single(request.Headers.GetValues("Depth")));
            var path = request.RequestUri!.AbsolutePath.TrimEnd('/');
            Paths.Add(path);
            var self = $"<d:response><d:href>{path}/</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>";
            string child;
            if (denial == "resource") self = $"<d:response><d:href>{path}/</d:href><d:status>HTTP/1.1 403 Forbidden</d:status></d:response>";
            if (path.EndsWith("/saves"))
                child = "<d:response><d:href>sub%20one/</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>";
            else
                child = $"<d:response><d:href>{path}/slot.sav</d:href><d:propstat><d:prop><d:resourcetype/><d:getcontentlength>9</d:getcontentlength></d:prop><d:status>HTTP/1.1 {(denial == "property" ? "403 Forbidden" : "200 OK")}</d:status></d:propstat><d:propstat><d:prop><d:getlastmodified/></d:prop><d:status>HTTP/1.1 404 Not Found</d:status></d:propstat></d:response>";
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)207) { Content = new StringContent($"<d:multistatus xmlns:d='DAV:'>{self}{child}</d:multistatus>") });
        }
    }

    private static WebDavStorageProvider Create(HttpMessageHandler handler) => new(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(new WebDavOptions
    {
        BaseUrl = "https://example.test/dav/backups",
        Username = "default-user",
        Password = "default-pass",
    }));

    private sealed class RecordingHandler(string href) : HttpMessageHandler
    {
        public List<(Uri uri, string? auth)> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!, request.Headers.Authorization?.ToString()));
            return Task.FromResult(request.Method.Method == "PROPFIND"
                ? new HttpResponseMessage((HttpStatusCode)207)
                {
                    Content = new StringContent($"<d:multistatus xmlns:d='DAV:'><d:response><d:href>{href}</d:href><d:propstat><d:prop><d:getcontentlength>9</d:getcontentlength><d:resourcetype/></d:prop></d:propstat></d:response></d:multistatus>"),
                }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("save-data") });
        }
    }
}
