using System.Text;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Models;
using GSYNC.Core.Services.Sync;
using GSYNC.Storage.Options;
using GSYNC.Storage.Services;
using Xunit;

namespace GSYNC.Storage.Tests;

public sealed class RemoteSaveSessionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gsync-generation-tests", Guid.NewGuid().ToString("N"));
    private LocalFolderStorageProvider Storage => new(Microsoft.Extensions.Options.Options.Create(new LocalFolderOptions { RootPath = _root }));
    private const string Namespace = "games/example";

    [Fact]
    public async Task ReadersSeeCompleteGenerations_AndRemainPinnedDuringACommit()
    {
        await SeedAsync();
        var oldReader = await OpenAsync();
        var writer = await OpenAsync();
        await PutAsync(writer, "one.sav", "new-one");
        await PutAsync(writer, "two.sav", "new-two");
        Assert.Equal("old-one", await ReadAsync(await OpenAsync(), "one.sav"));
        Assert.Equal("old-two", await ReadAsync(await OpenAsync(), "two.sav"));
        await writer.CommitAsync(CancellationToken.None);
        var latest = await OpenAsync();
        Assert.Equal("new-one", await ReadAsync(latest, "one.sav"));
        Assert.Equal("new-two", await ReadAsync(latest, "two.sav"));
        Assert.Equal("old-one", await ReadAsync(oldReader, "one.sav"));
        Assert.Equal("old-two", await ReadAsync(oldReader, "two.sav"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentWritersCannotOverwriteEachOthersCommit(bool existing)
    {
        if (existing) await SeedAsync();
        var first = await OpenAsync();
        var second = await OpenAsync();
        await PutAsync(first, "one.sav", "first");
        await PutAsync(second, "one.sav", "second");
        var results = await Task.WhenAll(TryCommitAsync(first), TryCommitAsync(second));
        Assert.Single(results, result => result);
        Assert.Equal(results[0] ? "first" : "second", await ReadAsync(await OpenAsync(), "one.sav"));
    }

    [Fact]
    public async Task InterruptedUploadAndCancellationLeaveTheCommittedGenerationUnchanged()
    {
        await SeedAsync();
        var failing = new FailingStorage(Storage);
        var writer = await RemoteSaveSession.OpenAsync(failing, Namespace, CancellationToken.None);
        await PutAsync(writer, "one.sav", "replacement");
        await Assert.ThrowsAsync<IOException>(() => PutAsync(writer, "two.sav", "replacement"));
        var current = await OpenAsync();
        Assert.Equal("old-one", await ReadAsync(current, "one.sav"));
        Assert.Equal("old-two", await ReadAsync(current, "two.sav"));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.CommitAsync(canceled.Token));
        Assert.Equal("old-one", await ReadAsync(await OpenAsync(), "one.sav"));
    }

    [Fact]
    public async Task LegacyImportPreservesUntouchedFiles_AndPinsThemAgainstLegacyWrites()
    {
        await PutAsync(Storage, "one.sav", "old-one");
        await PutAsync(Storage, "two.sav", "old-two");
        var writer = await OpenAsync();
        await PutAsync(writer, "one.sav", "new-one");
        await writer.CommitAsync(CancellationToken.None);
        await PutAsync(Storage, "two.sav", "legacy-client-change");
        Assert.Equal("new-one", await ReadAsync(await OpenAsync(), "one.sav"));
        Assert.Equal("old-two", await ReadAsync(await OpenAsync(), "two.sav"));
    }

    [Fact]
    public async Task RemovingAFileCreatesANewGenerationWithoutChangingPinnedReaders()
    {
        await SeedAsync();
        var oldReader = await OpenAsync();
        var writer = await OpenAsync();
        await writer.DeleteAsync(Namespace + "/default/one.sav", CancellationToken.None);
        Assert.Equal("old-one", await ReadAsync(await OpenAsync(), "one.sav"));
        await writer.CommitAsync(CancellationToken.None);
        Assert.Equal("old-one", await ReadAsync(oldReader, "one.sav"));
        Assert.Equal("old-two", await ReadAsync(await OpenAsync(), "two.sav"));
        var latest = await OpenAsync();
        await Assert.ThrowsAsync<FileNotFoundException>(() => ReadAsync(latest, "one.sav"));
    }

    [Fact]
    public async Task RemovingLegacyFileDoesNotReimportItAndLosingWriterCannotEraseNewerGeneration()
    {
        await PutAsync(Storage, "one.sav", "old-one");
        await PutAsync(Storage, "two.sav", "old-two");
        var remove = await OpenAsync();
        var competing = await OpenAsync();
        await remove.DeleteAsync(Namespace + "/default/one.sav", CancellationToken.None);
        await PutAsync(competing, "two.sav", "new-two");
        await competing.CommitAsync(CancellationToken.None);
        await Assert.ThrowsAsync<RemoteSaveChangedException>(() => remove.CommitAsync(CancellationToken.None));
        Assert.Equal("old-one", await ReadAsync(await OpenAsync(), "one.sav"));
        Assert.Equal("new-two", await ReadAsync(await OpenAsync(), "two.sav"));
        var current = await OpenAsync();
        await current.DeleteAsync(Namespace + "/default/one.sav", CancellationToken.None);
        await current.CommitAsync(CancellationToken.None);
        var latest = await OpenAsync();
        await Assert.ThrowsAsync<FileNotFoundException>(() => ReadAsync(latest, "one.sav"));
        Assert.Equal("new-two", await ReadAsync(latest, "two.sav"));
    }

    [Fact]
    public async Task CorruptManifestIsNeverTreatedAsAnEmptyLegacyStore()
    {
        using var corrupt = new MemoryStream("{bad-json"u8.ToArray());
        await Storage.UploadAsync(Namespace + "/__gsync_meta/current.json", corrupt, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidDataException>(() => OpenAsync());
    }

    private async Task SeedAsync()
    {
        var writer = await OpenAsync();
        await PutAsync(writer, "one.sav", "old-one");
        await PutAsync(writer, "two.sav", "old-two");
        await writer.CommitAsync(CancellationToken.None);
    }
    private Task<RemoteSaveSession> OpenAsync() => RemoteSaveSession.OpenAsync(Storage, Namespace, CancellationToken.None);
    private static async Task PutAsync(IStorageProvider storage, string path, string text)
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(text));
        await storage.UploadAsync(Namespace + "/default/" + path, source, CancellationToken.None);
    }
    private static async Task<string> ReadAsync(IStorageProvider storage, string path)
    {
        using var download = await storage.DownloadAsync(Namespace + "/default/" + path, CancellationToken.None);
        using var reader = new StreamReader(download.Content);
        return await reader.ReadToEndAsync();
    }
    private static async Task<bool> TryCommitAsync(RemoteSaveSession writer)
    {
        try { await writer.CommitAsync(CancellationToken.None); return true; }
        catch (RemoteSaveChangedException) { return false; }
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }

    private sealed class FailingStorage(IStorageProvider inner) : IStorageProvider
    {
        public string ProviderId => inner.ProviderId;
        public string DisplayName => inner.DisplayName;
        public Task<ConnectionResult> TestConnectionAsync(IReadOnlyDictionary<string, string> settings, CancellationToken token) => inner.TestConnectionAsync(settings, token);
        public Task UploadAsync(string path, Stream content, CancellationToken token) => path.EndsWith("two.sav") ? throw new IOException("Disconnected") : inner.UploadAsync(path, content, token);
        public Task<StorageDownloadResult> DownloadAsync(string path, CancellationToken token) => inner.DownloadAsync(path, token);
        public Task<StorageDownloadResult> DownloadVersionedAsync(string path, CancellationToken token) => inner.DownloadVersionedAsync(path, token);
        public Task<bool> TryUploadAsync(string path, Stream content, string? revision, CancellationToken token) => inner.TryUploadAsync(path, content, revision, token);
        public Task<IReadOnlyList<RemoteEntry>> ListAsync(string path, CancellationToken token) => inner.ListAsync(path, token);
        public Task DeleteAsync(string path, CancellationToken token) => inner.DeleteAsync(path, token);
    }
}
