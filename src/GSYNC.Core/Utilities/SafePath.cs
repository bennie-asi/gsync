namespace GSYNC.Core.Utilities;

public static class SafePath
{
    public static string GetPhysicalPath(string path) => GetPhysicalPath(path, 0);

    private static string GetPhysicalPath(string path, int linkDepth)
    {
        if (linkDepth > 40) throw new InvalidDataException("Too many symbolic links or junctions in the selected path.");
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? throw new InvalidDataException("An absolute path is required.");
        var current = root;
        foreach (var segment in full[root.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (segment.Length == 0) continue;
            current = Path.Combine(current, segment);
            try
            {
                var attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.ReparsePoint) == 0) continue;
                var target = PathInfo(current, attributes).ResolveLinkTarget(returnFinalTarget: false);
                // Cloud placeholders also have ReparsePoint, but do not redirect to another path.
                if (target is not null) current = GetPhysicalPath(target.FullName, linkDepth + 1);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        return Path.GetFullPath(current);
    }

    public static IEnumerable<string> EnumerateTree(string root, Func<string, bool>? skip = null)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                var attributes = File.GetAttributes(path);
                if (skip?.Invoke(relative) == true || ((attributes & FileAttributes.Directory) != 0 && skip?.Invoke(relative + '/') == true)) continue;
                if (IsLink(path, attributes))
                    throw new InvalidDataException($"Content contains a symbolic link or junction at '{path}'. Select its target explicitly or exclude it.");
                yield return path;
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(path);
            }
        }
    }

    public static string ValidateRelative(string path, bool allowEmpty = false)
    {
        var normalized = path.Replace('\\', '/');
        if ((!allowEmpty && string.IsNullOrWhiteSpace(normalized)) || Path.IsPathRooted(normalized) ||
            normalized.Split('/').Any(segment => segment is "." or ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                (OperatingSystem.IsWindows() && (segment.EndsWith('.') || segment.EndsWith(' ') || IsDeviceName(segment)))))
        {
            throw new InvalidDataException($"Remote path '{path}' is not a safe relative file path.");
        }
        return normalized;
    }

    private static bool IsDeviceName(string name)
    {
        var stem = name.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" ||
               (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                "123456789¹²³".Contains(stem[3]));
    }

    public static string ResolveUnderRoot(string root, string relative, bool allowRoot = false)
    {
        var normalized = ValidateRelative(relative, allowRoot);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var target = Path.GetFullPath(Path.Combine(fullRoot + Path.DirectorySeparatorChar, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if ((!allowRoot || !string.Equals(target.TrimEnd(Path.DirectorySeparatorChar), fullRoot, StringComparison.OrdinalIgnoreCase)) &&
            !target.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Path '{relative}' escapes the content directory.");
        }

        var current = fullRoot;
        foreach (var segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            try
            {
                if (IsLink(current, File.GetAttributes(current)))
                    throw new InvalidDataException($"Path '{relative}' passes through a symbolic link or junction.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        return target;
    }

    private static FileSystemInfo PathInfo(string path, FileAttributes attributes) =>
        (attributes & FileAttributes.Directory) != 0 ? new DirectoryInfo(path) : new FileInfo(path);

    private static bool IsLink(string path, FileAttributes attributes) =>
        (attributes & FileAttributes.ReparsePoint) != 0 && PathInfo(path, attributes).LinkTarget is not null;
}
