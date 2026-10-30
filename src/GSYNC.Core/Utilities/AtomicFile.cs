using System.Text;

namespace GSYNC.Core.Utilities;

public static class AtomicFile
{
    public static string TemporaryName() => $".gsync-write-{Guid.NewGuid():N}.tmp";

    public static bool IsTemporary(string path)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith(".gsync-write-", StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.Ordinal) &&
               Guid.TryParseExact(name[13..^4], "N", out _);
    }

    public static async Task<PreparedFile> PrepareAsync(Stream content, string targetPath, CancellationToken cancellationToken)
    {
        var target = Path.GetFullPath(targetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var prepared = new PreparedFile(target, Path.Combine(Path.GetDirectoryName(target)!, TemporaryName()));
        try
        {
            await using var stream = new FileStream(prepared.TemporaryPath!, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await content.CopyToAsync(stream, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true);
            cancellationToken.ThrowIfCancellationRequested();
            return prepared;
        }
        catch
        {
            prepared.Dispose();
            throw;
        }
    }

    public static async Task CopyAsync(Stream content, string target, CancellationToken cancellationToken)
    {
        using var prepared = await PrepareAsync(content, target, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        Apply([prepared]);
    }

    public static void WriteAllText(string targetPath, string contents)
        => WriteAllBytes(targetPath, Encoding.UTF8.GetBytes(contents));

    public static void WriteAllBytes(string targetPath, ReadOnlySpan<byte> contents)
    {
        var target = Path.GetFullPath(targetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using var prepared = new PreparedFile(target, Path.Combine(Path.GetDirectoryName(target)!, TemporaryName()));
        using (var stream = new FileStream(prepared.TemporaryPath!, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.Write(contents);
            stream.Flush(flushToDisk: true);
        }
        Apply([prepared]);
    }

    // Commit is a short, non-cancellable phase. A failed replacement rolls back earlier files.
    public static void Apply(IReadOnlyList<PreparedFile> files, string? journalDirectory = null)
    {
        if (journalDirectory is not null)
        {
            RecoverableFileTransaction.Apply(files, journalDirectory);
            return;
        }
        if (files.Select(file => Path.GetFullPath(file.TargetPath)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Count)
            throw new InvalidDataException("A file transaction contains duplicate targets.");
        var applied = new List<(PreparedFile file, string? backup, bool existed)>();
        try
        {
            foreach (var file in files)
            {
                if (Directory.Exists(file.TargetPath)) throw new IOException($"Target '{file.TargetPath}' is a directory.");
                var existed = File.Exists(file.TargetPath);
                var backup = existed ? Path.Combine(Path.GetDirectoryName(file.TargetPath)!, TemporaryName()) : null;
                applied.Add((file, backup, existed));
                if (file.TemporaryPath is null)
                {
                    if (existed) File.Move(file.TargetPath, backup!);
                }
                else if (existed) File.Replace(file.TemporaryPath, file.TargetPath, backup);
                else File.Move(file.TemporaryPath, file.TargetPath);
            }
        }
        catch (Exception original)
        {
            var errors = new List<Exception> { original };
            foreach (var change in applied.AsEnumerable().Reverse())
            {
                try
                {
                    if (change.backup is not null && File.Exists(change.backup)) File.Move(change.backup, change.file.TargetPath, overwrite: true);
                    else if (!change.existed && change.file.TemporaryPath is not null && !File.Exists(change.file.TemporaryPath)) File.Delete(change.file.TargetPath);
                }
                catch (Exception rollbackError)
                {
                    errors.Add(new IOException($"Rollback failed; recovery copy is at '{change.backup}'.", rollbackError));
                }
            }
            if (errors.Count > 1) throw new AggregateException("File replacement failed and needs recovery.", errors);
            throw;
        }
        foreach (var change in applied) if (change.backup is not null) TryDelete(change.backup);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public sealed class PreparedFile(string targetPath, string? temporaryPath) : IDisposable
    {
        public string TargetPath { get; } = targetPath;
        public string? TemporaryPath { get; } = temporaryPath;
        public bool VerifyOriginal { get; set; }
        public string? ExpectedOriginalHash { get; set; }
        public void Dispose() { if (TemporaryPath is not null) TryDelete(TemporaryPath); }
    }
}
