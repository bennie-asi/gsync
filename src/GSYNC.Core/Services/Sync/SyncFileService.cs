using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GSYNC.Core.Abstractions;
using GSYNC.Core.Models;
using GSYNC.Core.Models.Manifest;
using GSYNC.Core.Utilities;

namespace GSYNC.Core.Services.Sync;

internal sealed class SyncFileService(PathResolver pathResolver, SystemVariableProvider systemVariables, Func<IReadOnlyDictionary<string, string>>? userVariables)
{
    private readonly PathResolver _pathResolver = pathResolver;
    private readonly SystemVariableProvider _systemVariableProvider = systemVariables;
    private readonly Func<IReadOnlyDictionary<string, string>>? _userVariablesProvider = userVariables;
    internal static string RootMetadataPath(StorageBinding binding, ResolvedContentRoot root) =>
        BuildRemotePath(binding.RemoteNamespace, "__gsync_meta", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.StorageKey.ToUpperInvariant())))[..16] + ".json");

    internal IReadOnlyList<ResolvedContentRoot> ResolveContentRoots(
        GameContentDefinition definition,
        GameInstance instance,
        ISourceProvider sourceProvider,
        StorageBinding binding)
    {
        var systemVariables = _systemVariableProvider.GetVariables();
        var sourceVariables = sourceProvider.ResolveVariables(instance);
        var userVariables = _userVariablesProvider?.Invoke() ?? new Dictionary<string, string>();
        var instanceVariables = instance.Variables;
        var userOverrides = binding.Settings;
        var enabledContent = binding.ContentSelections.Count == 0
            ? definition.ContentItems.Where(item => item.DefaultEnabled)
            : definition.ContentItems.Where(item => binding.ContentSelections.Any(selection =>
                string.Equals(selection.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase) && selection.IsEnabled));

        var selectedItems = enabledContent.ToArray();
        if (selectedItems.Select(item => item.ContentId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != selectedItems.Length)
            throw new InvalidDataException("Content IDs must be unique.");
        foreach (var item in selectedItems)
        {
            SafePath.ValidateRelative(item.ContentId);
            if (item.ContentId.Contains('/') || item.ContentId.Contains('\\') || string.Equals(item.ContentId, "__gsync_meta", StringComparison.OrdinalIgnoreCase) || string.Equals(item.ContentId, "__gsync_versions", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Content IDs must be single, non-reserved path segments.");
        }

        return selectedItems
            .SelectMany(item =>
            {
                var templates = item.PathTemplates.Select(path => path.Replace('\\', '/')).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var selection = binding.ContentSelections.FirstOrDefault(value =>
                    string.Equals(value.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase));
                return templates.Where(template => selection?.IsTemplateEnabled(template) != false).Select(template =>
                {
                    var effectiveTemplate = selection?.EffectiveTemplate(template) ?? template;
                    var resolved = _pathResolver.Resolve(effectiveTemplate, systemVariables, sourceVariables, instanceVariables, userVariables, userOverrides);
                    if (string.IsNullOrWhiteSpace(resolved) || resolved.IndexOfAny(['<', '>']) >= 0 || Regex.IsMatch(resolved, @"%[A-Z0-9_]+%", RegexOptions.IgnoreCase) || !Path.IsPathFullyQualified(resolved))
                    {
                        throw new InvalidOperationException($"Content path '{template}' did not resolve to an absolute path. Check its variables.");
                    }

                    var (rootPath, pattern) = File.Exists(resolved) || Directory.Exists(resolved) ? (resolved, (string?)null) : PathGlob.SplitRoot(resolved);
                    return new ResolvedContentRoot(item, rootPath,
                        pattern is not null || (Directory.Exists(resolved) ? true : File.Exists(resolved) ? false : ShouldTreatAsDirectory(item, effectiveTemplate)),
                        templates.Length == 1 ? item.ContentId : BuildRemotePath(item.ContentId, "__roots", GetRootId(template)))
                    {
                        GlobPattern = pattern,
                    };
                });
            })
            .Where(root => !string.IsNullOrWhiteSpace(root.LocalRootPath))
            .DistinctBy(root => (root.ContentItem.ContentId, root.LocalRootPath), EqualityComparer<(string, string)>.Default)
            .ToArray();
    }

    internal static string GetRootId(string template)
    {
        // Hash the portable template, not the machine-specific resolved path or the list order.
        var normalized = template.Replace('\\', '/').TrimEnd('/').ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16];
    }

    internal static bool ShouldTreatAsDirectory(ContentItem item, string template)
    {
        if (item.IncludePatterns.Count > 0 || item.ExcludePatterns.Count > 0)
        {
            return true;
        }

        return template.EndsWith('/') || template.EndsWith('\\') || !Path.HasExtension(template);
    }

    internal static IEnumerable<ResolvedLocalFile> EnumerateLocalFiles(ResolvedContentRoot root)
    {
        if (File.Exists(root.LocalRootPath) && !root.TreatAsDirectory)
        {
            if (!MatchesRoot(root, Path.GetFileName(root.LocalRootPath))) yield break;
            yield return new ResolvedLocalFile(
                root.ContentItem,
                root.LocalRootPath,
                Path.GetFileName(root.LocalRootPath),
                File.GetLastWriteTimeUtc(root.LocalRootPath)) { StorageKey = root.StorageKey };
            yield break;
        }

        if (!Directory.Exists(root.LocalRootPath))
        {
            yield break;
        }

        foreach (var file in SafePath.EnumerateTree(root.LocalRootPath,
                     relative => root.ContentItem.ExcludePatterns.Any(pattern => MatchPattern(pattern, relative))))
        {
            if (Directory.Exists(file) || AtomicFile.IsTemporary(file)) continue;
            var relativePath = Path.GetRelativePath(root.LocalRootPath, file).Replace('\\', '/');
            if (!MatchesRoot(root, relativePath))
            {
                continue;
            }

            yield return new ResolvedLocalFile(
                root.ContentItem,
                file,
                relativePath,
                File.GetLastWriteTimeUtc(file)) { StorageKey = root.StorageKey };
        }
    }

    internal static bool MatchesPatterns(ContentItem item, string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        var includes = item.IncludePatterns.Count == 0 || item.IncludePatterns.Any(pattern => MatchPattern(pattern, normalized));
        var excludes = item.ExcludePatterns.Any(pattern => MatchPattern(pattern, normalized));
        return includes && !excludes;
    }

    internal static bool MatchPattern(string pattern, string relativePath)
    {
        return PathGlob.IsMatch(pattern, relativePath, matchFileName: true, includeDescendants: true);
    }

    internal static bool MatchesRoot(ResolvedContentRoot root, string relativePath)
    {
        // A single-file root can be moved or renamed locally; retain visibility of the old remote filename.
        return (root.TreatAsDirectory || !relativePath.Contains('/')) &&
               (root.GlobPattern is null || PathGlob.IsMatch(root.GlobPattern, relativePath, includeDescendants: true)) &&
               MatchesPatterns(root.ContentItem, relativePath);
    }

    internal static async Task<IReadOnlyDictionary<string, RemoteFileEntry>> ListRemoteFilesAsync(
        SyncExecutionContext context,
        ResolvedContentRoot root,
        CancellationToken cancellationToken,
        bool checkLegacyLayout = true)
    {
        var namespacePath = BuildRemotePath(context.Binding.RemoteNamespace, root.StorageKey);
        var entries = await context.StorageProvider.ListAsync(namespacePath, cancellationToken);
        if (!root.KindKnownLocally && !root.KindDeclared && root.GlobPattern is null)
        {
            var paths = entries.Where(entry => !entry.IsDirectory).Select(entry => CreateRemoteFileEntry(namespacePath, entry)).OfType<RemoteFileEntry>().ToArray();
            if (paths.Length > 0)
                root.UseRemoteKind(paths.Length != 1 || !string.Equals(paths[0].RelativePath, Path.GetFileName(root.LocalRootPath), StringComparison.OrdinalIgnoreCase) ||
                                   root.LocalRootPath.EndsWith('/') || root.LocalRootPath.EndsWith('\\'));
        }
        if (checkLegacyLayout && entries.All(entry => entry.IsDirectory) && root.StorageKey != root.ContentItem.ContentId)
        {
            var legacyNamespace = BuildRemotePath(context.Binding.RemoteNamespace, root.ContentItem.ContentId);
            var legacyEntries = await context.StorageProvider.ListAsync(legacyNamespace, cancellationToken);
            var files = legacyEntries.Where(entry => !entry.IsDirectory)
                .Select(entry => CreateRemoteFileEntry(legacyNamespace, entry)).OfType<RemoteFileEntry>().ToArray();
            if (files.Length > 0 && !files.Any(entry => entry.RelativePath.StartsWith("__roots/", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Remote content '{root.ContentItem.ContentId}' uses the old mixed-path layout. Back it up and upload the local saves again before downloading.");
            }
        }

        return entries
            .Where(entry => !entry.IsDirectory)
            .Select(entry => CreateRemoteFileEntry(namespacePath, entry))
            .Where(entry => entry is not null && MatchesRoot(root, entry.RelativePath))
            .ToDictionary(entry => entry!.RelativePath, entry => entry!, StringComparer.OrdinalIgnoreCase);
    }

    internal static RemoteFileEntry? CreateRemoteFileEntry(string namespacePath, RemoteEntry entry)
    {
        var normalizedNamespace = namespacePath.Trim('/');
        var normalizedPath = entry.Path.Trim('/');
        string relativePath;

        if (string.Equals(normalizedPath, normalizedNamespace, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (normalizedPath.StartsWith(normalizedNamespace + '/', StringComparison.OrdinalIgnoreCase))
        {
            relativePath = normalizedPath[(normalizedNamespace.Length + 1)..];
        }
        else
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        return new RemoteFileEntry(relativePath, normalizedPath, entry.Hash, entry.LastModifiedUtc, entry.Size);
    }

    internal static string ResolveTargetLocalPath(ResolvedContentRoot root, string relativePath)
    {
        SafePath.ValidateRelative(relativePath);
        if (!root.TreatAsDirectory)
        {
            return root.LocalRootPath;
        }

        return SafePath.ResolveUnderRoot(root.LocalRootPath, relativePath);
    }

    internal static SyncChangeKind DetermineChangeKind(ResolvedLocalFile? localFile, RemoteFileEntry? remoteFile, string? localHash, long? localSize)
    {
        if (localFile is not null && remoteFile is not null)
        {
            // Preferred signal: a content hash supplied by the storage provider.
            if (!string.IsNullOrWhiteSpace(remoteFile.Hash))
            {
                return string.Equals(localHash, remoteFile.Hash, StringComparison.OrdinalIgnoreCase)
                    ? SyncChangeKind.Unchanged
                    : SyncChangeKind.Conflict;
            }

            // Without a verified content hash, equal lengths do not establish equality.
            return SyncChangeKind.Conflict;
        }

        if (localFile is not null)
        {
            return SyncChangeKind.AddedLocally;
        }

        if (remoteFile is not null)
        {
            return SyncChangeKind.AddedRemotely;
        }

        return SyncChangeKind.Unchanged;
    }

    internal static async Task<string> ComputeFileHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    internal static string BuildRemotePath(params string[] segments)
    {
        return string.Join('/', segments.Where(segment => !string.IsNullOrWhiteSpace(segment)).Select(segment => segment.Trim('/')));
    }

    internal static bool TryResolvePlanPath(
        SyncExecutionContext context,
        IReadOnlyDictionary<ResolvedContentRoot, IReadOnlyDictionary<string, RemoteFileEntry>> remoteFileSets,
        string planPath,
        out ResolvedContentRoot root,
        out string relativePath,
        out RemoteFileEntry? remoteFile)
    {
        var normalizedPlanPath = NormalizePlanPath(context.Binding.RemoteNamespace.Trim('/'), planPath);
        foreach (var contentRoot in context.ContentRoots)
        {
            var prefix = contentRoot.StorageKey + '/';
            if (!normalizedPlanPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            relativePath = normalizedPlanPath[prefix.Length..];
            root = contentRoot;
            remoteFile = null;
            remoteFileSets.TryGetValue(contentRoot, out var remoteFiles);
            remoteFiles?.TryGetValue(relativePath, out remoteFile);
            return !string.IsNullOrWhiteSpace(relativePath);
        }

        root = default!;
        relativePath = string.Empty;
        remoteFile = null;
        return false;
    }

    internal static string NormalizePlanPath(string namespacePrefix, string planPath)
    {
        var normalized = planPath.Trim('/');
        if (!string.IsNullOrWhiteSpace(namespacePrefix) &&
            normalized.StartsWith(namespacePrefix + '/', StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[(namespacePrefix.Length + 1)..];
        }

        return normalized;
    }

    internal static void ValidateDownloadTargets(IReadOnlyDictionary<ResolvedContentRoot, IReadOnlyDictionary<string, RemoteFileEntry>> sets)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in sets)
        {
            foreach (var remote in pair.Value.Values)
            {
                var target = ResolveTargetLocalPath(pair.Key, remote.RelativePath);
                if (Directory.Exists(target) || !targets.Add(SafePath.GetPhysicalPath(target)))
                    throw new InvalidDataException($"Download target '{target}' is a directory or is mapped more than once.");
            }
        }
    }

    internal static async Task VerifyPreparedDownloadAsync(AtomicFile.PreparedFile prepared, RemoteFileEntry remote, CancellationToken cancellationToken)
    {
        if ((remote.Size > 0 && new FileInfo(prepared.TemporaryPath!).Length != remote.Size) ||
            (remote.Hash is not null && !string.Equals(remote.Hash, await ComputeFileHashAsync(prepared.TemporaryPath!, cancellationToken), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException($"Downloaded file '{remote.RelativePath}' failed its integrity check.");
    }

    internal static async Task<SyncComparison> CompareContextAsync(SyncExecutionContext context, CancellationToken cancellationToken)
    {
        var entries = new List<SyncDiffEntry>();

        foreach (var root in context.ContentRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var localFiles = EnumerateLocalFiles(root).ToDictionary(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase);
            var remoteFiles = await ListRemoteFilesAsync(context, root, cancellationToken);
            var allRelativePaths = localFiles.Keys.Union(remoteFiles.Keys, StringComparer.OrdinalIgnoreCase);

            foreach (var relativePath in allRelativePaths)
            {
                localFiles.TryGetValue(relativePath, out var localFile);
                remoteFiles.TryGetValue(relativePath, out var remoteFile);

                var localHash = localFile is not null ? await ComputeFileHashAsync(localFile.AbsolutePath, cancellationToken) : null;
                long? localSize = localFile is not null ? new FileInfo(localFile.AbsolutePath).Length : null;
                if (remoteFile is not null && string.IsNullOrWhiteSpace(remoteFile.Hash))
                {
                    using var download = await context.StorageProvider.DownloadAsync(remoteFile.StoragePath, cancellationToken);
                    var hash = await SHA256.HashDataAsync(download.Content, cancellationToken);
                    remoteFile = remoteFile with { Hash = Convert.ToHexString(hash) };
                }

                entries.Add(new SyncDiffEntry
                {
                    RelativePath = BuildRemotePath(context.Binding.RemoteNamespace, root.StorageKey, relativePath),
                    ChangeKind = DetermineChangeKind(localFile, remoteFile, localHash, localSize),
                    LocalHash = localHash,
                    RemoteHash = remoteFile?.Hash,
                    LocalModifiedAtUtc = localFile?.LastModifiedUtc,
                    RemoteModifiedAtUtc = remoteFile?.LastModifiedUtc,
                });
            }
        }

        return new SyncComparison
        {
            Entries = entries,
            ConfigurationFingerprint = context.ConfigurationFingerprint,
        };
    }

}
