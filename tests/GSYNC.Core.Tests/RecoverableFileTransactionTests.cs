using System.Text.Json;
using GSYNC.Core.Utilities;
using Xunit;

namespace GSYNC.Core.Tests;

public sealed class RecoverableFileTransactionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gsync-recovery-tests", Guid.NewGuid().ToString("N"));
    private string Journals => Path.Combine(_root, "transactions");
    public RecoverableFileTransactionTests() { Directory.CreateDirectory(_root); Directory.CreateDirectory(Journals); }

    [Fact]
    public async Task CompletedReplacementLeavesNoJournalOrRecoveryCopies()
    {
        var target = Path.Combine(_root, "save.sav");
        await File.WriteAllTextAsync(target, "old");
        using var content = new MemoryStream("new"u8.ToArray());
        using var prepared = await AtomicFile.PrepareAsync(content, target, CancellationToken.None);
        AtomicFile.Apply(new[] { prepared }, Journals);
        Assert.Equal("new", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.GetFiles(Journals));
        Assert.DoesNotContain(Directory.GetFiles(_root), AtomicFile.IsTemporary);
    }

    [Fact]
    public async Task StartupRollsBackAnInterruptedCommit_IncludingNewlyCreatedFiles()
    {
        var (path, entries) = CreateInterruptedJournal(committed: false);
        RecoverableFileTransaction.RecoverAll(Journals);
        RecoverableFileTransaction.RecoverAll(Journals);
        Assert.Equal("old", await File.ReadAllTextAsync(entries[0].Target));
        Assert.False(File.Exists(entries[1].Target));
        Assert.False(File.Exists(path));
        Assert.False(File.Exists(entries[0].Backup));
    }

    [Fact]
    public async Task StartupKeepsDurablyCommittedFiles_AndOnlyCleansRecoveryCopies()
    {
        var (path, entries) = CreateInterruptedJournal(committed: true);
        RecoverableFileTransaction.RecoverAll(Journals);
        Assert.Equal("new", await File.ReadAllTextAsync(entries[0].Target));
        Assert.Equal("added", await File.ReadAllTextAsync(entries[1].Target));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task RecoveryNeverOverwritesProgressSavedAfterTheInterruptedOperation()
    {
        var (path, entries) = CreateInterruptedJournal(committed: false);
        await File.WriteAllTextAsync(entries[0].Target, "later-progress");
        Assert.Throws<IOException>(() => RecoverableFileTransaction.RecoverAll(Journals));
        Assert.Equal("later-progress", await File.ReadAllTextAsync(entries[0].Target));
        Assert.Equal("old", await File.ReadAllTextAsync(entries[0].Backup!));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task ChangedLocalSaveIsRejectedBeforeAnyReplacement()
    {
        var target = Path.Combine(_root, "save.sav");
        await File.WriteAllTextAsync(target, "old");
        var original = RecoverableFileTransaction.Hash(target);
        using var content = new MemoryStream("remote"u8.ToArray());
        using var prepared = await AtomicFile.PrepareAsync(content, target, CancellationToken.None);
        prepared.VerifyOriginal = true;
        prepared.ExpectedOriginalHash = original;
        await File.WriteAllTextAsync(target, "new-game-progress");
        Assert.Throws<IOException>(() => AtomicFile.Apply(new[] { prepared }, Journals));
        Assert.Equal("new-game-progress", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.GetFiles(Journals));
    }

    [Fact]
    public async Task CorruptRecoveryCopyNeverReplacesAValidCurrentSave()
    {
        var (path, entries) = CreateInterruptedJournal(committed: false);
        await File.WriteAllTextAsync(entries[0].Backup!, "corrupt-backup");
        Assert.Throws<IOException>(() => RecoverableFileTransaction.RecoverAll(Journals));
        Assert.Equal("new", await File.ReadAllTextAsync(entries[0].Target));
        Assert.True(File.Exists(path));
    }

    private (string Path, Entry[] Entries) CreateInterruptedJournal(bool committed)
    {
        var target = Path.Combine(_root, "save.sav");
        File.WriteAllText(target, "old");
        var original = RecoverableFileTransaction.Hash(target);
        var temp = Path.Combine(_root, AtomicFile.TemporaryName());
        File.WriteAllText(temp, "new");
        var replacement = RecoverableFileTransaction.Hash(temp);
        var backup = Path.Combine(_root, AtomicFile.TemporaryName());
        File.Replace(temp, target, backup);
        var added = Path.Combine(_root, "added.sav");
        File.WriteAllText(added, "added");
        var entries = new[] { new Entry(target, temp, backup, original, replacement),
            new Entry(added, Path.Combine(_root, AtomicFile.TemporaryName()), null, null, RecoverableFileTransaction.Hash(added)) };
        var path = Path.Combine(Journals, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { Version = 1, Committed = committed, Entries = entries }));
        return (path, entries);
    }
    private sealed record Entry(string Target, string? Temporary, string? Backup, string? OriginalHash, string? NewHash);
    public void Dispose() => Directory.Delete(_root, recursive: true);
}
