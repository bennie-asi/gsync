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

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
