using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GSYNC.Core.Models;
using GSYNC.Core.Services.Sync;
using GSYNC.Core.Updates;
using Xunit;

namespace GSYNC.Core.Tests;

public sealed class UpdateTransactionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gsync-update-transaction-tests", Guid.NewGuid().ToString("N"));
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private string Install => Path.Combine(_root, "install");
    private string Data => Path.Combine(_root, "data");
    private string Session => Path.Combine(Data, "updates", "0123456789abcdef0123456789abcdef");
    private string PublicKey => _key.ExportSubjectPublicKeyInfoPem();

    public UpdateTransactionTests()
    {
        Directory.CreateDirectory(Install);
        Directory.CreateDirectory(Session);
        WriteInstalled("GSYNC.App.exe", "old app");
        WriteInstalled("Updater/GSYNC.Updater.exe", "old helper");
        WriteInstalled("old.dll", "old library");
        File.WriteAllText(Path.Combine(Install, UpdateTransaction.ManagedFilesName), JsonSerializer.Serialize(new[]
            { "GSYNC.App.exe", "Updater/GSYNC.Updater.exe", "old.dll", UpdateTransaction.ManagedFilesName }));
    }

    [Fact]
    public async Task SignedPackageCanBeAppliedAndCommittedWithoutTouchingDataOrUnlistedFiles()
    {
        File.WriteAllText(Path.Combine(Data, "user-data"), "save");
        File.WriteAllText(Path.Combine(Install, "user-file"), "keep");
        await CreatePackageAsync();
        var journal = await PrepareAsync();
        UpdateTransaction.Apply(Session, journal);
        UpdateTransaction.Commit(Session, journal);
        Assert.True(UpdateTransaction.ReadJournal(Session).Committed);
        Assert.Equal("new app", File.ReadAllText(Path.Combine(Install, "GSYNC.App.exe")));
        Assert.Equal("old library", File.ReadAllText(Path.Combine(Install, "old.dll")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(Install, "user-file")));
        Assert.Equal("save", File.ReadAllText(Path.Combine(Data, "user-data")));
        Assert.Throws<InvalidOperationException>(() => UpdateTransaction.Rollback(Session, UpdateTransaction.ReadJournal(Session)));
    }

    [Fact]
    public async Task InterruptedApplyCanBeRecoveredFromDurableJournal()
    {
        await CreatePackageAsync();
        var journal = await PrepareAsync();
        Assert.Throws<IOException>(() => UpdateTransaction.Apply(Session, journal, count => { if (count == 2) throw new IOException("power loss"); }));
        Assert.Equal(Session, UpdateTransaction.FindPendingRecovery(Install, Data));
        UpdateTransaction.Rollback(Session, UpdateTransaction.ReadJournal(Session));
        UpdateTransaction.Rollback(Session, UpdateTransaction.ReadJournal(Session));
        Assert.True(UpdateTransaction.ReadJournal(Session).RolledBack);
        Assert.Null(UpdateTransaction.FindPendingRecovery(Install, Data));
        Assert.Equal("old app", File.ReadAllText(Path.Combine(Install, "GSYNC.App.exe")));
        Assert.Equal("old helper", File.ReadAllText(Path.Combine(Install, "Updater", "GSYNC.Updater.exe")));
        Assert.False(File.Exists(Path.Combine(Install, "new.dll")));
    }

    [Fact]
    public async Task RecoveryPreservesBackupsWhenInstalledFilesChangeOrBackupIsDamaged()
    {
        await CreatePackageAsync();
        var journal = await PrepareAsync();
        UpdateTransaction.Apply(Session, journal);
        var target = Path.Combine(Install, "GSYNC.App.exe");
        File.WriteAllText(target, "changed externally");
        Assert.Throws<IOException>(() => UpdateTransaction.Rollback(Session, journal));
        Assert.Equal("old app", File.ReadAllText(Path.Combine(Session, "backup", "GSYNC.App.exe")));
        Assert.Equal("changed externally", File.ReadAllText(target));
        File.WriteAllText(target, "new app");
        File.Delete(Path.Combine(Session, "backup", "GSYNC.App.exe"));
        Assert.Throws<IOException>(() => UpdateTransaction.Rollback(Session, journal));
        Assert.Equal("new app", File.ReadAllText(target));
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("archive")]
    [InlineData("version")]
    [InlineData("digest")]
    public async Task RefusesInvalidSignatureOrArchive(string defect)
    {
        await CreatePackageAsync(badDigest: defect == "digest");
        if (defect == "signature") File.WriteAllBytes(Path.Combine(Session, "manifest.sig"), new byte[64]);
        if (defect == "archive") await File.AppendAllTextAsync(Path.Combine(Session, "package.zip"), "tamper");
        await Assert.ThrowsAnyAsync<Exception>(() => UpdateTransaction.PrepareAsync(Install, Data, Session, defect == "version" ? "2.0.0" : "1.2.3", PublicKey));
        Assert.Equal("old app", File.ReadAllText(Path.Combine(Install, "GSYNC.App.exe")));
    }

    [Theory]
    [InlineData("../escape.dll", false, false)]
    [InlineData("/absolute.dll", false, false)]
    [InlineData("link.dll", true, false)]
    [InlineData("GSYNC.App.exe", false, true)]
    public async Task RefusesUnsafeArchiveEntries(string name, bool link, bool duplicate)
    {
        await CreatePackageAsync(extraName: name, link: link, duplicate: duplicate);
        await Assert.ThrowsAnyAsync<Exception>(PrepareAsync);
        Assert.Equal("old app", File.ReadAllText(Path.Combine(Install, "GSYNC.App.exe")));
    }

    [Fact]
    public async Task RefusesToOverwriteAnUnmanagedFile()
    {
        File.WriteAllText(Path.Combine(Install, "new.dll"), "user owned");
        await CreatePackageAsync();
        await Assert.ThrowsAsync<IOException>(PrepareAsync);
        Assert.Equal("user owned", File.ReadAllText(Path.Combine(Install, "new.dll")));
    }

    [Fact]
    public void RefusesOverlappingDataAndInstallDirectories()
        => Assert.Throws<InvalidDataException>(() => UpdateTransaction.ValidateLocations(Install, Path.Combine(Install, "data"), Session));

    [Fact]
    public async Task QueueAndDirectOperationsFreezeOnlyWhenIdle()
    {
        var activity = new SyncActivityGate();
        using (activity.Enter()) Assert.Null(activity.TryFreeze());
        using (activity.TryFreeze()!) Assert.Throws<InvalidOperationException>(() => activity.Enter());
        using var nextActivity = activity.Enter();
        var queue = new SyncQueue();
        var job = new SyncJob { GameInstanceId = Guid.NewGuid(), Direction = SyncDirection.Upload };
        await queue.QueueAsync(job, CancellationToken.None);
        Assert.Null(queue.TryFreezeWhenIdle());
        Assert.True(queue.TryBeginJob(job));
        Assert.Null(queue.TryFreezeWhenIdle());
        queue.Complete();
        using (queue.TryFreezeWhenIdle()!)
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await queue.QueueAsync(new SyncJob { GameInstanceId = Guid.NewGuid(), Direction = SyncDirection.Upload }, CancellationToken.None));
        await queue.QueueAsync(new SyncJob { GameInstanceId = Guid.NewGuid(), Direction = SyncDirection.Upload }, CancellationToken.None);
    }

    private Task<UpdateJournal> PrepareAsync() => UpdateTransaction.PrepareAsync(Install, Data, Session, "1.2.3", PublicKey);

    private async Task CreatePackageAsync(string? extraName = null, bool link = false, bool duplicate = false, bool badDigest = false)
    {
        var files = new Dictionary<string, string> { ["GSYNC.App.exe"] = "new app", ["Updater/GSYNC.Updater.exe"] = "new helper", ["new.dll"] = "new library" };
        var managed = files.Keys.Append(UpdateTransaction.ManagedFilesName).ToArray();
        var archivePath = Path.Combine(Session, "package.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            foreach (var pair in files) Add(archive, pair.Key, pair.Value);
            Add(archive, UpdateTransaction.ManagedFilesName, JsonSerializer.Serialize(managed));
            if (extraName is not null)
            {
                var entry = Add(archive, extraName, "bad");
                if (link) entry.ExternalAttributes = 0xA000 << 16;
                _ = duplicate;
            }
        }
        var bytes = await File.ReadAllBytesAsync(archivePath);
        var digest = badDigest ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(bytes));
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new UpdateManifest(1, "1.2.3", "GSYNC-1.2.3-win-x64.zip", digest, bytes.Length));
        await File.WriteAllBytesAsync(Path.Combine(Session, "manifest.json"), manifest);
        await File.WriteAllBytesAsync(Path.Combine(Session, "manifest.sig"), _key.SignData(manifest, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    private static ZipArchiveEntry Add(ZipArchive archive, string path, string value)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(value);
        return entry;
    }

    private void WriteInstalled(string path, string value)
    {
        var target = Path.Combine(Install, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, value);
    }

    public void Dispose() { _key.Dispose(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
