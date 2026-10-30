using System.Net;
using System.Net.Http.Headers;
using GSYNC.Storage.Options;
using GSYNC.Storage.Services;
using Xunit;

namespace GSYNC.Storage.Tests;

public sealed class WebDavConditionalCommitTests
{
    [Fact]
    public async Task ConditionalCommitsUseStrongETags_AndPreserveAConcurrentWinner()
    {
        var server = new Server();
        var provider = Provider(server);
        using var first = new MemoryStream("first"u8.ToArray());
        Assert.True(await provider.TryUploadAsync("game/current.json", first, null, CancellationToken.None));
        using var read = await provider.DownloadVersionedAsync("game/current.json", CancellationToken.None);
        Assert.Equal("\"1\"", read.Revision);
        using var winner = new MemoryStream("winner"u8.ToArray());
        Assert.True(await provider.TryUploadAsync("game/current.json", winner, read.Revision, CancellationToken.None));
        using var stale = new MemoryStream("stale"u8.ToArray());
        Assert.False(await provider.TryUploadAsync("game/current.json", stale, read.Revision, CancellationToken.None));
        Assert.Equal("winner"u8.ToArray(), server.Bytes);
        Assert.Contains("*", server.Conditions);
        Assert.Contains("\"1\"", server.Conditions);
    }

    [Fact]
    public async Task CredentialsAreNeverSentOverUnencryptedRemoteHttp()
    {
        var server = new Server();
        var provider = new WebDavStorageProvider(new HttpClient(server), Microsoft.Extensions.Options.Options.Create(new WebDavOptions
        { BaseUrl = "http://example.test/dav", Username = "player", Password = "secret" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.DownloadAsync("save.sav", CancellationToken.None));
        Assert.Empty(server.Conditions);
    }

    private static WebDavStorageProvider Provider(HttpMessageHandler handler) => new(new HttpClient(handler),
        Microsoft.Extensions.Options.Options.Create(new WebDavOptions { BaseUrl = "https://example.test/dav" }));

    private sealed class Server : HttpMessageHandler
    {
        public byte[]? Bytes { get; private set; }
        public List<string> Conditions { get; } = new();
        private int _version;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.Method.Method == "MKCOL") return new HttpResponseMessage(HttpStatusCode.Created);
            if (request.Method == HttpMethod.Get)
            {
                var response = new HttpResponseMessage(Bytes is null ? HttpStatusCode.NotFound : HttpStatusCode.OK) { Content = new ByteArrayContent(Bytes ?? Array.Empty<byte>()) };
                response.Headers.ETag = new EntityTagHeaderValue($"\"{_version}\"");
                return response;
            }
            Assert.Equal(HttpMethod.Put, request.Method);
            var condition = request.Headers.IfNoneMatch.Count > 0 ? request.Headers.IfNoneMatch.Single().ToString() : request.Headers.IfMatch.Single().ToString();
            Conditions.Add(condition);
            if ((condition == "*" && Bytes is not null) || (condition != "*" && condition != $"\"{_version}\""))
                return new HttpResponseMessage(HttpStatusCode.PreconditionFailed);
            Bytes = await request.Content!.ReadAsByteArrayAsync(token);
            _version++;
            return new HttpResponseMessage(HttpStatusCode.Created);
        }
    }
}
