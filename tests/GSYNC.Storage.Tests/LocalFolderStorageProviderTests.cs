using System.Text;
using GSYNC.Core.Models;
using GSYNC.Storage.Options;
using GSYNC.Storage.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace GSYNC.Storage.Tests;

public sealed class LocalFolderStorageProviderTests : IDisposable
{
    private readonly string _rootPath;
    private readonly LocalFolderStorageProvider _provider;

    public LocalFolderStorageProviderTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "gsync-storage-tests", Guid.NewGuid().ToString("N"));
        _provider = new LocalFolderStorageProvider(Microsoft.Extensions.Options.Options.Create(new LocalFolderOptions
        {
            RootPath = _rootPath,
        }));
    }

    [Fact]
    public async Task TestConnectionAsync_CreatesRootDirectory()
    {
        var result = await _provider.TestConnectionAsync(new Dictionary<string, string>(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(Directory.Exists(_rootPath));
    }

    [Fact]
    public async Task UploadDownloadListDelete_RoundTripsFile()
    {
        var payload = "hello gsync"u8.ToArray();
        await using (var uploadStream = new MemoryStream(payload))
        {
            await _provider.UploadAsync("games/game-a/save1.sav", uploadStream, CancellationToken.None);
        }

        var entries = await _provider.ListAsync("games", CancellationToken.None);
        Assert.Contains(entries, entry => entry.Path == "games/game-a/save1.sav" && !entry.IsDirectory);

        using var download = await _provider.DownloadAsync("games/game-a/save1.sav", CancellationToken.None);
        using var reader = new StreamReader(download.Content, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();
        Assert.Equal("hello gsync", content);

        await _provider.DeleteAsync("games/game-a/save1.sav", CancellationToken.None);
        var entriesAfterDelete = await _provider.ListAsync("games", CancellationToken.None);
        Assert.DoesNotContain(entriesAfterDelete, entry => entry.Path == "games/game-a/save1.sav");
    }

    [Fact]
    public async Task ScopedProviders_UseSelectedRoots_WithoutChangingEachOther()
    {
        var firstRoot = Path.Combine(_rootPath, "A");
        var secondRoot = Path.Combine(_rootPath, "B");
        var first = _provider.WithConfiguration(new Dictionary<string, string> { ["rootPath"] = firstRoot });
        var second = _provider.WithConfiguration(new Dictionary<string, string> { ["rootPath"] = secondRoot });
        await first.UploadAsync("game/slot.sav", new MemoryStream("first"u8.ToArray()), CancellationToken.None);
        await second.UploadAsync("game/slot.sav", new MemoryStream("second"u8.ToArray()), CancellationToken.None);

        Assert.Equal("first", await File.ReadAllTextAsync(Path.Combine(firstRoot, "game/slot.sav")));
        Assert.Equal("second", await File.ReadAllTextAsync(Path.Combine(secondRoot, "game/slot.sav")));
        using var download = await second.DownloadAsync("game/slot.sav", CancellationToken.None);
        using var reader = new StreamReader(download.Content);
        Assert.Equal("second", await reader.ReadToEndAsync());
        Assert.Single(await second.ListAsync("game", CancellationToken.None), entry => !entry.IsDirectory);
        await second.DeleteAsync("game/slot.sav", CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(firstRoot, "game/slot.sav")));
        Assert.False(File.Exists(Path.Combine(secondRoot, "game/slot.sav")));
    }

    [Theory]
    [InlineData("../outside.sav")]
    [InlineData("..\\outside.sav")]
    [InlineData("C:/outside.sav")]
    [InlineData("")]
    public async Task Mutations_RejectPathsOutsideTheConfiguredRoot(string path)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => _provider.UploadAsync(path, new MemoryStream("data"u8.ToArray()), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(async () => await _provider.DeleteAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task PathsThroughJunctionsInsideStorage_CannotReadOrOverwriteOutsideFiles()
    {
        Directory.CreateDirectory(_rootPath);
        var outside = _rootPath + "-outside";
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "slot.sav"), "outside");
        var link = Path.Combine(_rootPath, "link");
        if (OperatingSystem.IsWindows())
        {
            var info = new System.Diagnostics.ProcessStartInfo("cmd.exe") { CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "/c", "mklink", "/J", link, outside }) info.ArgumentList.Add(argument);
            using var process = System.Diagnostics.Process.Start(info)!;
            await process.WaitForExitAsync();
            Assert.Equal(0, process.ExitCode);
        }
        else Directory.CreateSymbolicLink(link, outside);
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => _provider.UploadAsync("link/slot.sav", new MemoryStream("changed"u8.ToArray()), CancellationToken.None));
            await Assert.ThrowsAsync<InvalidDataException>(() => _provider.DownloadAsync("link/slot.sav", CancellationToken.None));
            await Assert.ThrowsAsync<InvalidDataException>(() => _provider.DeleteAsync("link/slot.sav", CancellationToken.None));
            Assert.Equal("outside", await File.ReadAllTextAsync(Path.Combine(outside, "slot.sav")));
        }
        finally { Directory.Delete(link); Directory.Delete(outside, true); }
    }

    [Fact]
    public async Task InterruptedUpload_PreservesRemoteFile()
    {
        await _provider.UploadAsync("slot.sav", new MemoryStream("original"u8.ToArray()), CancellationToken.None);
        using var content = new InterruptedStream();
        await Assert.ThrowsAsync<IOException>(() => _provider.UploadAsync("slot.sav", content, CancellationToken.None));
        Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(_rootPath, "slot.sav")));
        Assert.Single(Directory.GetFiles(_rootPath));
    }

    [Fact]
    public async Task InvalidBoundTarget_NeverFallsBackToAnotherConfiguredFolder()
    {
        Assert.Throws<InvalidOperationException>(() => _provider.WithConfiguration(new Dictionary<string, string> { ["targetId"] = Guid.NewGuid().ToString() }));
        Assert.Throws<InvalidOperationException>(() => _provider.WithConfiguration(new Dictionary<string, string> { ["rootPath"] = "" }));
        Assert.False((await _provider.TestConnectionAsync(new Dictionary<string, string> { ["rootPath"] = "" }, CancellationToken.None)).IsSuccess);
        Assert.False(Directory.Exists(_rootPath));
    }

    private sealed class InterruptedStream : Stream
    {
        private bool _read;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 20;
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_read) throw new IOException("Interrupted transfer");
            _read = true;
            buffer[offset] = 1;
            return 1;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
