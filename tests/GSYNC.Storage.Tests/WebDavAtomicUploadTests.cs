using System.Net;
using System.Text;
using GSYNC.Storage.Options;
using GSYNC.Storage.Services;
using Xunit;

namespace GSYNC.Storage.Tests;

public sealed class WebDavAtomicUploadTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Upload_OnlyReplacesDestinationAfterCompleteTransfer_AndCleansStaging(bool failPut, bool failMove)
    {
        var server = new Server(failPut, failMove);
        var provider = new WebDavStorageProvider(new HttpClient(server), Microsoft.Extensions.Options.Options.Create(new WebDavOptions { BaseUrl = "https://example.test/dav" }));
        using var content = new MemoryStream("replacement"u8.ToArray());
        if (failPut || failMove) await Assert.ThrowsAsync<HttpRequestException>(() => provider.UploadAsync("game/slot.sav", content, CancellationToken.None));
        else await provider.UploadAsync("game/slot.sav", content, CancellationToken.None);

        Assert.Equal(failPut || failMove ? "original" : "replacement", Encoding.UTF8.GetString(server.Files["/dav/game/slot.sav"]));
        Assert.Single(server.Files);
        Assert.DoesNotContain(server.Requests, request => request == "PUT /dav/game/slot.sav");
    }

    [Fact]
    public async Task Download_KeepsResponseAliveUntilTheReturnedStreamIsDisposed()
    {
        var content = new TrackingContent();
        var provider = new WebDavStorageProvider(new HttpClient(new ContentHandler(content)),
            Microsoft.Extensions.Options.Options.Create(new WebDavOptions { BaseUrl = "https://example.test/dav" }));
        var download = await provider.DownloadAsync("slot.sav", CancellationToken.None);
        Assert.False(content.Disposed);
        using (var reader = new StreamReader(download.Content, leaveOpen: true)) Assert.Equal("save-data", await reader.ReadToEndAsync());
        download.Dispose();
        Assert.True(content.Disposed);
    }

    private sealed class ContentHandler(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }

    private sealed class TrackingContent : HttpContent
    {
        public bool Disposed { get; private set; }
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream("save-data"u8.ToArray()));
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync("save-data"u8.ToArray()).AsTask();
        protected override bool TryComputeLength(out long length) { length = 9; return true; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class Server(bool failPut, bool failMove) : HttpMessageHandler
    {
        public Dictionary<string, byte[]> Files { get; } = new() { ["/dav/game/slot.sav"] = "original"u8.ToArray() };
        public List<string> Requests { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(request.Method.Method + " " + path);
            if (request.Method.Method == "PUT")
            {
                Files[path] = failPut ? "partial"u8.ToArray() : await request.Content!.ReadAsByteArrayAsync(cancellationToken);
                return new HttpResponseMessage(failPut ? HttpStatusCode.InternalServerError : HttpStatusCode.Created);
            }
            if (request.Method.Method == "MOVE")
            {
                if (failMove) return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                var destination = new Uri(request.Headers.GetValues("Destination").Single()).AbsolutePath;
                Files[destination] = Files[path];
                Files.Remove(path);
            }
            if (request.Method == HttpMethod.Delete) Files.Remove(path);
            return new HttpResponseMessage(HttpStatusCode.Created);
        }
    }
}
