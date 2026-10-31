using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using GSYNC.Core.Utilities;

namespace GSYNC.Core.Updates;

public sealed record UpdateFileEntry(string Path, string? OriginalHash, string NewHash);
public sealed record UpdateJournal(int FormatVersion, string InstallRoot, string DataRoot, string Version, bool Committed, UpdateFileEntry[] Files, bool RolledBack = false);

/// <summary>Stages a signed package, backs up managed files, and retains a durable undo journal.</summary>
public static class UpdateTransaction
{
    public const string ManagedFilesName = "GSYNC.managed.json";
    public const string JournalName = "journal.json";
    private const int MaxFiles = 50_000;
    private const long MaxExtractedBytes = 2L * 1024 * 1024 * 1024;
    private const long MaxSingleFileBytes = 512L * 1024 * 1024;

    public static void ValidateLocations(string installRoot, string dataRoot, string session)
    {
        installRoot = FullDirectory(installRoot);
        dataRoot = FullDirectory(dataRoot);
        session = FullDirectory(session);
        if (Within(installRoot, dataRoot) || Within(dataRoot, installRoot))
            throw new InvalidDataException("The program and data directories overlap; automatic update is not safe.");
        if (!Within(session, Path.Combine(dataRoot, "updates")) ||
            !Guid.TryParseExact(Path.GetFileName(session), "N", out _))
            throw new InvalidDataException("The update session is outside the application update directory.");
        if (string.Equals(installRoot, Path.GetPathRoot(installRoot), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A drive root cannot be updated automatically.");
        RejectReparsePoints(installRoot);
        RejectReparsePoints(dataRoot);
        RejectReparsePoints(session);
    }

    public static void ValidateInstalledHelper(string installRoot)
    {
        var owned = ReadManagedFiles(Path.Combine(installRoot, ManagedFilesName));
        var helper = Path.Combine(installRoot, "Updater", "GSYNC.Updater.exe");
        RejectReparsePoints(helper);
        if (!owned.Contains("GSYNC.App.exe") || !owned.Contains("Updater/GSYNC.Updater.exe") || !File.Exists(helper))
            throw new InvalidDataException("The installed copy has no managed update helper. Install a signed release manually first.");
    }

    public static async Task<UpdateJournal> PrepareAsync(string installRoot, string dataRoot, string session, string expectedVersion,
        string? publicKey = null, CancellationToken token = default)
    {
        ValidateLocations(installRoot, dataRoot, session);
        installRoot = FullDirectory(installRoot);
        dataRoot = FullDirectory(dataRoot);
        session = FullDirectory(session);
        var manifestBytes = await ReadBoundedAsync(Path.Combine(session, "manifest.json"), UpdateManifestVerifier.MaximumManifestBytes, token);
        var signature = await ReadBoundedAsync(Path.Combine(session, "manifest.sig"), 64, token);
        var manifest = UpdateManifestVerifier.Verify(manifestBytes, signature, expectedVersion, publicKey);
        var archivePath = Path.Combine(session, "package.zip");
        RejectReparsePoints(archivePath);
        await UpdateManifestVerifier.VerifyArchiveAsync(archivePath, manifest, token);
        var owned = ReadManagedFiles(Path.Combine(installRoot, ManagedFilesName));
        var stage = Path.Combine(session, "stage");
        var backup = Path.Combine(session, "backup");
        if (Directory.Exists(stage) || Directory.Exists(backup) || File.Exists(Path.Combine(session, JournalName)))
            throw new IOException("This update session has already been prepared.");
        Directory.CreateDirectory(stage);
        Directory.CreateDirectory(backup);
        var staged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        long extracted = 0;
        using (var archive = ZipFile.OpenRead(archivePath))
        {
            if (archive.Entries.Count > MaxFiles) throw new InvalidDataException("The update archive contains too many entries.");
            foreach (var entry in archive.Entries)
            {
                token.ThrowIfCancellationRequested();
                var relative = ValidateRelative(entry.FullName.TrimEnd('/', '\\'));
                var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
                if ((unixType != 0 && unixType != 0x8000 && unixType != 0x4000) || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("The update archive contains a link or special file.");
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) continue;
                if (entry.Length < 0 || entry.Length > MaxSingleFileBytes || extracted + entry.Length > MaxExtractedBytes || !staged.TryAdd(relative, string.Empty))
                    throw new InvalidDataException("The update archive contains an oversized or duplicate file.");
                var target = SafePath.ResolveUnderRoot(stage, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var input = entry.Open();
                await using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920]; long written = 0; int read;
                    while ((read = await input.ReadAsync(buffer, token)) != 0)
                    {
                        written += read;
                        if (written > entry.Length || extracted + written > MaxExtractedBytes) throw new InvalidDataException("The extracted update is too large.");
                        await output.WriteAsync(buffer.AsMemory(0, read), token);
                    }
                    if (written != entry.Length) throw new InvalidDataException("An update file was truncated.");
                    await output.FlushAsync(token);
                    output.Flush(true);
                }
                extracted += entry.Length;
                staged[relative] = Hash(target)!;
            }
        }
        var newOwned = ReadManagedFiles(Path.Combine(stage, ManagedFilesName));
        if (!newOwned.SetEquals(staged.Keys) || !newOwned.Contains("GSYNC.App.exe") || !newOwned.Contains("Updater/GSYNC.Updater.exe"))
            throw new InvalidDataException("The signed package does not match its managed file list.");

        var entries = new List<UpdateFileEntry>(staged.Count);
        foreach (var pair in staged.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            var target = SafePath.ResolveUnderRoot(installRoot, pair.Key);
            RejectReparsePoints(target);
            if (Directory.Exists(target)) throw new IOException($"An update target is a directory: {target}");
            var original = Hash(target);
            if (original is not null)
            {
                if (!owned.Contains(pair.Key)) throw new IOException($"The update would overwrite a file not managed by GSYNC: {target}");
                var copy = SafePath.ResolveUnderRoot(backup, pair.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                await CopyNewAsync(target, copy, token);
                if (!string.Equals(Hash(copy), original, StringComparison.Ordinal)) throw new IOException("An update backup did not pass its integrity check.");
            }
            entries.Add(new UpdateFileEntry(pair.Key, original, pair.Value));
        }
        var journal = new UpdateJournal(1, installRoot, dataRoot, manifest.Version, false, entries.ToArray());
        WriteJournal(session, journal);
        return journal;
    }

    public static void Apply(string session, UpdateJournal journal, Action<int>? afterFile = null)
    {
        ValidateJournal(session, journal);
        if (journal.Committed || journal.RolledBack) throw new InvalidOperationException("This update is already finished.");
        var count = 0;
        foreach (var entry in journal.Files)
        {
            var target = SafePath.ResolveUnderRoot(journal.InstallRoot, entry.Path);
            var staged = SafePath.ResolveUnderRoot(Path.Combine(session, "stage"), entry.Path);
            RejectReparsePoints(target);
            RejectReparsePoints(staged);
            if (!string.Equals(Hash(target), entry.OriginalHash, StringComparison.Ordinal) || !string.Equals(Hash(staged), entry.NewHash, StringComparison.Ordinal))
                throw new IOException($"An update file changed during preparation: {entry.Path}");
            ReplaceFrom(staged, target);
            afterFile?.Invoke(++count);
        }
    }

    public static void Commit(string session, UpdateJournal journal)
    {
        ValidateJournal(session, journal);
        if (journal.Committed || journal.RolledBack) throw new InvalidOperationException("This update is already finished.");
        foreach (var entry in journal.Files)
        {
            var target = SafePath.ResolveUnderRoot(journal.InstallRoot, entry.Path);
            RejectReparsePoints(target);
            if (!string.Equals(Hash(target), entry.NewHash, StringComparison.Ordinal)) throw new IOException("An installed file changed before update confirmation.");
        }
        WriteJournal(session, journal with { Committed = true });
    }

    public static void Rollback(string session, UpdateJournal journal)
    {
        ValidateJournal(session, journal);
        if (journal.Committed) throw new InvalidOperationException("A committed update cannot be rolled back automatically.");
        if (journal.RolledBack) return;
        // Validate everything before replacing anything; preserve user changes made after an interruption.
        foreach (var entry in journal.Files)
        {
            var target = SafePath.ResolveUnderRoot(journal.InstallRoot, entry.Path);
            RejectReparsePoints(target);
            var current = Hash(target);
            if (current != entry.OriginalHash && current != entry.NewHash)
                throw new IOException($"A program file changed after the interrupted update: {target}. Backups were preserved.");
            if (entry.OriginalHash is not null)
            {
                var backup = SafePath.ResolveUnderRoot(Path.Combine(session, "backup"), entry.Path);
                RejectReparsePoints(backup);
                if (Hash(backup) != entry.OriginalHash) throw new IOException($"An update backup is missing or damaged: {backup}");
            }
        }
        foreach (var entry in journal.Files.Reverse())
        {
            var target = SafePath.ResolveUnderRoot(journal.InstallRoot, entry.Path);
            if (Hash(target) == entry.OriginalHash) continue;
            if (entry.OriginalHash is null) File.Delete(target);
            else ReplaceFrom(SafePath.ResolveUnderRoot(Path.Combine(session, "backup"), entry.Path), target);
        }
        WriteJournal(session, journal with { RolledBack = true });
    }

    public static UpdateJournal ReadJournal(string session)
    {
        var path = Path.Combine(session, JournalName);
        RejectReparsePoints(path);
        if (!File.Exists(path) || new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("The update recovery journal is missing or too large.");
        var journal = JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllBytes(path)) ?? throw new InvalidDataException("The update recovery journal is empty.");
        ValidateJournal(session, journal);
        return journal;
    }

    /// <summary>Called while holding the application lock, before normal startup opens user data.</summary>
    public static string? FindPendingRecovery(string installRoot, string dataRoot)
    {
        installRoot = FullDirectory(installRoot);
        dataRoot = FullDirectory(dataRoot);
        var updates = Path.Combine(dataRoot, "updates");
        if (!Directory.Exists(updates)) return null;
        RejectReparsePoints(updates);
        foreach (var session in Directory.EnumerateDirectories(updates))
        {
            if (!Guid.TryParseExact(Path.GetFileName(session), "N", out _) || !File.Exists(Path.Combine(session, JournalName))) continue;
            var journal = ReadJournal(session);
            if (string.Equals(FullDirectory(journal.DataRoot), dataRoot, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(FullDirectory(journal.InstallRoot), installRoot, StringComparison.OrdinalIgnoreCase) &&
                !journal.Committed && !journal.RolledBack)
                return session;
        }
        return null;
    }

    private static void ValidateJournal(string session, UpdateJournal journal)
    {
        if (journal.FormatVersion != 1 || (journal.Committed && journal.RolledBack) || journal.Files is null || journal.Files.Length is 0 or > MaxFiles) throw new InvalidDataException("Invalid update recovery journal.");
        ValidateLocations(journal.InstallRoot, journal.DataRoot, session);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in journal.Files)
        {
            if (entry is null || !seen.Add(ValidateRelative(entry.Path)) || !ValidHash(entry.NewHash) ||
                (entry.OriginalHash is not null && !ValidHash(entry.OriginalHash))) throw new InvalidDataException("Invalid update recovery entry.");
        }
    }

    private static HashSet<string> ReadManagedFiles(string path)
    {
        RejectReparsePoints(path);
        if (!File.Exists(path) || new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("The installation has no valid managed file list. Install this version manually first.");
        string[] files;
        try { files = JsonSerializer.Deserialize<string[]>(File.ReadAllBytes(path)) ?? throw new InvalidDataException("The managed file list is empty."); }
        catch (JsonException ex) { throw new InvalidDataException("The managed file list is invalid.", ex); }
        if (files.Length is 0 or > MaxFiles) throw new InvalidDataException("The managed file list has an invalid size.");
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files) if (!result.Add(ValidateRelative(file))) throw new InvalidDataException("The managed file list contains duplicates.");
        return result;
    }

    private static string ValidateRelative(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("An update path is empty.");
        var normalized = path.Replace('\\', '/');
        if (normalized.Contains(':') || normalized.Split('/').Any(segment => segment.Length == 0)) throw new InvalidDataException("Invalid update path.");
        return SafePath.ValidateRelative(normalized);
    }

    private static string FullDirectory(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    private static bool Within(string path, string root) => string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static bool ValidHash(string hash) => hash is { Length: 64 } && hash.All(Uri.IsHexDigit);
    private static string? Hash(string path) => RecoverableFileTransaction.Hash(path);

    private static void RejectReparsePoints(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? throw new InvalidDataException("An absolute path is required.");
        var current = root;
        foreach (var segment in full[root.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (segment.Length == 0) continue;
            current = Path.Combine(current, segment);
            try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException($"Update path contains a link: {current}"); }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(string path, int maximum, CancellationToken token)
    {
        RejectReparsePoints(path);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        if (stream.Length > maximum) throw new InvalidDataException("Update metadata is too large.");
        var bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, token);
        return bytes;
    }

    private static async Task CopyNewAsync(string source, string target, CancellationToken token)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await input.CopyToAsync(output, token);
        await output.FlushAsync(token);
        output.Flush(true);
    }

    private static void ReplaceFrom(string source, string target)
    {
        var directory = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(directory);
        RejectReparsePoints(directory);
        var temporary = Path.Combine(directory, AtomicFile.TemporaryName());
        try
        {
            using (var input = File.OpenRead(source))
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { input.CopyTo(output); output.Flush(true); }
            if (File.Exists(target)) File.Replace(temporary, target, null);
            else File.Move(temporary, target);
        }
        finally { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    private static void WriteJournal(string session, UpdateJournal journal)
        => AtomicFile.WriteAllBytes(Path.Combine(session, JournalName), JsonSerializer.SerializeToUtf8Bytes(journal));
}
