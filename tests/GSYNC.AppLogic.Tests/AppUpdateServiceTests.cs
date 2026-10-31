using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GSYNC.App.Infrastructure;
using GSYNC.Core.Updates;
using Xunit;

namespace GSYNC.AppLogic.Tests;

public sealed class AppUpdateServiceTests
{
    [Fact]
    public async Task ChecksStableRelease_ThrottlesAutomaticChecks_ButAllowsManualChecks()
    {
        using var fixture = new UpdateFixture();
        await fixture.Service.CheckAsync(true, CancellationToken.None);
        Assert.Equal("available", fixture.Service.Snapshot.State);
        Assert.Equal("1.2.3", fixture.Service.Snapshot.Version);
        Assert.Equal(1, fixture.Handler.RequestCount);
        await fixture.Service.CheckAsync(true, CancellationToken.None);
        Assert.Equal(1, fixture.Handler.RequestCount);
        await fixture.Service.CheckAsync(false, CancellationToken.None);
        Assert.Equal(2, fixture.Handler.RequestCount);
        var reloaded = new AppUpdateService(fixture.Root, fixture.Client, "1.0.0");
        await reloaded.CheckAsync(true, CancellationToken.None);
        Assert.Equal(2, fixture.Handler.RequestCount);
    }

    [Theory]
    [InlineData("v1.2.3-beta.1", false, "invalid")]
    [InlineData("v1.2.3", true, "unverified")]
    public async Task RejectsPreviewOrMissingDigest(string tag, bool omitDigest, string expected)
    {
        using var fixture = new UpdateFixture(tag, omitDigest);
        await fixture.Service.CheckAsync(false, CancellationToken.None);
        Assert.Equal(expected, fixture.Service.Snapshot.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.DownloadAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task DownloadsVerifiedBytesAndRemovesTemporaryFileOnMismatch()
    {
        using var fixture = new UpdateFixture();
        await fixture.Service.CheckAsync(false, CancellationToken.None);
        var file = await fixture.Service.DownloadAsync(null, CancellationToken.None);
        Assert.Equal(fixture.Payload, await File.ReadAllBytesAsync(file));
        Assert.Equal(file, fixture.Service.LastDownload?.Path);
        Assert.Equal("1.2.3", fixture.Service.LastDownload?.Version);
        fixture.Handler.BadBytes = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Service.DownloadAsync(null, CancellationToken.None));
        Assert.Empty(Directory.GetFiles(Path.Combine(fixture.Root, "updates"), ".download-*.tmp"));
        Assert.True(File.Exists(file));
        Assert.Equal(file, fixture.Service.LastDownload?.Path);
    }

    [Fact]
    public async Task RefusesRedirectToUntrustedHost()
    {
        using var fixture = new UpdateFixture();
        await fixture.Service.CheckAsync(false, CancellationToken.None);
        fixture.Handler.RedirectOutsideGitHub = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Service.DownloadAsync(null, CancellationToken.None));
        Assert.Empty(Directory.GetFiles(Path.Combine(fixture.Root, "updates"), ".download-*.tmp"));
    }

    [Fact]
    public async Task CancellationDuringDownloadRemovesPartialFile()
    {
        using var fixture = new UpdateFixture();
        await fixture.Service.CheckAsync(false, CancellationToken.None);
        fixture.Handler.BlockDownload = true;
        using var cancellation = new CancellationTokenSource();
        var download = fixture.Service.DownloadAsync(null, cancellation.Token);
        await fixture.Handler.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => download);
        Assert.Empty(Directory.GetFiles(Path.Combine(fixture.Root, "updates"), ".download-*.tmp"));
    }

    [Theory]
    [InlineData(false, "error")]
    [InlineData(true, "limited")]
    public async Task DistinguishesForbiddenFromRateLimit(bool limited, string expected)
    {
        using var fixture = new UpdateFixture();
        fixture.Handler.Forbidden = true;
        fixture.Handler.RateLimited = limited;
        await fixture.Service.CheckAsync(false, CancellationToken.None);
        Assert.Equal(expected, fixture.Service.Snapshot.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SignedDownloadRequiresTrustedSignature(bool tamperSignature)
    {
        using var fixture = new UpdateFixture(signed: true);
        fixture.Handler.BadSignature = tamperSignature;
        await fixture.Service.CheckAsync(false, CancellationToken.None);
        if (tamperSignature)
        {
            await Assert.ThrowsAsync<CryptographicException>(() => fixture.Service.DownloadAsync(null, CancellationToken.None));
            Assert.Null(fixture.Service.LastDownload);
            Assert.Empty(Directory.GetFiles(Path.Combine(fixture.Root, "updates"), ".download-*.tmp"));
        }
        else
        {
            await fixture.Service.DownloadAsync(null, CancellationToken.None);
            Assert.True(fixture.Service.LastDownload?.IsSigned);
        }
    }

    [Theory]
    [InlineData(false, "available")]
    [InlineData(true, "limited")]
    public async Task RateLimitedApiFallsBackOnlyToTrustedSignedMetadata(bool badSignature, string expected)
    {
        using var fixture = new UpdateFixture(signed: true);
        fixture.Handler.Forbidden = true;
        fixture.Handler.RateLimited = true;
        fixture.Handler.RedirectLatest = true;
        fixture.Handler.BadSignature = badSignature;
        await fixture.Service.CheckAsync(false, CancellationToken.None);
        Assert.Equal(expected, fixture.Service.Snapshot.State);
        if (!badSignature)
        {
            await fixture.Service.DownloadAsync(null, CancellationToken.None);
            Assert.True(fixture.Service.LastDownload?.IsSigned);
        }
    }

    private sealed class UpdateFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "gsync-update-tests", Guid.NewGuid().ToString("N"));
        public byte[] Payload { get; } = Encoding.UTF8.GetBytes("sample release payload");
        public Handler Handler { get; }
        public HttpClient Client { get; }
        public AppUpdateService Service { get; }
        private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        public UpdateFixture(string tag = "v1.2.3", bool omitDigest = false, bool signed = false)
        {
            Handler = new Handler(Payload, tag, omitDigest, signed ? _key : null);
            Client = new HttpClient(Handler);
            Service = new AppUpdateService(Root, Client, "1.0.0", trustedPublicKey: _key.ExportSubjectPublicKeyInfoPem());
        }

        public void Dispose() { Client.Dispose(); _key.Dispose(); if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }

    private sealed class Handler(byte[] payload, string tag, bool omitDigest, ECDsa? signingKey) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public bool BadBytes { get; set; }
        public bool RedirectOutsideGitHub { get; set; }
        public bool BlockDownload { get; set; }
        public bool Forbidden { get; set; }
        public bool RateLimited { get; set; }
        public bool BadSignature { get; set; }
        public bool RedirectLatest { get; set; }
        public TaskCompletionSource DownloadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (request.RequestUri!.Host == "api.github.com")
            {
                if (Forbidden)
                {
                    var forbidden = new HttpResponseMessage(HttpStatusCode.Forbidden);
                    if (RateLimited) forbidden.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "0");
                    return Task.FromResult(forbidden);
                }
                var version = tag.TrimStart('v');
                var name = $"GSYNC-{version}-win-x64.zip";
                var prefix = $"GSYNC-{version}-win-x64";
                var assets = new List<object> { new { name, browser_download_url = $"https://github.com/bennie-asi/gsync/releases/download/{tag}/{name}",
                    size = payload.Length, digest = omitDigest ? null : "sha256:" + Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant() } };
                if (signingKey is not null)
                    foreach (var (suffix, size) in new[] { (".manifest.json", 1000), (".manifest.sig", 64) })
                        assets.Add(new { name = prefix + suffix, browser_download_url = $"https://github.com/bennie-asi/gsync/releases/download/{tag}/{prefix}{suffix}", size });
                var body = JsonSerializer.Serialize(new { tag_name = tag, draft = false, prerelease = false, body = "Release notes", assets });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
            }
            if (RedirectLatest && request.RequestUri.AbsolutePath.EndsWith("/releases/latest", StringComparison.Ordinal))
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                redirect.Headers.Location = new Uri($"https://github.com/bennie-asi/gsync/releases/tag/{tag}");
                return Task.FromResult(redirect);
            }
            if (RedirectOutsideGitHub)
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                redirect.Headers.Location = new Uri("https://example.com/archive.zip");
                return Task.FromResult(redirect);
            }
            if (BlockDownload) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new BlockingStream(DownloadStarted)) });
            if (signingKey is not null && request.RequestUri.AbsolutePath.EndsWith(".manifest.json", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Manifest()) });
            if (signingKey is not null && request.RequestUri.AbsolutePath.EndsWith(".manifest.sig", StringComparison.Ordinal))
            {
                var signature = BadSignature ? new byte[64] : signingKey.SignData(Manifest(), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(signature) });
            }
            var bytes = BadBytes ? Enumerable.Repeat((byte)0, payload.Length).ToArray() : payload;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        }

        private byte[] Manifest()
        {
            var version = tag.TrimStart('v');
            return JsonSerializer.SerializeToUtf8Bytes(new UpdateManifest(1, version, $"GSYNC-{version}-win-x64.zip", Convert.ToHexString(SHA256.HashData(payload)), payload.Length));
        }
    }

    private sealed class BlockingStream(TaskCompletionSource started) : Stream
    {
        private bool _sent;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_sent) { _sent = true; buffer.Span[0] = 1; started.TrySetResult(); return 1; }
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
