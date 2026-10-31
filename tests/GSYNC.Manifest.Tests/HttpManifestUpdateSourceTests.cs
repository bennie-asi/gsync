using System.Net;
using GSYNC.Manifest.Options;
using GSYNC.Manifest.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace GSYNC.Manifest.Tests;

public sealed class HttpManifestUpdateSourceTests
{
    [Fact]
    public async Task Fetch_ReadsCurrentSettings_AndRejectsOversizedResponses()
    {
        var handler = new Handler();
        var options = new LiveOptions { Value = new() { RemoteManifestUrl = "https://example.test/old" } };
        var source = new HttpManifestUpdateSource(new HttpClient(handler), options);
        Assert.Equal("manifest", await source.TryFetchLatestManifestAsync(CancellationToken.None));
        options.Value = new() { RemoteManifestUrl = "https://example.test/new" };
        Assert.Equal("manifest", await source.TryFetchLatestManifestAsync(CancellationToken.None));
        Assert.Equal(new[] { "/old", "/new" }, handler.Paths);
        handler.Oversized = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => source.TryFetchLatestManifestAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_SurfacesHttpFailureInsteadOfReportingACompletedRefresh()
    {
        var handler = new Handler { Status = HttpStatusCode.Forbidden };
        var source = new HttpManifestUpdateSource(new HttpClient(handler),
            Microsoft.Extensions.Options.Options.Create(new ManifestOptions { RemoteManifestUrl = "https://example.test/catalog" }));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => source.TryFetchLatestManifestAsync(CancellationToken.None));
        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }

    private sealed class LiveOptions : IOptions<ManifestOptions>
    {
        public ManifestOptions Value { get; set; } = new();
    }
    private sealed class Handler : HttpMessageHandler
    {
        public List<string> Paths { get; } = new();
        public bool Oversized { get; set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            var content = new StringContent("manifest");
            if (Oversized) content.Headers.ContentLength = 32 * 1024 * 1024 + 1;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = content });
        }
    }
}
