using System.Security.Cryptography;
using System.Text.Json;

namespace GSYNC.Core.Utilities;

/// <summary>Durable undo information for a multi-file save replacement, recovered before the next application startup.</summary>
public static class RecoverableFileTransaction
{
    private static readonly object Gate = new();
    private const int MaximumJournalBytes = 64 * 1024 * 1024;

    public static void Apply(IReadOnlyList<AtomicFile.PreparedFile> files, string journalDirectory)
    {
        if (files.Count == 0) return;
        lock (Gate)
        {
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entries = files.Select(file =>
            {
                var target = SafePath.GetPhysicalPath(file.TargetPath);
                if (!targets.Add(target)) throw new InvalidDataException("A save transaction contains duplicate targets.");
                var originalHash = Hash(target);
                if (file.VerifyOriginal && !string.Equals(originalHash, file.ExpectedOriginalHash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException($"Save '{file.TargetPath}' changed while synchronization was preparing. Compare the latest files and retry.");
                var temporary = file.TemporaryPath is null ? null : SafePath.GetPhysicalPath(file.TemporaryPath);
                return new Entry(target, temporary, originalHash is null ? null : Path.Combine(Path.GetDirectoryName(target)!, AtomicFile.TemporaryName()), originalHash, temporary is null ? null : Hash(temporary));
            }).ToArray();
            var journal = new Journal(1, false, entries);
            Validate(journal);
            Directory.CreateDirectory(journalDirectory);
            var path = Path.Combine(journalDirectory, Guid.NewGuid().ToString("N") + ".json");
            Write(path, journal);
            try
            {
                foreach (var entry in entries)
                {
                    if (SafePath.GetPhysicalPath(entry.Target) != entry.Target || Hash(entry.Target) != entry.OriginalHash)
                        throw new IOException($"Save '{entry.Target}' changed before replacement.");
                    if (entry.Temporary is null)
                    {
                        if (entry.Backup is not null) File.Move(entry.Target, entry.Backup);
                    }
                    else if (entry.Backup is not null) File.Replace(entry.Temporary, entry.Target, entry.Backup);
                    else File.Move(entry.Temporary, entry.Target);
                }
                Write(path, journal with { Committed = true });
            }
            catch (Exception original)
            {
                try { Recover(path, journal); }
                catch (Exception recovery) { throw new AggregateException("Save replacement failed; its recovery journal was preserved.", original, recovery); }
                throw;
            }
            // A cleanup failure is harmless after the durable commit marker. Startup retries it.
            try { Recover(path, journal with { Committed = true }); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static void RecoverAll(string journalDirectory)
    {
        lock (Gate)
        {
            if (!Directory.Exists(journalDirectory)) return;
            foreach (var path in Directory.EnumerateFiles(journalDirectory, "*.json"))
            {
                if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out _) || new FileInfo(path).Length > MaximumJournalBytes)
                    throw new InvalidDataException("An invalid recovery journal was preserved for inspection.");
                Journal journal;
                try { journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty recovery journal."); }
                catch (JsonException exception) { throw new InvalidDataException($"Cannot read recovery journal '{path}'.", exception); }
                Validate(journal);
                Recover(path, journal);
            }
        }
    }

    private static void Recover(string path, Journal journal)
    {
        Validate(journal);
        if (!journal.Committed)
        {
            // Check every target before rolling back any of them. Newer game progress must be preserved.
            foreach (var entry in journal.Entries)
            {
                if (SafePath.GetPhysicalPath(entry.Target) != entry.Target) throw new IOException("A recovery target now redirects to another directory.");
                var current = Hash(entry.Target);
                if (current != entry.OriginalHash && current != entry.NewHash)
                    throw new IOException($"Save '{entry.Target}' changed after an interrupted operation. Its backup and recovery journal were preserved.");
                if (entry.Backup is not null && !File.Exists(entry.Backup) && current != entry.OriginalHash)
                    throw new IOException($"The recovery copy for '{entry.Target}' is missing. Its journal was preserved.");
                if (entry.Backup is not null && File.Exists(entry.Backup) && Hash(entry.Backup) != entry.OriginalHash)
                    throw new IOException($"The recovery copy for '{entry.Target}' failed its integrity check. Current files and the journal were preserved.");
            }
            foreach (var entry in journal.Entries.Reverse())
            {
                if (entry.Backup is not null && File.Exists(entry.Backup)) File.Move(entry.Backup, entry.Target, overwrite: true);
                else if (entry.OriginalHash is null && File.Exists(entry.Target)) File.Delete(entry.Target);
            }
        }
        foreach (var entry in journal.Entries)
        {
            if (entry.Temporary is not null) File.Delete(entry.Temporary);
            if (entry.Backup is not null) File.Delete(entry.Backup);
        }
        File.Delete(path);
    }

    private static void Validate(Journal journal)
    {
        if (journal.Version != 1 || journal.Entries is null || journal.Entries.Length > 100_000)
            throw new InvalidDataException("Unsupported file recovery journal.");
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scratch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in journal.Entries)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.Target) || !Path.IsPathFullyQualified(entry.Target) || !targets.Add(entry.Target) ||
                (entry.OriginalHash is null) != (entry.Backup is null) || (entry.Temporary is null) != (entry.NewHash is null))
                throw new InvalidDataException("Invalid recovery entry.");
            foreach (var temporary in new[] { entry.Temporary, entry.Backup }.OfType<string>())
                if (!Path.IsPathFullyQualified(temporary) || !AtomicFile.IsTemporary(temporary) ||
                    !scratch.Add(temporary) ||
                    !string.Equals(SafePath.GetPhysicalPath(temporary), Path.GetFullPath(temporary), StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Path.GetDirectoryName(temporary), Path.GetDirectoryName(entry.Target), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("A recovery file is outside its target directory.");
            foreach (var hash in new[] { entry.OriginalHash, entry.NewHash }.OfType<string>())
                if (hash.Length != 64 || !hash.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid recovery hash.");
        }
        if (targets.Overlaps(scratch)) throw new InvalidDataException("A recovery target overlaps a temporary file.");
    }

    public static string? Hash(string path)
    {
        if (Directory.Exists(path)) throw new IOException($"Save target '{path}' is a directory.");
        try { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static void Write(string path, Journal journal)
    {
        var json = JsonSerializer.Serialize(journal);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumJournalBytes) throw new InvalidDataException("The recovery journal is too large.");
        AtomicFile.WriteAllText(path, json);
    }

    private sealed record Journal(int Version, bool Committed, Entry[] Entries);
    private sealed record Entry(string Target, string? Temporary, string? Backup, string? OriginalHash, string? NewHash);
}
